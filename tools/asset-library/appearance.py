#!/usr/bin/env python3
"""Faithful appearance descriptors for library assets (Python 3.9, stdlib only).

GET /api/assets/<id>/appearance answers, for one indexed asset, which model to
load and which real textures/materials go on it, using only evidence found in
the sources:

  1. Unity .meta externalObjects (FBX material name -> .mat GUID);
  2. prefabs that reference the model (MeshFilter / SkinnedMeshRenderer +
     renderer m_Materials, or a model PrefabInstance with material overrides);
  3. Unity material search by name (only when the importer imports materials);
  4. explicit texture links stored inside the FBX (Texture -> Material).

Nothing is guessed from "the first texture of the pack": when a slot has no
evidence it stays unbound and the descriptor says so (fidelity 'partial').

Bytes come from the repo copy, a loose library file, a zip member or a
.unitypackage member. Unity packages are gzip tars without random access: they
are streamed read-only (never extractall), only "<guid>/asset" regular members
of wanted GUIDs are copied into a bounded local cache, and the small text files
needed for bindings (.mat, .prefab, model .meta) go to one sidecar sqlite per
archive, built on demand.
"""

from __future__ import annotations

import hashlib
import json
import os
import re
import sqlite3
import stat
import struct
import tarfile
import threading
import time
import zipfile
from collections import OrderedDict
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Set, Tuple

from catalog import MAX_MEDIA, ArchiveCache, contained, ext_of, safe_label, stem_of

MAX_TEXT = 8 * 1024 * 1024
MAX_META = 4 * 1024 * 1024
BLOB_CACHE_LIMIT = 2 * 1024 * 1024 * 1024
SIDECAR_KEEP = 48
DESCRIPTOR_CACHE = 512
MAX_PREFAB_DEPTH = 3
MAX_PREFABS_PER_MODEL = 40

FILE_MIME = {
    ".fbx": "application/octet-stream", ".glb": "model/gltf-binary",
    ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".webp": "image/webp", ".tga": "image/x-tga",
    ".mat": "text/plain; charset=utf-8", ".prefab": "text/plain; charset=utf-8",
}
WEB_TEXTURE = {".png", ".jpg", ".jpeg", ".webp", ".tga"}
MODEL_KIND = {".fbx": "fbx", ".glb": "glb"}
SIDE_TEXT_EXT = {".mat", ".prefab"}
SIDE_META_EXT = {".fbx"}

TAR_MEMBER_RE = re.compile(r"^(?:\./)?([0-9a-f]{32})/(asset|asset\.meta)$")
GUID_RE = re.compile(r"^[0-9a-f]{32}$")
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)([^\n]*)$", re.M)
REF_RE = re.compile(r"\{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}))?(?:, type: (-?\d+))?\}")
GUID_ANY_RE = re.compile(r"guid: ([0-9a-f]{32})")

# Unity shader property names, most specific first.
MAP_KEYS = ("_BaseMap", "_Albedo_Map", "_Base_Map", "_BaseColorMap", "_Albedo", "_MainTex", "_Main_Texture",
            "_ColorMap", "_Texture")
NORMAL_KEYS = ("_BumpMap", "_Normal_Map", "_NormalMap", "_Normal")
EMISSION_KEYS = ("_EmissionMap", "_Emission_Map")
COLOR_KEYS = ("_BaseColor", "_Color", "_Albedo_Colour", "_Albedo_Color", "_Base_Color", "_Colour")
BUILTIN_GUIDS = {"0000000000000000f000000000000000", "0000000000000000e000000000000000"}

CLS_GAMEOBJECT, CLS_TRANSFORM, CLS_RECT, CLS_MESHFILTER = 1, 4, 224, 33
CLS_MESHRENDERER, CLS_SKINNED, CLS_PREFABINSTANCE = 23, 137, 1001


# -- Unity YAML (just the fields we need) -------------------------------------

def unity_docs(text: str) -> List[Tuple[int, str, bool, str]]:
    marks = list(DOC_RE.finditer(text))
    out = []
    for i, m in enumerate(marks):
        end = marks[i + 1].start() if i + 1 < len(marks) else len(text)
        out.append((int(m.group(1)), m.group(2), "stripped" in m.group(3), text[m.end():end]))
    return out


def _ref(body: str, name: str) -> Optional[Tuple[str, Optional[str]]]:
    m = re.search(r"^\s*%s: (\{[^}\n]*\})" % re.escape(name), body, re.M)
    if not m:
        return None
    r = REF_RE.match(m.group(1))
    return (r.group(1), r.group(2)) if r else None


def _ref_list(body: str, name: str) -> List[Tuple[str, Optional[str]]]:
    m = re.search(r"^(\s*)%s:[ \t]*\n((?:\1- \{[^}\n]*\}[ \t]*\n?)*)" % re.escape(name), body, re.M)
    if not m:
        return []
    return [(r.group(1), r.group(2)) for r in REF_RE.finditer(m.group(2))]


def _scalar(body: str, name: str) -> Optional[str]:
    m = re.search(r"^\s*%s: ?([^\n]*)$" % re.escape(name), body, re.M)
    return m.group(1).strip() if m else None


def _vector(body: str, name: str) -> Optional[List[float]]:
    m = re.search(r"^\s*%s: \{([^}\n]*)\}" % re.escape(name), body, re.M)
    if not m:
        return None
    vals = [_num(p.split(":")[1]) for p in m.group(1).split(",") if ":" in p]
    return [round(v, 6) for v in vals] if vals and all(v is not None for v in vals) else None  # type: ignore[misc]


def mesh_names_from_meta(meta: str) -> Dict[str, str]:
    """fileID -> object name, from fileIDToRecycleName (old) or internalIDToNameTable (new)."""
    out: Dict[str, str] = {}
    m = re.search(r"^  fileIDToRecycleName:\s*\n((?:    -?\d+: [^\n]*\n)*)", meta, re.M)
    if m:
        for line in m.group(1).splitlines():
            k, _, v = line.strip().partition(": ")
            out[k] = v.strip()
    for m in re.finditer(r"- first:\s*\n\s+(-?\d+): (-?\d+)\s*\n\s+second: ([^\n]*)", meta):
        out[m.group(2)] = m.group(3).strip()
    return out


def external_materials(meta: str) -> List[Tuple[str, str]]:
    """(FBX material name, material GUID) from ModelImporter externalObjects."""
    out = []
    for m in re.finditer(r"- first:\s*\n\s+type: UnityEngine:Material\s*\n\s+assembly: [^\n]*\n\s+name: ([^\n]*)\n"
                         r"\s+second: (\{[^}\n]*\})", meta):
        r = REF_RE.match(m.group(2))
        if r and r.group(2):
            out.append((m.group(1).strip(), r.group(2)))
    return out


def meta_imports_materials(meta: str) -> bool:
    for key in ("materialImportMode", "importMaterials"):
        v = _scalar(meta, key)
        if v is not None:
            return v.strip() != "0"
    return True


def avatar_source(meta: str) -> Optional[str]:
    for key in ("sourceAvatar", "lastHumanDescriptionAvatarSource"):
        r = _ref(meta, key)
        if r and r[1] and r[1] not in BUILTIN_GUIDS:
            return r[1]
    return None


def _num(s: str) -> Optional[float]:
    try:
        return float(s)
    except (TypeError, ValueError):
        return None


def parse_material(text: str) -> dict:
    name = _scalar(text, "m_Name") or ""
    shader = _ref(text, "m_Shader")
    tex: Dict[str, str] = {}
    for rx in (r"^\s*- (_?\w+):\s*\n\s*m_Texture: (\{[^}\n]*\})", r"name: (_?\w+)\s*\n\s*second:\s*\n\s*m_Texture: (\{[^}\n]*\})"):
        for m in re.finditer(rx, text, re.M):
            r = REF_RE.match(m.group(2))
            if r and r.group(2) and r.group(1) != "0" and m.group(1) not in tex:
                tex[m.group(1)] = r.group(2)
    floats: Dict[str, float] = {}
    for rx in (r"^\s*- (_?\w+): (-?[0-9.eE+-]+)[ \t]*$", r"name: (_?\w+)\s*\n\s*second: (-?[0-9.eE+-]+)[ \t]*$"):
        for m in re.finditer(rx, text, re.M):
            v = _num(m.group(2))
            if v is not None:
                floats.setdefault(m.group(1), v)
    colors: Dict[str, List[float]] = {}
    crx = r"\{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}"
    for rx in (r"^\s*- (_?\w+): " + crx, r"name: (_?\w+)\s*\n\s*second: " + crx):
        for m in re.finditer(rx, text, re.M):
            vals = [_num(m.group(i)) for i in range(2, 6)]
            if all(v is not None for v in vals):
                colors.setdefault(m.group(1), vals)  # type: ignore[arg-type]
    keywords = set(re.findall(r"\b(_[A-Z][A-Z0-9_]+)\b", "\n".join(re.findall(
        r"m_(?:ShaderKeywords|ValidKeywords):([^\n]*(?:\n  - [^\n]*)*)", text))))
    render_type = _scalar(text, "RenderType") or ""
    return {"name": name, "shaderGuid": shader[1] if shader else None, "shaderFileId": shader[0] if shader else None,
            "textures": tex, "floats": floats, "colors": colors, "keywords": sorted(keywords), "renderType": render_type}


def parse_prefab(text: str) -> dict:
    """GameObjects, renderers (with their mesh) and prefab instances of a Unity prefab."""
    gos: Dict[str, dict] = {}
    transforms: Dict[str, Tuple[Optional[str], Optional[str]]] = {}
    filters: Dict[str, Tuple[str, Optional[str]]] = {}
    renderers: List[dict] = []
    instances: List[dict] = []
    trs: Dict[str, dict] = {}
    for cls, fid, stripped, body in unity_docs(text):
        if stripped:
            continue
        if cls == CLS_GAMEOBJECT:
            gos[fid] = {"name": _scalar(body, "m_Name") or "", "active": (_scalar(body, "m_IsActive") or "1") != "0"}
        elif cls in (CLS_TRANSFORM, CLS_RECT):
            go, father = _ref(body, "m_GameObject"), _ref(body, "m_Father")
            transforms[fid] = (go[0] if go else None, father[0] if father and father[0] != "0" else None)
            trs[fid] = {k: _vector(body, "m_Local" + k) for k in ("Position", "Rotation", "Scale")}
        elif cls == CLS_MESHFILTER:
            go, mesh = _ref(body, "m_GameObject"), _ref(body, "m_Mesh")
            if go and mesh and mesh[1]:
                filters[go[0]] = (mesh[0], mesh[1])
        elif cls in (CLS_MESHRENDERER, CLS_SKINNED):
            go = _ref(body, "m_GameObject")
            mesh = _ref(body, "m_Mesh") if cls == CLS_SKINNED else None
            renderers.append({"go": go[0] if go else None, "mesh": mesh if mesh and mesh[1] else None,
                              "materials": _ref_list(body, "m_Materials"), "skinned": cls == CLS_SKINNED,
                              "enabled": (_scalar(body, "m_Enabled") or "1") != "0"})
        elif cls == CLS_PREFABINSTANCE:
            src = _ref(body, "m_SourcePrefab") or _ref(body, "m_ParentPrefab")
            mods = []
            for m in re.finditer(r"- target: (\{[^}\n]*\})\s*\n\s*propertyPath: ([^\n]*)\n\s*value: ?([^\n]*)\n"
                                 r"\s*objectReference: (\{[^}\n]*\})", body):
                t, o = REF_RE.match(m.group(1)), REF_RE.match(m.group(4))
                mods.append({"target": t.group(1) if t else None, "path": m.group(2).strip(), "value": m.group(3).strip(),
                             "ref": o.group(2) if o and o.group(2) else None})
            if src and src[1]:
                instances.append({"source": src[1], "mods": mods})
    go_tf = {go: tf for tf, (go, _) in transforms.items() if go}
    active_cache: Dict[str, bool] = {}

    def active(go: Optional[str], depth: int = 0) -> bool:
        if go is None or depth > 256:
            return True
        if go in active_cache:
            return active_cache[go]
        info = gos.get(go)
        ok = info["active"] if info else True
        if ok:
            tf = go_tf.get(go)
            father = transforms.get(tf, (None, None))[1] if tf else None
            parent_go = transforms.get(father, (None, None))[0] if father else None
            ok = active(parent_go, depth + 1) if parent_go else True
        active_cache[go] = ok
        return ok

    for r in renderers:
        if r["mesh"] is None and r["go"] in filters:
            r["mesh"] = filters[r["go"]]
        r["active"] = r["enabled"] and active(r["go"])
        r["goName"] = (gos.get(r["go"]) or {}).get("name", "")
        tf = go_tf.get(r["go"] or "")
        father = transforms.get(tf, (None, None))[1] if tf else None
        parent_go = transforms.get(father, (None, None))[0] if father else None
        r["parentName"] = (gos.get(parent_go or "") or {}).get("name") if parent_go else None
        r["local"] = {k.lower(): v for k, v in (trs.get(tf or "") or {}).items() if v is not None}
    return {"renderers": renderers, "instances": instances, "gameObjects": len(gos)}


# -- FBX (binary 7.x and ASCII) ----------------------------------------------

def parse_fbx(data: bytes) -> Optional[dict]:
    """Objects and connections needed for material/texture bindings, or None if unreadable."""
    try:
        if data.startswith(b"Kaydara FBX Binary  \x00"):
            return _fbx_finish(*_fbx_binary(data))
        head = data[:4096].decode("latin-1")
        if "FBXHeaderExtension" in head or "; FBX" in head:
            return _fbx_finish(*_fbx_ascii(data.decode("utf-8", "replace")))
    except (struct.error, ValueError, IndexError, KeyError):
        return None
    return None


_FBX_SCALAR = {b"Y": ("<h", 2), b"C": ("<?", 1), b"I": ("<i", 4), b"F": ("<f", 4), b"D": ("<d", 8), b"L": ("<q", 8)}


def _fbx_binary(data: bytes):
    version = struct.unpack_from("<I", data, 23)[0]
    hdr = struct.Struct("<QQQB" if version >= 7500 else "<IIIB")

    def props(pos: int, count: int) -> list:
        out = []
        for _ in range(count):
            t = data[pos:pos + 1]
            pos += 1
            if t in _FBX_SCALAR:
                fmt, size = _FBX_SCALAR[t]
                out.append(struct.unpack_from(fmt, data, pos)[0])
                pos += size
            elif t in (b"f", b"d", b"l", b"i", b"b", b"c"):
                _, _, clen = struct.unpack_from("<III", data, pos)
                pos += 12 + clen
                out.append(None)
            elif t in (b"S", b"R"):
                n = struct.unpack_from("<I", data, pos)[0]
                out.append(data[pos + 4:pos + 4 + n])
                pos += 4 + n
            else:
                raise ValueError("bad fbx property")
        return out

    def children(pos: int, end: int):
        while pos < end:
            if pos + hdr.size > len(data):
                return
            node_end, nprops, plen, nlen = hdr.unpack_from(data, pos)
            if node_end == 0:
                return
            if node_end <= pos or node_end > len(data):
                raise ValueError("bad fbx node")
            name = data[pos + hdr.size:pos + hdr.size + nlen]
            pstart = pos + hdr.size + nlen
            yield name, pstart, nprops, pstart + plen, node_end
            pos = node_end

    objects: List[Tuple[str, int, str, str, Dict[str, str]]] = []
    conns: List[Tuple[str, int, int, str]] = []
    for name, ps, np_, cs, ce in children(27, len(data)):
        if name == b"Objects":
            for oname, ops, onp, ocs, oce in children(cs, ce):
                if oname not in (b"Model", b"Material", b"Texture", b"Geometry", b"AnimationStack"):
                    continue
                p = props(ops, min(onp, 3))
                extra: Dict[str, str] = {}
                if oname == b"Texture":
                    for cname, cps, cnp, _, _ in children(ocs, oce):
                        if cname in (b"RelativeFilename", b"FileName") and cnp:
                            v = props(cps, 1)[0]
                            if isinstance(v, bytes):
                                extra[cname.decode()] = v.decode("utf-8", "replace")
                label = p[1].split(b"\x00\x01")[0].decode("utf-8", "replace") if len(p) > 1 and isinstance(p[1], bytes) else ""
                sub = p[2].decode("utf-8", "replace") if len(p) > 2 and isinstance(p[2], bytes) else ""
                objects.append((oname.decode(), int(p[0]), label, sub, extra))
        elif name == b"Connections":
            for cname, cps, cnp, _, _ in children(cs, ce):
                if cname != b"C" or cnp < 3:
                    continue
                p = props(cps, min(cnp, 4))
                kind = p[0].decode() if isinstance(p[0], bytes) else ""
                prop = p[3].decode("utf-8", "replace") if len(p) > 3 and isinstance(p[3], bytes) else ""
                conns.append((kind, int(p[1]), int(p[2]), prop))
    return objects, conns


def _fbx_ascii(text: str):
    objects = []
    obj_re = re.compile(r'^\s*(Model|Material|Texture|Geometry|AnimationStack): (-?\d+), "\w+::([^"]*)", "([^"]*)"', re.M)
    marks = list(obj_re.finditer(text))
    for i, m in enumerate(marks):
        extra: Dict[str, str] = {}
        if m.group(1) == "Texture":
            block = text[m.end():marks[i + 1].start() if i + 1 < len(marks) else min(len(text), m.end() + 8192)]
            for key in ("RelativeFilename", "FileName"):
                f = re.search(r'^\s*%s: "([^"]*)"' % key, block, re.M)
                if f:
                    extra[key] = f.group(1)
        objects.append((m.group(1), int(m.group(2)), m.group(3), m.group(4), extra))
    conns = [(m.group(1), int(m.group(2)), int(m.group(3)), m.group(4) or "")
             for m in re.finditer(r'C: "(OO|OP)",\s*(-?\d+),\s*(-?\d+)(?:,\s*"([^"]*)")?', text)]
    return objects, conns


def _fbx_finish(objects, conns) -> dict:
    by_id = {o[1]: o for o in objects}
    mats = {o[1]: o[2] for o in objects if o[0] == "Material"}
    models = {o[1]: o for o in objects if o[0] == "Model"}
    node_mats: Dict[str, List[str]] = {}
    mat_tex: Dict[str, List[Tuple[str, str]]] = {}
    for kind, child, parent, prop in conns:
        if child in mats and parent in models:
            node_mats.setdefault(models[parent][2], []).append(mats[child])
        elif kind == "OP" and parent in mats and child in by_id and by_id[child][0] == "Texture":
            tex = by_id[child][4]
            path = tex.get("RelativeFilename") or tex.get("FileName") or ""
            if path:
                mat_tex.setdefault(mats[parent], []).append((prop, path))
    geometries = sum(1 for o in objects if o[0] == "Geometry")
    mesh_nodes = [o[2] for o in models.values() if o[3] == "Mesh"]
    return {"materials": sorted(set(mats.values())), "nodeMaterials": node_mats, "materialTextures": mat_tex,
            "geometries": geometries, "meshNodes": mesh_nodes,
            "animations": sum(1 for o in objects if o[0] == "AnimationStack")}


def glb_info(data: bytes) -> Optional[dict]:
    if len(data) < 20 or data[:4] != b"glTF":
        return None
    length, ctype = struct.unpack_from("<II", data, 12)
    if ctype != 0x4E4F534A:
        return None
    try:
        doc = json.loads(data[20:20 + length].decode("utf-8"))
    except ValueError:
        return None
    images = doc.get("images") or []
    return {"materials": len(doc.get("materials") or []), "images": len(images),
            "externalImages": sum(1 for i in images if isinstance(i, dict) and i.get("uri") and not str(i["uri"]).startswith("data:")),
            "animations": len(doc.get("animations") or []), "meshes": len(doc.get("meshes") or [])}


# -- resolver -----------------------------------------------------------------

ROW_COLS = "id, name, lname, pack_id, pack_name, family, kind, path, fmt, src_fmt, archive, member, guid, local_root, local_rel, size, cache_key"


class AppearanceError(Exception):
    pass


class AppearanceResolver:
    def __init__(self, catalog: Any, cache_dir: Optional[Path] = None, blob_limit: int = BLOB_CACHE_LIMIT):
        self.catalog = catalog
        self.library_root = Path(catalog.library_root)
        self.unity_root = Path(catalog.unity_root)
        self.root = Path(cache_dir) if cache_dir else Path(catalog.data_dir) / "appearance"
        self.blob_dir = self.root / "blobs"
        self.side_dir = self.root / "sidecars"
        self.blob_dir.mkdir(parents=True, exist_ok=True)
        self.side_dir.mkdir(parents=True, exist_ok=True)
        self.blob_limit = blob_limit
        self._locks: Dict[str, threading.Lock] = {}
        self._locks_guard = threading.Lock()
        # asset id -> (descriptor, dependency stamps); see describe().
        self._memo: "OrderedDict[str, Tuple[dict, tuple]]" = OrderedDict()
        self._memo_lock = threading.Lock()
        self._memo_stamp: Optional[float] = None
        self._deps = threading.local()

    # -- descriptor dependencies --------------------------------------------

    def _dep(self, path: Path) -> None:
        """Record a file the descriptor being built depends on (only while describe() collects)."""
        paths = getattr(self._deps, "paths", None)
        if paths is not None:
            paths.add(str(path))

    @staticmethod
    def _stamps(paths: Iterable[str]) -> tuple:
        out = []
        for p in sorted(paths):
            try:
                st = os.stat(p)
                out.append((p, st.st_mtime_ns, st.st_size))
            except OSError:
                out.append((p, None, None))
        return tuple(out)

    # -- catalog rows -----------------------------------------------------

    def _con(self) -> sqlite3.Connection:
        con = self.catalog._connect()
        if con is None:
            raise AppearanceError("Aún no hay índice.")
        return con

    def row(self, asset_id: str) -> Optional[dict]:
        con = self._con()
        try:
            r = con.execute("SELECT %s FROM assets WHERE id=?" % ROW_COLS, (str(asset_id),)).fetchone()
        finally:
            con.close()
        return dict(r) if r else None

    def by_guid(self, guid: str, near: dict) -> Optional[dict]:
        if not guid or not GUID_RE.match(guid):
            return None
        con = self._con()
        try:
            rows = [dict(r) for r in con.execute("SELECT %s FROM assets WHERE guid=?" % ROW_COLS, (guid,))]
        finally:
            con.close()
        rows.sort(key=lambda r: (r["archive"] != near.get("archive"), r["pack_id"] != near.get("pack_id"), r["id"]))
        return rows[0] if rows else None

    def by_name(self, pack_id: str, lname: str, fmts: Iterable[str], src_fmt: Optional[str] = None) -> List[dict]:
        fmts = list(fmts)
        sql = "SELECT %s FROM assets WHERE pack_id=? AND lname=? AND fmt IN (%s)" % (ROW_COLS, ",".join("?" * len(fmts)))
        args: List[Any] = [pack_id, lname] + fmts
        if src_fmt:
            sql += " AND src_fmt=?"
            args.append(src_fmt)
        con = self._con()
        try:
            return [dict(r) for r in con.execute(sql, args)]
        finally:
            con.close()

    # -- bytes ------------------------------------------------------------

    def _lock(self, key: str) -> threading.Lock:
        with self._locks_guard:
            return self._locks.setdefault(key, threading.Lock())

    def _local_path(self, row: dict) -> Optional[Path]:
        if not row.get("local_rel") or not safe_label(row["local_rel"]):
            return None
        root = self.unity_root if row["local_root"] == "repo" else self.library_root
        self._dep(root / row["local_rel"])
        self._dep(root / (row["local_rel"] + ".meta"))
        return contained(root / row["local_rel"], root)

    def _blob_path(self, row: dict) -> Path:
        key = hashlib.sha1(("%s|%s" % (row["cache_key"] or row["archive"], row["guid"])).encode("utf-8")).hexdigest()
        return self.blob_dir / (key + ".bin")

    def read_bytes(self, row: dict, limit: int = MAX_MEDIA) -> bytes:
        """Exact bytes of an indexed asset, from whichever source holds them."""
        if (row.get("size") or 0) > limit:
            raise ValueError("El archivo supera %d MB." % (limit // (1024 * 1024)))
        local = self._local_path(row)
        if local is not None:
            with open(str(local), "rb") as fh:
                st = os.fstat(fh.fileno())
                if not stat.S_ISREG(st.st_mode) or st.st_size > limit:
                    raise ValueError("El archivo supera el límite.")
                data = fh.read(limit + 1)
            if len(data) > limit:
                # The file grew after the size check: never return a silently truncated copy.
                raise ValueError("El archivo cambió durante la lectura y supera el límite.")
            return data
        if row["src_fmt"] == "zip":
            return self._zip_bytes(row["archive"], row["member"], limit)
        if row["src_fmt"] == "unitypackage":
            got = self._tar_assets(row["archive"], row["cache_key"], {row["guid"]: row}, limit)
            if row["guid"] in got:
                return got[row["guid"]]
            raise FileNotFoundError("El recurso ya no está dentro del paquete Unity.")
        raise FileNotFoundError("Recurso sin origen legible.")

    def _zip_bytes(self, archive: str, member: str, limit: int) -> bytes:
        if not safe_label(archive or "") or not archive.lower().endswith(".zip") or not safe_label(member or ""):
            raise ValueError("Ruta no permitida.")
        self._dep(self.library_root / archive)
        path = contained(self.library_root / archive, self.library_root)
        if path is None:
            raise FileNotFoundError("No se encuentra el paquete (¿disco desconectado?).")
        with zipfile.ZipFile(str(path)) as zf:
            info = zf.getinfo(member)
            mode = (info.external_attr >> 16) & 0o170000  # 0: archiver stored no Unix type
            if info.is_dir() or (mode and mode != stat.S_IFREG) or info.file_size > limit:
                raise ValueError("Entrada no permitida.")
            with zf.open(info) as fh:
                data = fh.read(limit + 1)
        if len(data) > limit:
            raise ValueError("El archivo supera el límite.")
        return data

    def _archive_path(self, archive: str) -> Path:
        if not safe_label(archive or "") or not archive.endswith(".unitypackage"):
            raise ValueError("Paquete no permitido.")
        self._dep(self.library_root / archive)
        path = contained(self.library_root / archive, self.library_root)
        if path is None:
            raise FileNotFoundError("No se encuentra el paquete Unity (¿disco desconectado?).")
        return path

    def _archive_current(self, archive: str, cache_key: Optional[str]) -> Path:
        """Package path, refused when it no longer matches the indexed cache_key.

        Blobs and sidecars are keyed by cache_key, so every cache hit and every miss checks it first: a package
        replaced after indexing must never be served from a warm cache.
        """
        path = self._archive_path(archive)
        if cache_key and ArchiveCache.key_for(archive, path.stat()) != cache_key:
            # The package changed after indexing: its GUIDs, sizes and sidecar may no longer match the catalog.
            raise FileNotFoundError("El paquete Unity cambió desde el último índice; hay que reindexar.")
        return path

    @staticmethod
    def _blob_ok(p: Path) -> bool:
        try:
            return stat.S_ISREG(os.lstat(str(p)).st_mode)
        except OSError:
            return False

    @staticmethod
    def _read_blob(p: Path, limit: int) -> Optional[bytes]:
        """Cached blob bytes if p is a regular file (never a symlink); ValueError over the caller's limit."""
        try:
            st = os.lstat(str(p))
        except OSError:
            return None
        if not stat.S_ISREG(st.st_mode):
            return None
        if st.st_size > limit:
            raise ValueError("El archivo supera el límite.")
        try:
            with open(str(p), "rb") as fh:
                data = fh.read(limit + 1)
            os.utime(str(p), None)
        except OSError:
            return None
        if len(data) > limit:
            raise ValueError("El archivo supera el límite.")
        return data

    def _tar_assets(self, archive: str, cache_key: Optional[str], wanted: Dict[str, dict],
                    limit: int = MAX_MEDIA) -> Dict[str, bytes]:
        """Bytes of '<guid>/asset' for wanted GUIDs; cached ones first, the rest in one streamed pass."""
        self._archive_current(archive, cache_key)
        out: Dict[str, bytes] = {}
        missing: Dict[str, dict] = {}
        for guid, row in wanted.items():
            data = self._read_blob(self._blob_path(row), limit)
            if data is not None:
                out[guid] = data
            else:
                missing[guid] = row
        if not missing:
            return out
        with self._lock("tar:" + archive):
            still = {g: r for g, r in missing.items() if not self._blob_ok(self._blob_path(r))}
            if still:
                self._stream(archive, cache_key, assets=still)
        for guid, row in missing.items():
            data = self._read_blob(self._blob_path(row), limit)
            if data is not None:
                out[guid] = data
        self._prune_blobs()
        return out

    def _stream(self, archive: str, cache_key: Optional[str], assets: Optional[Dict[str, dict]] = None,
                side: Optional[sqlite3.Connection] = None, side_paths: Optional[Dict[str, str]] = None) -> None:
        """One read-only pass over a .unitypackage; never extracts to disk except wanted asset blobs."""
        assets = dict(assets or {})
        need_side = side is not None
        path = self._archive_current(archive, cache_key)
        started = time.time()
        with tarfile.open(str(path), mode="r|gz", bufsize=1 << 20) as tf:
            for m in tf:
                mm = TAR_MEMBER_RE.match(m.name)
                if not mm or m.type not in (tarfile.REGTYPE, tarfile.AREGTYPE) or m.size > MAX_MEDIA:
                    continue
                guid, leaf = mm.group(1), mm.group(2)
                want_blob = leaf == "asset" and guid in assets
                side_ext = ext_of(side_paths[guid]) if need_side and side_paths is not None and guid in side_paths else None
                want_text = side_ext is not None and leaf == "asset" and side_ext in SIDE_TEXT_EXT and m.size <= MAX_TEXT
                want_meta = side_ext is not None and leaf == "asset.meta" and side_ext in SIDE_META_EXT and m.size <= MAX_META
                if not (want_blob or want_text or want_meta):
                    continue
                # A streamed tar member can be read only once (r|gz cannot seek back): read it a single
                # time and reuse the bytes for the blob cache and for the sidecar text.
                fh = tf.extractfile(m)
                data = fh.read(m.size + 1) if fh else b""
                if want_blob:
                    if len(data) == m.size <= MAX_MEDIA:
                        dest = self._blob_path(assets[guid])
                        tmp = dest.with_name(dest.name + ".%d.tmp" % threading.get_ident())
                        tmp.write_bytes(data)
                        os.replace(str(tmp), str(dest))
                    del assets[guid]
                if want_text:
                    text = data[:MAX_TEXT].decode("utf-8", "replace")
                    side.execute("INSERT OR REPLACE INTO texts VALUES (?,?,?)", (guid, "asset", text))
                    if side_ext == ".prefab":
                        for ref in set(GUID_ANY_RE.findall(text)):
                            side.execute("INSERT INTO refs VALUES (?,?)", (guid, ref))
                elif want_meta:
                    side.execute("INSERT OR REPLACE INTO texts VALUES (?,?,?)",
                                 (guid, "meta", data[:MAX_META].decode("utf-8", "replace")))
                if not assets and not need_side:
                    break
        self._log("paso por %s: %.1f s" % (archive.rsplit("/", 1)[-1], time.time() - started))

    def _prune_blobs(self) -> None:
        try:
            files = [(p.stat().st_mtime, p.stat().st_size, p) for p in self.blob_dir.glob("*.bin")]
        except OSError:
            return
        total = sum(f[1] for f in files)
        for _, size, p in sorted(files):
            if total <= self.blob_limit:
                break
            try:
                p.unlink()
                total -= size
            except OSError:
                pass

    @staticmethod
    def _log(msg: str) -> None:
        import sys
        sys.stderr.write("[asset-library] apariencia: %s\n" % msg)

    # -- sidecar: small texts of one unitypackage ------------------------------

    def _sidecar(self, row: dict) -> Optional[sqlite3.Connection]:
        if row["src_fmt"] != "unitypackage" or not row.get("cache_key"):
            return None
        self._archive_current(row["archive"], row["cache_key"])
        p = self.side_dir / ("%s.sqlite" % row["cache_key"])
        if not p.is_file():
            with self._lock("tar:" + row["archive"]):
                if not p.is_file():
                    self._build_sidecar(row, p)
        os.utime(str(p), None)
        con = sqlite3.connect("file:%s?mode=ro" % p, uri=True, check_same_thread=False)
        return con

    def _build_sidecar(self, row: dict, dest: Path) -> None:
        con = self._con()
        try:
            paths = {r[0]: r[1] for r in con.execute("SELECT guid, path FROM assets WHERE archive=? AND guid IS NOT NULL",
                                                     (row["archive"],))
                     if ext_of(r[1]) in SIDE_TEXT_EXT | SIDE_META_EXT}
        finally:
            con.close()
        tmp = dest.with_name(dest.name + ".tmp")
        if tmp.exists():
            tmp.unlink()
        side = sqlite3.connect(str(tmp))
        try:
            side.execute("CREATE TABLE texts (guid TEXT, part TEXT, text TEXT, PRIMARY KEY (guid, part))")
            side.execute("CREATE TABLE refs (prefab TEXT, ref TEXT)")
            want = {row["guid"]: row} if ext_of(row["path"]) in FILE_MIME and not self._blob_ok(self._blob_path(row)) \
                and self._local_path(row) is None else {}
            self._stream(row["archive"], row["cache_key"], assets=want, side=side, side_paths=paths)
            side.execute("CREATE INDEX refs_ref ON refs(ref)")
            side.commit()
        finally:
            side.close()
        os.replace(str(tmp), str(dest))
        olds = sorted(self.side_dir.glob("*.sqlite"), key=lambda q: q.stat().st_mtime)
        for q in olds[:max(0, len(olds) - SIDECAR_KEEP)]:
            try:
                q.unlink()
            except OSError:
                pass

    def text(self, row: dict, part: str = "asset") -> Optional[str]:
        """Text of a .mat/.prefab ('asset') or of a model .meta ('meta'): repo copy first, then sidecar."""
        local = self._local_path(row)
        if local is not None:
            target = Path(str(local) + ".meta") if part == "meta" else local
            try:
                if target.is_file() and not target.is_symlink() and target.stat().st_size <= MAX_TEXT:
                    data = target.read_text("utf-8", "replace")
                    if part != "meta" or not row.get("guid") or ("guid: %s" % row["guid"]) in data:
                        return data
            except OSError:
                pass
        if row["src_fmt"] != "unitypackage":
            return None
        side = self._sidecar(row)
        if side is None:
            return None
        try:
            r = side.execute("SELECT text FROM texts WHERE guid=? AND part=?", (row["guid"], part)).fetchone()
        finally:
            side.close()
        return r[0] if r else None

    def prefabs_for(self, model: dict) -> List[dict]:
        """Prefabs of the same package that reference this model GUID; same-name ones first."""
        found: Dict[str, dict] = {}
        for r in self.by_name(model["pack_id"], model["lname"], ["prefab"]):
            if r["archive"] == model["archive"]:
                t = self.text(r)
                if t and model["guid"] in t:
                    found[r["id"]] = r
        if not found and model["src_fmt"] == "unitypackage":
            side = self._sidecar(model)
            if side is not None:
                try:
                    guids = [g for (g,) in side.execute("SELECT DISTINCT prefab FROM refs WHERE ref=? LIMIT 50", (model["guid"],))]
                finally:
                    side.close()
                for g in guids:
                    r = self.by_guid(g, model)
                    if r:
                        found[r["id"]] = r
        return sorted(found.values(), key=lambda r: (r["lname"] != model["lname"], len(r["path"]), r["path"]))

    # -- public API -------------------------------------------------------

    def prefetch(self, desc: dict) -> None:
        """Copy every package-only file the descriptor points to in one pass per package."""
        ids = [t["id"] for t in desc.get("textures") or []]
        for key in ("model", "animationModel"):
            if desc.get(key):
                ids.append(desc[key]["id"])
        groups: Dict[str, Dict[str, dict]] = {}
        for aid in ids:
            r = self.row(aid)
            if r and r["src_fmt"] == "unitypackage" and self._local_path(r) is None and r["guid"] \
                    and (r["size"] or 0) <= MAX_MEDIA and not self._blob_ok(self._blob_path(r)):
                groups.setdefault(r["archive"], {})[r["guid"]] = r
        for archive, wanted in groups.items():
            try:
                self._tar_assets(archive, next(iter(wanted.values()))["cache_key"], wanted)
            except (OSError, ValueError, tarfile.TarError, EOFError) as exc:
                desc.setdefault("warnings", []).append("No se pudieron copiar archivos del paquete Unity (%s)." % exc.__class__.__name__)

    def read_file(self, asset_id: str) -> Tuple[bytes, str]:
        row = self.row(asset_id)
        if row is None:
            raise KeyError(asset_id)
        ext = ext_of(row["path"])
        if ext not in FILE_MIME:
            raise ValueError("Este tipo de archivo no se sirve.")
        return self.read_bytes(row), FILE_MIME[ext]

    def describe(self, asset_id: str) -> dict:
        """Descriptor of one asset, memoized until the catalog or any file it was built from changes.

        Dependencies are the local files (and their .meta) and the packages read while building; each hit
        re-stats them (mtime_ns + size), so an edited .mat/.prefab/.meta/FBX or a replaced package invalidates
        the entry without a reindex. Blobs and sidecars of packages are keyed by the indexed cache_key and are
        refused when the package no longer matches it (see _stream).
        """
        try:
            stamp = self.catalog.db_path.stat().st_mtime_ns
        except OSError:
            stamp = None
        with self._memo_lock:
            if stamp != self._memo_stamp:
                self._memo.clear()
                self._memo_stamp = stamp
            hit = self._memo.get(asset_id)
        if hit is not None and self._stamps(p for p, _, _ in hit[1]) == hit[1]:
            with self._memo_lock:
                if asset_id in self._memo:
                    self._memo.move_to_end(asset_id)
            return json.loads(json.dumps(hit[0]))
        row = self.row(asset_id)
        if row is None:
            raise KeyError(asset_id)
        self._deps.paths = set()
        try:
            desc = _Builder(self, row).build()
            self.prefetch(desc)
            deps = self._stamps(self._deps.paths)
        finally:
            self._deps.paths = None
        with self._memo_lock:
            if self._memo_stamp == stamp:
                self._memo[asset_id] = (desc, deps)
                while len(self._memo) > DESCRIPTOR_CACHE:
                    self._memo.popitem(last=False)
        return json.loads(json.dumps(desc))


def file_url(asset_id: str) -> str:
    return "/api/assets/%s/file" % asset_id


class _Builder:
    """Builds one descriptor; collects warnings and coverage while resolving."""

    def __init__(self, res: AppearanceResolver, row: dict):
        self.res = res
        self.row = row
        self.warnings: List[str] = []
        self.textures: "OrderedDict[str, dict]" = OrderedDict()
        self.materials: List[dict] = []
        self.mat_cache: Dict[str, Optional[dict]] = {}
        self.bindings: List[str] = []
        self.unbound: List[str] = []
        self.texture_gaps = 0
        self.composite = False
        self.degraded = False
        self.fbx_ok = True

    def warn(self, msg: str) -> None:
        # A leading "!" marks a warning that lowers fidelity to 'partial'; it is not shown.
        if msg.startswith("!"):
            self.degraded = True
            msg = msg[1:]
        if msg not in self.warnings:
            self.warnings.append(msg)

    def model_ref(self, r: dict) -> dict:
        return {"id": r["id"], "kind": MODEL_KIND[ext_of(r["path"])], "url": file_url(r["id"]), "name": r["name"],
                "path": r["path"], "source": self.source_of(r)}

    def source_of(self, r: dict) -> str:
        if r.get("local_rel"):
            return "repo" if r["local_root"] == "repo" else "biblioteca"
        return r["src_fmt"]

    def build(self) -> dict:
        r = self.row
        ext = ext_of(r["path"])
        out: Dict[str, Any] = {"id": r["id"], "assetKind": r["kind"], "model": None, "textures": [], "materials": [],
                               "warnings": [], "fidelity": "unavailable"}
        if (r.get("size") or 0) > MAX_MEDIA:
            self.warn("El archivo pasa de 64 MB: no se sirve.")
        elif ext in (".fbx", ".glb"):
            self.describe_model(r, out)
        elif ext == ".obj":
            sib = self.sibling_fbx(r)
            if sib:
                self.warn("OBJ sin visor propio: se muestra su FBX equivalente del mismo paquete (%s)." % sib["path"])
                out["representedBy"] = sib["id"]
                self.describe_model(sib, out)
            else:
                self.warn("OBJ sin FBX equivalente único en el paquete: no hay vista 3D fiel.")
        elif ext == ".prefab":
            self.describe_prefab(r, out)
        elif ext == ".mat":
            text = self.res.text(r)
            if text is None:
                self.warn("No se pudo leer el material.")
            else:
                b = self.material_binding(r, None, None, None)
                if b:
                    out["material"] = b
        elif ext in WEB_TEXTURE or ext in (".psd", ".tif", ".tiff", ".exr", ".bmp", ".hdr"):
            self.add_texture(r, "imagen", None)
        elif ext == ".anim":
            self.warn("Clip .anim de Unity (YAML): el navegador no lo reproduce; las animaciones se ven en sus FBX.")
        else:
            self.warn("Este tipo de recurso no tiene vista de apariencia.")
        out["textures"] = list(self.textures.values())
        out["materials"] = self.materials
        out["fidelity"] = self.fidelity(out)
        out["coverage"] = {"bindings": self.bindings, "unboundMaterials": sorted(set(self.unbound)),
                           "textureGaps": self.texture_gaps, "composite": self.composite}
        out["warnings"] = self.warnings
        return out

    def fidelity(self, out: dict) -> str:
        ext = ext_of(self.row["path"])
        if ext == ".mat":
            if not out.get("material"):
                return "unavailable"
            return "partial" if self.texture_gaps or self.degraded else "resolved"
        if ext in WEB_TEXTURE:
            return "resolved" if self.textures else "unavailable"
        if out["model"] is None:
            return "unavailable"
        if not self.fbx_ok or self.unbound or self.texture_gaps or self.composite or not self.bindings or self.degraded:
            return "partial"
        return "resolved"

    # -- textures and materials -------------------------------------------

    def add_texture(self, r: dict, role: str, material: Optional[str]) -> Optional[str]:
        ext = ext_of(r["path"])
        if ext not in WEB_TEXTURE:
            self.texture_gaps += 1
            self.warn("La textura %s está en formato %s, que el navegador no muestra." % (r["name"], ext[1:].upper()))
            return None
        if (r.get("size") or 0) > MAX_MEDIA:
            self.texture_gaps += 1
            self.warn("La textura %s pasa de 64 MB." % r["name"])
            return None
        t = self.textures.get(r["id"])
        if t is None:
            t = {"id": r["id"], "name": r["name"], "path": r["path"], "url": file_url(r["id"]), "format": ext[1:],
                 "roles": [], "materials": [], "source": self.source_of(r)}
            self.textures[r["id"]] = t
        if role not in t["roles"]:
            t["roles"].append(role)
        if material and material not in t["materials"]:
            t["materials"].append(material)
        return t["url"]

    def parsed_material(self, mrow: dict) -> Optional[dict]:
        if mrow["id"] not in self.mat_cache:
            text = self.res.text(mrow)
            self.mat_cache[mrow["id"]] = parse_material(text) if text else None
        return self.mat_cache[mrow["id"]]

    def material_binding(self, mrow: dict, name: Optional[str], meshes: Optional[List[str]], slot: Optional[int],
                         evidence: str = "material") -> Optional[dict]:
        pm = self.parsed_material(mrow)
        if pm is None:
            self.warn("No se pudo leer el material %s." % mrow["name"])
            return None
        b: Dict[str, Any] = {"name": name or pm["name"] or mrow["name"], "unityMaterial": pm["name"] or mrow["name"],
                             "materialId": mrow["id"], "materialUrl": file_url(mrow["id"]), "evidence": evidence}
        if meshes:
            b["meshes"] = sorted(set(meshes))
        if slot is not None:
            b["slot"] = slot
        shader = self.shader_name(pm, mrow)
        if shader:
            b["shader"] = shader
            if shader != "Unity integrado":
                # A pack's own shader (Synty shader graphs, etc.): the viewer only approximates it.
                self.warn("!El material %s usa el shader propio %s; el visor lo aproxima con un material estándar."
                          % (pm["name"] or mrow["name"], shader))
        tex = pm["textures"]
        used: Set[str] = set()

        def tex_url(keys: Iterable[str], role: str) -> Optional[str]:
            for k in keys:
                g = tex.get(k)
                if not g:
                    continue
                used.add(k)
                trow = self.res.by_guid(g, mrow)
                if trow is None:
                    self.texture_gaps += 1
                    self.warn("El material %s apunta a una textura (%s) que no está en el catálogo." % (b["unityMaterial"], k))
                    return None
                return self.add_texture(trow, role, b["unityMaterial"])
            return None

        m = tex_url(MAP_KEYS, "map")
        if m:
            b["map"] = m
        n = tex_url(NORMAL_KEYS, "normalMap")
        if n:
            b["normalMap"] = n
        fl, co = pm["floats"], pm["colors"]
        kw = set(pm["keywords"])
        emissive_on = "_EMISSION" in kw or fl.get("_Enable_Emission", 0) == 1
        if emissive_on:
            e = tex_url(EMISSION_KEYS, "emissiveMap")
            if e:
                b["emissiveMap"] = e
            ec = co.get("_EmissionColor") or co.get("_Emission_Color")
            if ec:
                b["emissive"] = [round(c, 4) for c in ec[:3]]
        color = next((co[k] for k in COLOR_KEYS if k in co), None)
        if color:
            b["color"] = [round(c, 4) for c in color[:3]]
        transparent = fl.get("_Surface") == 1 or pm["renderType"].startswith("Transparent") and \
            pm["renderType"] != "TransparentCutout" or fl.get("_Mode") in (2, 3)
        if transparent:
            b["transparent"] = True
            b["opacity"] = round(color[3], 4) if color else 1.0
        if fl.get("_AlphaClip") == 1 or "_ALPHATEST_ON" in kw or pm["renderType"] == "TransparentCutout" or fl.get("_Mode") == 1:
            b["alphaTest"] = round(fl.get("_Cutoff", fl.get("_Alpha_Clip_Threshold", 0.5)), 4)
        if "_Metallic" in fl:
            b["metalness"] = round(fl["_Metallic"], 4)
        smooth = fl.get("_Smoothness", fl.get("_Glossiness"))
        if smooth is not None:
            b["roughness"] = round(1 - smooth, 4)
        if fl.get("_Cull") == 0:
            b["doubleSided"] = True
        extra = {k: g for k, g in tex.items() if k not in used and k not in EMISSION_KEYS
                 and not k.startswith("unity_") and k not in MAP_KEYS and k not in NORMAL_KEYS}
        if extra:
            # Maps the standard viewer does not combine (detail, mask, occlusion, custom shader inputs):
            # the look is partial even when the textures are found and listed.
            maps = {}
            for k, g in sorted(extra.items()):
                trow = self.res.by_guid(g, mrow)
                if trow is not None:
                    url = self.add_texture(trow, k, b["unityMaterial"])
                    if url:
                        maps[k] = url
            if maps:
                b["extraMaps"] = maps
            self.warn("!El material %s usa mapas del shader (%s) que el visor estándar no combina."
                      % (b["unityMaterial"], ", ".join(sorted(extra))))
        if "map" not in b and not any(k in tex for k in MAP_KEYS) and color is None:
            self.warn("!El material %s no declara textura base ni color." % b["unityMaterial"])
        self.materials.append(b)
        self.bindings.append(evidence)
        return b

    def shader_name(self, pm: dict, mrow: dict) -> Optional[str]:
        g = pm.get("shaderGuid")
        if not g:
            return None
        if g in BUILTIN_GUIDS:
            return "Unity integrado"
        srow = self.res.by_guid(g, mrow)
        return srow["name"] if srow else None

    # -- models ---------------------------------------------------------------

    def sibling_fbx(self, r: dict) -> Optional[dict]:
        cands = self.res.by_name(r["pack_id"], r["lname"], ["fbx"])
        same = [c for c in cands if c["archive"] == r["archive"]]
        for group in (same, [c for c in cands if c["src_fmt"] == "unitypackage"], cands):
            if len(group) == 1:
                return group[0]
        return None

    def unity_equivalent(self, r: dict) -> Optional[dict]:
        cands = [c for c in self.res.by_name(r["pack_id"], r["lname"], [r["fmt"]], "unitypackage")]
        return cands[0] if len(cands) == 1 else None

    def describe_model(self, m: dict, out: dict, visible: Optional[List[str]] = None) -> None:
        try:
            data = self.res.read_bytes(m)
        except (OSError, ValueError, KeyError, tarfile.TarError, zipfile.BadZipFile, EOFError) as exc:
            self.warn("No se pudo leer el modelo (%s)." % exc.__class__.__name__)
            return
        out["model"] = self.model_ref(m)
        if ext_of(m["path"]) == ".glb":
            info = glb_info(data)
            if info is None:
                self.warn("!GLB ilegible.")
                return
            out["embedded"] = info
            if info["externalImages"]:
                self.warn("!El GLB referencia %d imágenes externas que no se sirven." % info["externalImages"])
            self.bindings.append("glb-embedded")
            return
        fbx = parse_fbx(data)
        if fbx is None:
            self.fbx_ok = False
            self.warn("!No se pudo leer la estructura del FBX (versión antigua o ASCII raro): sin comprobar materiales.")
            fbx = {"materials": [], "nodeMaterials": {}, "materialTextures": {}, "geometries": 0, "meshNodes": [], "animations": 0}
        out["embedded"] = {"materials": fbx["materials"], "meshes": len(fbx["meshNodes"]), "animations": fbx["animations"]}
        if visible:
            out["visibleNodes"] = sorted(set(visible))
        if fbx["animations"]:
            out["animations"] = fbx["animations"]
        if not fbx["geometries"]:
            self.describe_animation_only(m, out)
            return
        slots = self.slots(fbx, visible)
        if m["src_fmt"] == "unitypackage":
            self.bind_unity(m, fbx, slots)
        else:
            eq = self.unity_equivalent(m) if ext_of(m["path"]) == ".fbx" else None
            if eq is not None:
                same = self.same_structure(eq, fbx)
                if same:
                    self.warn("Materiales tomados del FBX equivalente de Unity del mismo paquete (%s): mismas mallas y "
                              "materiales." % eq["path"])
                    self.bind_unity(eq, fbx, slots)
                elif same is False:
                    out["unityEquivalent"] = {"id": eq["id"], "appearanceUrl": "/api/assets/%s/appearance" % eq["id"]}
                    self.warn("El FBX de Unity del mismo nombre tiene otras mallas o materiales; no se le copian sus "
                              "materiales.")
            if not self.covered_all(slots):
                self.bind_fbx_textures(m, fbx, slots)
        self.check_coverage(slots)

    def same_structure(self, eq: dict, fbx: dict) -> Optional[bool]:
        """True if the Unity FBX has the same mesh nodes and material slots; None if unreadable."""
        try:
            other = parse_fbx(self.res.read_bytes(eq))
        except (OSError, ValueError, KeyError, tarfile.TarError, zipfile.BadZipFile, EOFError):
            return None
        if other is None:
            return None
        return other["nodeMaterials"] == fbx["nodeMaterials"] and sorted(other["meshNodes"]) == sorted(fbx["meshNodes"])

    def describe_animation_only(self, m: dict, out: dict) -> None:
        self.bindings.append("animation")
        meta = self.res.text(m, "meta") if m["src_fmt"] == "unitypackage" else None
        g = avatar_source(meta) if meta else None
        src = self.res.by_guid(g, m) if g else None
        if src is not None and ext_of(src["path"]) in MODEL_KIND and src["id"] != m["id"]:
            out["animationModel"] = self.model_ref(src)
            out["animationModel"]["evidence"] = "avatar de origen en el .meta"
        else:
            self.warn("!FBX de animación sin malla y sin modelo de avatar resoluble: no hay cuerpo donde reproducirla.")

    @staticmethod
    def slots(fbx: dict, visible: Optional[List[str]]) -> List[Tuple[str, int, str]]:
        nodes = fbx["meshNodes"] if not visible else [n for n in fbx["meshNodes"] if n in set(visible)]
        out = []
        for n in nodes:
            for i, mat in enumerate(fbx["nodeMaterials"].get(n, [])):
                out.append((n, i, mat))
        return out

    def covered(self, slot: Tuple[str, int, str]) -> bool:
        node, idx, name = slot
        for b in self.materials:
            if b.get("meshes"):
                if node in b["meshes"] and b.get("slot", idx) == idx:
                    return True
            elif b["name"] == name:
                return True
        return False

    def covered_all(self, slots: List[Tuple[str, int, str]]) -> bool:
        return bool(slots) and all(self.covered(s) for s in slots)

    def check_coverage(self, slots: List[Tuple[str, int, str]]) -> None:
        if not slots:
            if not self.materials:
                self.warn("!El FBX no enlaza materiales a sus mallas y no hay otra evidencia de apariencia.")
            return
        for s in slots:
            if not self.covered(s):
                self.unbound.append(s[2])
        if self.unbound:
            self.warn("!Sin evidencia de textura para: %s (se verían con el material del FBX)." % ", ".join(sorted(set(self.unbound))))

    def bind_unity(self, m: dict, fbx: dict, slots: List[Tuple[str, int, str]]) -> None:
        meta = self.res.text(m, "meta")
        if meta is None:
            self.warn("!No se pudo leer el .meta del FBX en el paquete Unity.")
        else:
            for name, guid in external_materials(meta):
                mrow = self.res.by_guid(guid, m)
                if mrow is None:
                    self.warn("!El .meta remapea %s a un material que no está en el catálogo." % name)
                    continue
                self.material_binding(mrow, name, None, None, "meta-externalObjects")
        if self.covered_all(slots):
            return
        names = mesh_names_from_meta(meta or "")
        prefabs = self.res.prefabs_for(m)
        used = []
        for p in prefabs[:MAX_PREFABS_PER_MODEL]:
            if self.covered_all(slots):
                break
            text = self.res.text(p)
            if not text:
                continue
            before = len(self.materials)
            self.bind_from_prefab(parse_prefab(text), m, names, fbx, "prefab:%s" % p["name"], skip_covered=True)
            if len(self.materials) > before:
                used.append(p["name"])
        if len(used) > 1 or (used and len(prefabs) > 1):
            self.warn("Materiales de %d de los %d prefabs que usan este modelo (%s%s)."
                      % (len(used), len(prefabs), ", ".join(used[:4]), "…" if len(used) > 4 else ""))
        if self.covered_all(slots) or meta is None or not meta_imports_materials(meta):
            return
        # Unity material search by name: only meaningful when the importer imports materials.
        for name in fbx["materials"]:
            if any(b["name"] == name for b in self.materials):
                continue
            cands = [c for c in self.res.by_name(m["pack_id"], name.lower(), ["mat"]) if c["archive"] == m["archive"]]
            if len(cands) == 1:
                self.material_binding(cands[0], name, None, None, "busqueda-por-nombre")
                self.warn("El material %s se asocia por nombre, como hace la búsqueda de Unity." % name)

    def bind_from_prefab(self, pf: dict, m: dict, names: Dict[str, str], fbx: dict, evidence: str,
                         skip_covered: bool = False) -> List[str]:
        """Bind renderers of prefab pf whose mesh comes from model m; return the visible node names."""
        visible: List[str] = []
        merged: "OrderedDict[Tuple[str, Optional[str], int], List[str]]" = OrderedDict()
        for r in pf["renderers"]:
            if not r["active"] or not r["mesh"] or r["mesh"][1] != m["guid"]:
                continue
            node = names.get(r["mesh"][0]) or r["goName"]
            visible.append(node)
            fbx_mats = fbx["nodeMaterials"].get(node, [])
            for i, (fid, guid) in enumerate(r["materials"]):
                if not guid:
                    continue
                if skip_covered and self.covered((node, i, fbx_mats[i] if i < len(fbx_mats) else "")):
                    continue
                key = (guid, fbx_mats[i] if i < len(fbx_mats) else None, i)
                merged.setdefault(key, []).append(node)
        for inst in pf["instances"]:
            if inst["source"] != m["guid"]:
                continue
            for mod in inst["mods"]:
                mm = re.match(r"m_Materials\.Array\.data\[(\d+)\]$", mod["path"])
                if mm and mod["ref"]:
                    target = names.get(mod["target"] or "")
                    i = int(mm.group(1))
                    fbx_mats = fbx["nodeMaterials"].get(target or "", [])
                    key = (mod["ref"], fbx_mats[i] if i < len(fbx_mats) else None, i)
                    merged.setdefault(key, [])
                    if target:
                        merged[key].append(target)
        for (guid, fbx_name, i), nodes in merged.items():
            mrow = self.res.by_guid(guid, m)
            if mrow is None:
                self.warn("!El prefab usa un material que no está en el catálogo.")
                continue
            if nodes:
                self.material_binding(mrow, fbx_name, nodes, i, evidence)
            elif fbx_name or len(fbx["materials"]) == 1:
                self.material_binding(mrow, fbx_name or fbx["materials"][0], None, None, evidence)
            else:
                self.warn("!El prefab cambia un material sin que se sepa a qué malla va.")
        return visible

    def bind_fbx_textures(self, m: dict, fbx: dict, slots: List[Tuple[str, int, str]]) -> None:
        """Explicit Texture -> Material links stored in the FBX itself."""
        for name, links in fbx["materialTextures"].items():
            if any(b["name"] == name for b in self.materials):
                continue
            b: Dict[str, Any] = {"name": name, "evidence": "fbx-texture-link"}
            for prop, path in links:
                role = "normalMap" if "normal" in prop.lower() or "bump" in prop.lower() else \
                    ("map" if "diffuse" in prop.lower() or prop == "" else None)
                if role is None or role in b:
                    continue
                base = path.replace("\\", "/").rsplit("/", 1)[-1]
                trow, exact = self.texture_by_basename(m, base)
                if trow is None:
                    self.texture_gaps += 1
                    self.warn("!El FBX enlaza %s a %s, que no está en el paquete." % (name, base))
                    continue
                if not exact:
                    self.warn("!%s: el FBX pide %s; se usa %s (mismo nombre, otro formato)." % (name, base, trow["path"]))
                url = self.add_texture(trow, role, name)
                if url:
                    b[role] = url
            if "map" in b or "normalMap" in b:
                self.materials.append(b)
                self.bindings.append("fbx-texture-link")

    def texture_by_basename(self, m: dict, base: str) -> Tuple[Optional[dict], bool]:
        stem = stem_of(base).lower()
        ext = ext_of(base)
        cands = self.res.by_name(m["pack_id"], stem, ["png", "jpg", "jpeg", "tga", "webp", "psd", "tif", "tiff"])
        if not cands:
            return None, False
        for exact in (True, False):
            group = [c for c in cands if (ext_of(c["path"]) == ext) == exact]
            same = [c for c in group if c["archive"] == m["archive"]] or group
            if exact and len({c["path"] for c in same}) == 1:
                return same[0], True
            web = [c for c in same if ext_of(c["path"]) in WEB_TEXTURE]
            if not exact and len({c["path"] for c in web}) == 1:
                return web[0], False
        return None, False

    # -- prefabs ----------------------------------------------------------------

    def describe_prefab(self, p: dict, out: dict, depth: int = 0) -> None:
        text = self.res.text(p)
        if text is None:
            self.warn("No se pudo leer el prefab.")
            return
        pf = parse_prefab(text)
        groups: "OrderedDict[str, int]" = OrderedDict()
        skinned: Set[str] = set()
        for r in pf["renderers"]:
            if r["active"] and r["mesh"]:
                groups[r["mesh"][1]] = groups.get(r["mesh"][1], 0) + 1
                if r["skinned"]:
                    skinned.add(r["mesh"][1])
        for inst in pf["instances"]:
            groups[inst["source"]] = groups.get(inst["source"], 0) + 1
        models: List[Tuple[dict, int]] = []
        for guid, count in groups.items():
            r = self.res.by_guid(guid, p)
            if r is None:
                self.warn("!El prefab usa una malla o prefab que no está en el catálogo.")
                continue
            ext = ext_of(r["path"])
            if ext in MODEL_KIND:
                models.append((r, count))
            elif ext == ".prefab":
                self.composite = True
                out.setdefault("parts", []).append({"prefab": {"id": r["id"], "name": r["name"],
                                                               "appearanceUrl": "/api/assets/%s/appearance" % r["id"]}})
                self.warn("!El prefab anida otro prefab (%s): se lista en parts, no se monta." % r["name"])
            else:
                self.warn("!El prefab usa una malla %s que no es un modelo servible (%s)." % (ext[1:], r["name"]))
        if not models:
            if not out.get("parts"):
                self.warn("El prefab no tiene mallas visibles.")
            return
        # Primary: the model the prefab is named after, else the skinned body, else the most used.
        lname = p["lname"]
        node_names = {r["mesh"][1]: (r["goName"] or "").lower() for r in pf["renderers"] if r["active"] and r["mesh"]}
        models.sort(key=lambda x: (x[0]["lname"] != lname and node_names.get(x[0]["guid"]) != lname,
                                   x[0]["guid"] not in skinned, -x[1]))
        primary = models[0][0]
        if len(models) > 1:
            self.composite = True
            self.warn("!Prefab compuesto de %d modelos: se monta %s; el resto va en parts con su hueso padre y su "
                      "transformación local." % (len(models), primary["name"]))
        for r, _ in models:
            meta = self.res.text(r, "meta") if r["src_fmt"] == "unitypackage" else None
            names = mesh_names_from_meta(meta or "")
            try:
                data = self.res.read_bytes(r)
                fbx = parse_fbx(data) if ext_of(r["path"]) == ".fbx" else None
            except (OSError, ValueError, KeyError, tarfile.TarError, zipfile.BadZipFile, EOFError):
                data, fbx = None, None
            fbx = fbx or {"materials": [], "nodeMaterials": {}, "materialTextures": {}, "geometries": 0, "meshNodes": [], "animations": 0}
            if r is primary:
                if data is None:
                    self.warn("!No se pudo leer el modelo del prefab.")
                    return
                out["model"] = self.model_ref(r)
                visible = self.bind_from_prefab(pf, r, names, fbx, "prefab")
                out["embedded"] = {"materials": fbx["materials"], "meshes": len(fbx["meshNodes"]), "animations": fbx["animations"]}
                if fbx["animations"]:
                    out["animations"] = fbx["animations"]
                if visible and fbx["meshNodes"] and set(fbx["meshNodes"]) - set(visible):
                    out["visibleNodes"] = sorted(set(visible))
                inst_only = not visible
                slots = self.slots(fbx, None if inst_only else visible)
                if inst_only and not self.covered_all(slots) and r["src_fmt"] == "unitypackage":
                    # Model prefab instance without overrides: the FBX import binding applies.
                    self.bind_unity(r, fbx, slots)
                self.check_coverage(slots)
            else:
                part: Dict[str, Any] = {"model": self.model_ref(r), "appearanceUrl": "/api/assets/%s/appearance" % r["id"]}
                rends = [rr for rr in pf["renderers"] if rr["active"] and rr["mesh"] and rr["mesh"][1] == r["guid"]]
                vis = sorted({names.get(rr["mesh"][0]) or rr["goName"] for rr in rends})
                if vis:
                    part["visibleNodes"] = vis
                if len(rends) == 1:
                    part["parentName"] = rends[0]["parentName"]
                    part["local"] = rends[0]["local"]
                before = len(self.materials)
                self.bind_from_prefab(pf, r, names, fbx, "prefab")
                for b in self.materials[before:]:
                    b["modelId"] = r["id"]  # bindings without modelId belong to the main model
                part["materials"] = [b["name"] for b in self.materials[before:]]
                out.setdefault("parts", []).append(part)
