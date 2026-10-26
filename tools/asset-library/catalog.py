#!/usr/bin/env python3
"""Synty asset catalog indexer for the asset library server (Python 3.9, stdlib only).

Reads the external Synty library (read-only), indexes archive members without
extracting them (zip central directory, streamed .unitypackage tar headers),
detects real usages in the repo (Unity GUID references, Unity Resources JSON,
Three.js sources.json) and stores everything in data_dir/catalog.sqlite.

Reindex is atomic: a new catalog.sqlite.tmp is built and swapped in with
os.replace; on failure the previous catalog stays untouched.

CLI:  python3 catalog.py --index [--library-root P] [--repo-root P] [--data-dir P]
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sqlite3
import stat
import sys
import tarfile
import threading
import time
import zipfile
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Tuple

SCHEMA_VERSION = "1"
DEFAULT_LIBRARY = Path("/Volumes/4T_White/50_INBOX/Synty")
MAX_MEDIA = 64 * 1024 * 1024
MAX_PREVIEW_BLOB = 256 * 1024
MAX_LIMIT = 100
DEFAULT_LIMIT = 60

# Packs listed on the Synty web that are missing from the local inventory.
EXTRA_WEB_PACKS = ["SIDEKICK - Modern Thugs"]
EXTRA_DIRS = ["_Biblioteca"]

MEDIA_MIME = {
    ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".webp": "image/webp",
    ".fbx": "application/octet-stream", ".glb": "model/gltf-binary",
}
IMAGE_EXT = {".png", ".jpg", ".jpeg", ".webp"}
TEXTURE_EXT = IMAGE_EXT | {".tga", ".psd", ".tif", ".tiff", ".exr", ".bmp", ".hdr"}
MODEL_EXT = {".fbx", ".obj", ".glb", ".gltf", ".blend", ".dae", ".3ds", ".max", ".ma", ".mb"}
SKIP_EXT = {
    ".meta", ".cs", ".dll", ".asmdef", ".asmref", ".js", ".pdb", ".so", ".dylib", ".md", ".txt", ".pdf",
    ".json", ".xml", ".rsp", ".ds_store", ".ini", ".uproject", ".bat", ".sh", ".py", ".url", ".html", ".htm",
    ".zip", ".rar", ".7z", ".unitypackage", ".mtl", ".csv", ".log", ".cginc", ".hlsl", ".uplugin", ".h", ".cpp",
}
UNREAL_EXT = {".uasset", ".umap"}

GUID_YAML_RE = re.compile(r"guid:\s*([0-9a-f]{32})")
GUID_USS_RE = re.compile(r"guid=([0-9a-f]{32})")
META_GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.M)
SLUG_RE = re.compile(r"[^a-z0-9]+")
VERSION_RE = re.compile(r"\d+")

# Pack aliases used by unity/Bastion/Assets/Bastion/Client/Resources/*.json.
PACK_ALIASES = {
    "apocalypse": "POLYGON - Apocalypse Pack",
    "town": "POLYGON - Town Pack",
    "city": "POLYGON - City Pack",
    "military": "POLYGON - Military Pack",
    "office": "POLYGON - Office Pack",
    "farm": "POLYGON - Farm Pack",
    "construction": "POLYGON - Construction Pack",
    "nature": "POLYGON - Nature Pack",
    "woodland": "POLYGON - Woodland Apocalypse Map",
    "alpine": "POLYGON - Alpine Mountain - Nature Biome",
    "baseLocomotion": "ANIMATION - Base Locomotion",
    "idles": "ANIMATION - Idles",
    "bowCombat": "ANIMATION - Bow Combat",
    "swordCombat": "ANIMATION - Sword Combat",
    "emotes": "ANIMATION - Emotes and Taunts",
    "goblinLocomotion": "ANIMATION - Goblin Locomotion",
}
NON_SYNTY_ALIASES = {"mixamo"}
RESOURCE_JSONS = ["MapArt", "Pieces", "Anims", "Wardrobe"]
UNITY_REF_EXT = {".asset", ".unity", ".mat", ".uss", ".prefab", ".controller", ".overridecontroller"}
# Families shown in the library but not allowed in Bastión (CLAUDE.md: no Synty SIMPLE nor MINI).
DISALLOWED_FAMILIES = ("SIMPLE", "POLYGON MINI")
UNITY_PROJECT_REL = "unity/Bastion"
APPEARANCE_EXT = {".fbx", ".glb", ".obj", ".prefab", ".mat", ".png", ".jpg", ".jpeg", ".webp", ".tga"}


# -- small helpers ----------------------------------------------------------

def slug(name: str) -> str:
    return SLUG_RE.sub("-", name.lower()).strip("-") or "pack"


def family_of(name: str) -> str:
    if name.startswith("_"):
        return "BIBLIOTECA"
    return name.split(" - ", 1)[0].strip().upper() if " - " in name else "OTROS"


def asset_id(pack_id: str, archive: str, member: str) -> str:
    return hashlib.sha256(("v1\x00%s\x00%s\x00%s" % (pack_id, archive, member)).encode("utf-8")).hexdigest()


def ext_of(path: str) -> str:
    base = path.rsplit("/", 1)[-1]
    return ("." + base.rsplit(".", 1)[-1].lower()) if "." in base else ""


def stem_of(path: str) -> str:
    base = path.rsplit("/", 1)[-1]
    return base.rsplit(".", 1)[0] if "." in base else base


def version_key(path: str) -> Tuple[int, ...]:
    return tuple(int(x) for x in VERSION_RE.findall(path.rsplit("/", 1)[-1]))


def classify(path: str, ext: str, family: str) -> str:
    low = path.lower()
    name = low.rsplit("/", 1)[-1]
    if ext == ".anim" or ext == ".controller" or ext == ".overridecontroller":
        return "animacion"
    if ext == ".mat":
        return "material"
    if ext == ".prefab":
        if family == "SIDEKICK" or "/characters/" in low or name.startswith(("sk_chr", "chr_", "character_")):
            return "personaje"
        if family == "INTERFACE" or "/ui/" in low or "/interface" in low:
            return "interfaz"
        return "prefab"
    if ext in TEXTURE_EXT:
        if family == "INTERFACE" or "/ui/" in low or "/icons/" in low or "/sprites/" in low or "_icon" in name:
            return "interfaz"
        return "textura"
    if ext in MODEL_EXT:
        if family == "ANIMATION" or "/animations/" in low or "/animation/" in low or name.startswith("a_"):
            return "animacion"
        if family == "SIDEKICK" or "/characters/" in low or name.startswith(("sk_chr", "chr_", "character_", "sk_")):
            return "personaje"
        return "modelo"
    if ext in UNREAL_EXT:
        if name.startswith(("sm_",)):
            return "modelo"
        if name.startswith(("sk_",)):
            return "personaje"
        if name.startswith(("t_",)):
            return "textura"
        if name.startswith(("m_", "mi_")):
            return "material"
        if name.startswith(("a_", "as_", "am_")):
            return "animacion"
        if name.startswith(("wbp_", "ui_")):
            return "interfaz"
        return "otro"
    return "otro"


def useful_member(path: str) -> bool:
    if not path or path.endswith("/"):
        return False
    low = path.lower()
    if "__macosx/" in low or low.rsplit("/", 1)[-1].startswith("."):
        return False
    ext = ext_of(path)
    return ext != "" and ext not in SKIP_EXT


def safe_label(path: str) -> bool:
    return not (path.startswith("/") or "\x00" in path or any(p == ".." for p in path.split("/")))


def iso_now() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%S%z")


def atomic_write(path: Path, data: bytes) -> None:
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_bytes(data)
    os.replace(str(tmp), str(path))


def contained(path: Path, root: Path) -> Optional[Path]:
    """Resolve path and return it only if it is a regular file inside root."""
    try:
        resolved = path.resolve()
        root_res = root.resolve()
    except OSError:
        return None
    try:
        resolved.relative_to(root_res)
    except ValueError:
        return None
    try:
        st = os.lstat(str(path))
    except OSError:
        return None
    if stat.S_ISLNK(st.st_mode) or not stat.S_ISREG(st.st_mode):
        return None
    return resolved


# -- archive scanning (cached per archive) ----------------------------------

def scan_zip(path: Path) -> List[dict]:
    out = []
    with zipfile.ZipFile(str(path)) as zf:
        for info in zf.infolist():
            name = info.filename
            if info.is_dir() or not useful_member(name) or not safe_label(name):
                continue
            mode = (info.external_attr >> 16) & 0o170000
            if mode == stat.S_IFLNK:
                continue
            out.append({"member": name, "size": info.file_size})
    return out


def scan_unitypackage(path: Path) -> List[dict]:
    """Stream the gzip tar once; read only pathname and preview.png, skip asset data."""
    entries: Dict[str, dict] = {}
    with tarfile.open(str(path), mode="r|gz", bufsize=1 << 20) as tf:
        for m in tf:
            name = m.name[2:] if m.name.startswith("./") else m.name
            parts = name.split("/")
            if len(parts) != 2 or not m.isfile():
                continue
            guid, leaf = parts
            if leaf == "pathname" and m.size < 8192:
                fh = tf.extractfile(m)
                raw = fh.read() if fh else b""
                entries.setdefault(guid, {})["pathname"] = raw.decode("utf-8", "replace").split("\n", 1)[0].strip()
            elif leaf == "asset":
                entries.setdefault(guid, {})["size"] = m.size
            elif leaf == "preview.png" and m.size <= MAX_PREVIEW_BLOB:
                fh = tf.extractfile(m)
                entries.setdefault(guid, {})["preview"] = fh.read() if fh else None
    out = []
    for guid, e in entries.items():
        pn = e.get("pathname")
        if not pn or "size" not in e or not useful_member(pn) or not safe_label(pn):
            continue
        out.append({"member": pn, "size": e["size"], "guid": guid, "preview": e.get("preview")})
    return out


class ArchiveCache:
    """One small sqlite per archive, keyed by (relative path, size, mtime)."""

    def __init__(self, root: Path):
        self.root = root
        self.root.mkdir(parents=True, exist_ok=True)

    @staticmethod
    def key_for(rel: str, st: os.stat_result) -> str:
        return hashlib.sha1(("%s|%d|%d" % (rel, st.st_size, int(st.st_mtime))).encode("utf-8")).hexdigest()

    def path(self, key: str) -> Path:
        return self.root / ("%s.sqlite" % key)

    def load(self, key: str) -> Optional[List[dict]]:
        p = self.path(key)
        if not p.is_file():
            return None
        con = sqlite3.connect("file:%s?mode=ro" % p, uri=True)
        try:
            rows = con.execute("SELECT member, size, guid, preview IS NOT NULL FROM entries").fetchall()
        finally:
            con.close()
        return [{"member": r[0], "size": r[1], "guid": r[2], "hasPreview": bool(r[3])} for r in rows]

    def store(self, key: str, entries: List[dict]) -> List[dict]:
        p = self.path(key)
        tmp = p.with_name(p.name + ".tmp")
        if tmp.exists():
            tmp.unlink()
        con = sqlite3.connect(str(tmp))
        try:
            con.execute("CREATE TABLE entries (member TEXT, size INTEGER, guid TEXT, preview BLOB)")
            con.executemany("INSERT INTO entries VALUES (?,?,?,?)",
                            [(e["member"], e["size"], e.get("guid"), e.get("preview")) for e in entries])
            con.execute("CREATE INDEX entries_guid ON entries(guid)")
            con.commit()
        finally:
            con.close()
        os.replace(str(tmp), str(p))
        return [{"member": e["member"], "size": e["size"], "guid": e.get("guid"), "hasPreview": e.get("preview") is not None}
                for e in entries]

    def preview(self, key: str, guid: str) -> Optional[bytes]:
        p = self.path(key)
        if not p.is_file():
            return None
        con = sqlite3.connect("file:%s?mode=ro" % p, uri=True)
        try:
            row = con.execute("SELECT preview FROM entries WHERE guid=? AND preview IS NOT NULL", (guid,)).fetchone()
        finally:
            con.close()
        return bytes(row[0]) if row else None


# -- catalog ----------------------------------------------------------------

SCHEMA = """
CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT);
CREATE TABLE packs (
  id TEXT PRIMARY KEY, name TEXT, family TEXT, available INTEGER, folder TEXT, icon TEXT,
  asset_count INTEGER DEFAULT 0, used_count INTEGER DEFAULT 0);
CREATE TABLE assets (
  id TEXT PRIMARY KEY, name TEXT, lname TEXT, lpath TEXT, pack_id TEXT, pack_name TEXT, family TEXT, kind TEXT,
  path TEXT, fmt TEXT, src_fmt TEXT, archive TEXT, member TEXT, guid TEXT, local_root TEXT, local_rel TEXT,
  size INTEGER, cache_key TEXT, has_preview INTEGER, usages TEXT, used INTEGER DEFAULT 0);
"""
INDEXES = """
CREATE INDEX a_pack ON assets(pack_id, lname);
CREATE INDEX a_lname ON assets(lname);
CREATE INDEX a_guid ON assets(guid);
CREATE INDEX a_path ON assets(path);
CREATE INDEX a_arch ON assets(archive, member);
CREATE INDEX a_used ON assets(used);
CREATE INDEX a_kind ON assets(kind);
CREATE INDEX a_family ON assets(family);
"""

ASSET_COLS = ("id, name, pack_id, pack_name, family, kind, path, fmt, src_fmt, archive, member, guid, "
              "local_root, local_rel, size, cache_key, has_preview, usages, used")


class Catalog:
    def __init__(self, repo_root: Path, library_root: Path, data_dir: Path):
        self.repo_root = Path(repo_root)
        self.library_root = Path(library_root)
        self.data_dir = Path(data_dir)
        self.data_dir.mkdir(parents=True, exist_ok=True)
        self.db_path = self.data_dir / "catalog.sqlite"
        self.cache = ArchiveCache(self.data_dir / "archives")
        self.unity_root = self.repo_root / "unity" / "Bastion"
        self._lock = threading.Lock()
        self._progress: Dict[str, Any] = {"running": False}

    # -- read side ----------------------------------------------------------

    def _connect(self) -> Optional[sqlite3.Connection]:
        if not self.db_path.is_file():
            return None
        con = sqlite3.connect("file:%s?mode=ro" % self.db_path, uri=True, check_same_thread=False)
        con.row_factory = sqlite3.Row
        return con

    def _meta(self, con: sqlite3.Connection) -> Dict[str, str]:
        return {r["key"]: r["value"] for r in con.execute("SELECT key, value FROM meta")}

    def status(self) -> dict:
        with self._lock:
            progress = dict(self._progress)
        con = self._connect()
        if con is None:
            return {"assets": 0, "packs": 0, "used": 0, "indexedAt": None, "indexing": progress.get("running", False),
                    "progress": progress, "issues": ["Aún no hay índice: lanza la reindexación."], "coverage": {}}
        try:
            meta = self._meta(con)
        finally:
            con.close()
        return {
            "assets": int(meta.get("assets", 0)), "packs": int(meta.get("packs", 0)), "used": int(meta.get("used", 0)),
            "indexedAt": meta.get("indexedAt"), "indexing": progress.get("running", False), "progress": progress,
            "issues": json.loads(meta.get("issues", "[]")), "coverage": json.loads(meta.get("coverage", "{}")),
        }

    def packs(self) -> list:
        con = self._connect()
        if con is None:
            return []
        try:
            rows = con.execute("SELECT * FROM packs ORDER BY family, name").fetchall()
        finally:
            con.close()
        return [{
            "id": r["id"], "name": r["name"], "family": r["family"], "available": bool(r["available"]),
            "assetCount": r["asset_count"], "usedCount": r["used_count"],
            "thumbnailUrl": ("/api/packs/%s/thumbnail" % r["id"]) if r["icon"] else None,
        } for r in rows]

    def used_ids(self) -> list:
        con = self._connect()
        if con is None:
            return []
        try:
            return [r[0] for r in con.execute("SELECT id FROM assets WHERE used=1 ORDER BY id")]
        finally:
            con.close()

    def search(self, filters: dict) -> dict:
        filters = filters or {}
        try:
            limit = int(filters.get("limit") or DEFAULT_LIMIT)
            offset = int(filters.get("offset") or 0)
        except (TypeError, ValueError):
            raise ValueError("Límite o desplazamiento no válidos.")
        limit = max(1, min(MAX_LIMIT, limit))
        offset = max(0, offset)
        where: List[str] = []
        args: List[Any] = []
        q = str(filters.get("q") or "").strip().lower()
        for term in q.split()[:8]:
            where.append("lpath LIKE ? ESCAPE '\\'")
            args.append("%" + term.replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_") + "%")
        family = str(filters.get("family") or "").strip()
        if family:
            where.append("family = ? COLLATE NOCASE")
            args.append(family)
        pack = str(filters.get("pack") or "").strip()
        if pack:
            where.append("(pack_id = ? OR pack_name = ?)")
            args.extend([pack, pack])
        kind = str(filters.get("kind") or "").strip()
        if kind:
            where.append("kind = ?")
            args.append(kind)
        state = str(filters.get("state") or "").strip()
        if state not in ("", "used"):
            raise ValueError("Estado no válido.")
        if state == "used":
            where.append("used = 1")
        ids = filters.get("ids")
        if ids is not None:
            ids = [str(i) for i in ids][:100000]
            if not ids:
                return {"items": [], "total": 0, "offset": offset, "limit": limit}
            where.append("id IN (SELECT value FROM json_each(?))")
            args.append(json.dumps(ids))
        con = self._connect()
        if con is None:
            return {"items": [], "total": 0, "offset": offset, "limit": limit}
        try:
            clause = (" WHERE " + " AND ".join(where)) if where else ""
            total = con.execute("SELECT COUNT(*) FROM assets" + clause, args).fetchone()[0]
            rows = con.execute("SELECT %s FROM assets%s ORDER BY used DESC, pack_name, path LIMIT ? OFFSET ?"
                               % (ASSET_COLS, clause), args + [limit, offset]).fetchall()
            icons = self._icons(con)
            equiv = self._equivalent_previews(con, rows)
        finally:
            con.close()
        return {"items": [self._asset_dict(r, icons, equiv) for r in rows], "total": total, "offset": offset, "limit": limit}

    def get_asset(self, asset_id_: str) -> Optional[dict]:
        con = self._connect()
        if con is None:
            return None
        try:
            row = con.execute("SELECT %s FROM assets WHERE id=?" % ASSET_COLS, (str(asset_id_),)).fetchone()
            icons = self._icons(con) if row else {}
            equiv = self._equivalent_previews(con, [row]) if row else {}
        finally:
            con.close()
        return self._asset_dict(row, icons, equiv) if row else None

    @staticmethod
    def _icons(con: sqlite3.Connection) -> Dict[str, bool]:
        return {r[0]: bool(r[1]) for r in con.execute("SELECT id, icon IS NOT NULL AND icon != '' FROM packs")}

    def _media_source(self, row: Any) -> Optional[Tuple[str, str, str]]:
        """Return (where, a, b) for servable bytes: ('zip', archive, member) or ('file', root, rel)."""
        ext = ext_of(row["path"])
        if ext not in MEDIA_MIME or (row["size"] or 0) > MAX_MEDIA:
            return None
        if row["local_rel"]:
            return ("file", row["local_root"], row["local_rel"])
        if row["src_fmt"] == "zip":
            return ("zip", row["archive"], row["member"])
        return None

    @staticmethod
    def _equivalent_previews(con: sqlite3.Connection, rows: List[Any]) -> Dict[str, Tuple[str, str, str]]:
        """Unity preview of the same file for rows that have none (e.g. SourceFiles zip models).

        Same pack, same name and same format, and only one distinct Unity path with a preview
        (several package versions of one path count as the same file). Returns id -> (unity id, key, guid).
        """
        out: Dict[str, Tuple[str, str, str]] = {}
        for r in rows:
            if r["has_preview"] or r["src_fmt"] == "unitypackage" or ("." + (r["fmt"] or "")) not in MODEL_EXT:
                continue
            cands = con.execute("SELECT id, path, cache_key, guid FROM assets WHERE pack_id=? AND lname=? AND fmt=?"
                                " AND src_fmt='unitypackage' AND has_preview=1 AND cache_key IS NOT NULL"
                                " AND guid IS NOT NULL ORDER BY archive DESC LIMIT 8",
                                (r["pack_id"], (r["name"] or "").lower(), r["fmt"])).fetchall()
            if cands and len({c["path"] for c in cands}) == 1:
                out[r["id"]] = (cands[0]["id"], cands[0]["cache_key"], cands[0]["guid"])
        return out

    def _asset_dict(self, row: Any, icons: Dict[str, bool], equiv: Optional[Dict[str, Tuple[str, str, str]]] = None) -> dict:
        aid = row["id"]
        ext = ext_of(row["path"])
        media = self._media_source(row)
        preview = None
        thumb_url, thumb_is_pack = None, False
        if media and ext in IMAGE_EXT:
            preview = {"kind": "image", "url": "/api/assets/%s/media" % aid, "note": None}
            thumb_url = "/api/assets/%s/thumbnail" % aid
        elif media and ext in (".fbx", ".glb"):
            note = "FBX importado en el proyecto de Unity" if row["local_root"] == "repo" else None
            preview = {"kind": ext[1:], "url": "/api/assets/%s/media" % aid, "note": note}
        if thumb_url is None and row["has_preview"]:
            thumb_url = "/api/assets/%s/thumbnail" % aid
        thumb_from = None
        if thumb_url is None and equiv and aid in equiv:
            thumb_url, thumb_from = "/api/assets/%s/thumbnail" % aid, equiv[aid][0]
        if thumb_url is None and icons.get(row["pack_id"]):
            thumb_url, thumb_is_pack = "/api/packs/%s/thumbnail" % row["pack_id"], True
        source = {"archive": row["archive"] or None, "member": row["member"] or None,
                  "localPath": row["local_rel"] or None, "format": row["src_fmt"]}
        if row["guid"]:
            source["guid"] = row["guid"]
        if row["local_rel"]:
            source["localRoot"] = "repo" if row["local_root"] == "repo" else "biblioteca"
            if row["local_root"] == "repo":
                # local_rel is relative to the Unity project; expose it relative to the repo root.
                source["localPath"] = "%s/%s" % (UNITY_PROJECT_REL, row["local_rel"])
        out = {
            "id": aid, "name": row["name"], "packId": row["pack_id"], "packName": row["pack_name"],
            "family": row["family"], "kind": row["kind"], "path": row["path"], "format": row["fmt"],
            "size": row["size"], "source": source, "usages": json.loads(row["usages"] or "[]"),
            "thumbnailUrl": thumb_url, "thumbnailIsPack": thumb_is_pack, "preview": preview, "available": True,
        }
        if thumb_from:
            # The thumbnail is the Unity preview of the same file from the pack's Unity package.
            out["thumbnailFrom"] = thumb_from
        if ext in APPEARANCE_EXT and (row["size"] or 0) <= MAX_MEDIA:
            out["appearanceUrl"] = "/api/assets/%s/appearance" % aid
        return out

    def read_media(self, asset_id_: str, mode: str = "asset") -> Tuple[bytes, str]:
        if mode not in ("asset", "thumbnail"):
            raise ValueError("Modo no válido.")
        con = self._connect()
        if con is None:
            raise KeyError(asset_id_)
        try:
            row = con.execute("SELECT %s FROM assets WHERE id=?" % ASSET_COLS, (str(asset_id_),)).fetchone()
            icons = self._icons(con) if row else {}
        finally:
            con.close()
        if row is None:
            raise KeyError(asset_id_)
        media = self._media_source(row)
        ext = ext_of(row["path"])
        if mode == "asset":
            if media is None:
                raise FileNotFoundError("Este recurso solo existe dentro de su paquete (%s); no se sirve." % (row["src_fmt"],))
            return self._read_source(media), MEDIA_MIME[ext]
        if media and ext in IMAGE_EXT:
            return self._read_source(media), MEDIA_MIME[ext]
        if row["has_preview"] and row["cache_key"] and row["guid"]:
            blob = self.cache.preview(row["cache_key"], row["guid"])
            if blob:
                return blob, "image/png"
        con = self._connect()
        try:
            equiv = self._equivalent_previews(con, [row]) if con is not None else {}
        finally:
            if con is not None:
                con.close()
        if row["id"] in equiv:
            blob = self.cache.preview(equiv[row["id"]][1], equiv[row["id"]][2])
            if blob:
                return blob, "image/png"
        if icons.get(row["pack_id"]):
            return self.read_pack_thumbnail(row["pack_id"])
        raise FileNotFoundError("Este recurso no tiene miniatura.")

    def _read_source(self, media: Tuple[str, str, str]) -> bytes:
        where, a, b = media
        if where == "zip":
            return self._read_zip(a, b)
        root = (self.unity_root if a == "repo" else self.library_root)
        return self._read_file(root, b)

    def _read_file(self, root: Path, rel: str) -> bytes:
        if not safe_label(rel) or ext_of(rel) not in MEDIA_MIME:
            raise ValueError("Ruta no permitida.")
        path = contained(root / rel, root)
        if path is None:
            raise FileNotFoundError("No se encuentra el archivo (o no es un archivo normal).")
        if path.stat().st_size > MAX_MEDIA:
            raise ValueError("El archivo supera 64 MB.")
        with open(str(path), "rb") as fh:
            return fh.read(MAX_MEDIA + 1)

    def _read_zip(self, archive: str, member: str) -> bytes:
        if not safe_label(archive) or not archive.lower().endswith(".zip") or ext_of(member) not in MEDIA_MIME:
            raise ValueError("Ruta no permitida.")
        path = contained(self.library_root / archive, self.library_root)
        if path is None:
            raise FileNotFoundError("No se encuentra el paquete (¿disco desconectado?).")
        with zipfile.ZipFile(str(path)) as zf:
            try:
                info = zf.getinfo(member)
            except KeyError:
                raise FileNotFoundError("El archivo ya no está dentro del paquete.")
            if info.is_dir() or ((info.external_attr >> 16) & 0o170000) == stat.S_IFLNK:
                raise ValueError("Entrada no permitida.")
            if info.file_size > MAX_MEDIA:
                raise ValueError("El archivo supera 64 MB.")
            with zf.open(info) as fh:
                data = fh.read(MAX_MEDIA + 1)
        if len(data) > MAX_MEDIA:
            raise ValueError("El archivo supera 64 MB.")
        return data

    # -- faithful appearance (appearance.py) -------------------------------

    def _appearance(self) -> Any:
        res = getattr(self, "_appearance_resolver", None)
        if res is None:
            from appearance import AppearanceResolver
            with self._lock:
                res = getattr(self, "_appearance_resolver", None)
                if res is None:
                    res = AppearanceResolver(self)
                    self._appearance_resolver = res
        return res

    def appearance(self, asset_id_: str) -> dict:
        """Descriptor of models, materials and textures that make up an asset's real look."""
        return self._appearance().describe(asset_id_)

    def read_file(self, asset_id_: str) -> Tuple[bytes, str]:
        """Exact bytes of an indexed model/texture/material, including package-only ones."""
        return self._appearance().read_file(asset_id_)

    def read_pack_thumbnail(self, pack_id: str) -> Tuple[bytes, str]:
        con = self._connect()
        if con is None:
            raise KeyError(pack_id)
        try:
            row = con.execute("SELECT icon FROM packs WHERE id=?", (str(pack_id),)).fetchone()
        finally:
            con.close()
        if row is None:
            raise KeyError(pack_id)
        if not row[0]:
            raise FileNotFoundError("Este paquete no tiene icono.")
        ext = ext_of(row[0])
        if ext not in IMAGE_EXT:
            raise ValueError("Icono no permitido.")
        return self._read_file(self.library_root, row[0]), MEDIA_MIME[ext]

    # -- write side ---------------------------------------------------------

    def _set_progress(self, **kw: Any) -> None:
        with self._lock:
            self._progress.update(kw)

    def reindex(self) -> dict:
        if not self.library_root.is_dir() or not (self.library_root / "inventory.json").is_file():
            raise FileNotFoundError("No se encuentra la biblioteca en %s (¿disco desconectado?). Se conserva el índice anterior."
                                    % self.library_root)
        tmp = self.db_path.with_name(self.db_path.name + ".tmp")
        started = time.time()
        self._set_progress(running=True, startedAt=iso_now(), phase="paquetes", done=0, total=0, current=None)
        try:
            if tmp.exists():
                tmp.unlink()
            con = sqlite3.connect(str(tmp))
            con.row_factory = sqlite3.Row
            try:
                con.executescript(SCHEMA)
                issues: List[str] = []
                coverage = self._index_library(con, issues)
                con.executescript(INDEXES)
                self._set_progress(phase="usos")
                usage_cov = UsageDetector(self, con, issues).run()
                coverage["usages"] = usage_cov
                con.execute("UPDATE packs SET asset_count=(SELECT COUNT(*) FROM assets WHERE pack_id=packs.id),"
                            " used_count=(SELECT COUNT(*) FROM assets WHERE pack_id=packs.id AND used=1)")
                self._disallowed_issues(con, issues)
                counts = {
                    "assets": con.execute("SELECT COUNT(*) FROM assets").fetchone()[0],
                    "packs": con.execute("SELECT COUNT(*) FROM packs").fetchone()[0],
                    "used": con.execute("SELECT COUNT(*) FROM assets WHERE used=1").fetchone()[0],
                }
                coverage["kinds"] = {r[0]: r[1] for r in con.execute("SELECT kind, COUNT(*) FROM assets GROUP BY kind")}
                coverage["seconds"] = round(time.time() - started, 1)
                cache_keys = coverage.pop("cacheKeys", [])
                meta = dict(counts)
                meta.update(indexedAt=iso_now(), issues=json.dumps(issues, ensure_ascii=False),
                            coverage=json.dumps(coverage, ensure_ascii=False), schema=SCHEMA_VERSION)
                con.executemany("INSERT INTO meta VALUES (?,?)", [(k, str(v)) for k, v in meta.items()])
                con.commit()
            finally:
                con.close()
            os.replace(str(tmp), str(self.db_path))
            self._prune_cache(cache_keys)
            summary = dict(counts, indexedAt=meta["indexedAt"], issues=issues, coverage=coverage)
            atomic_write(self.data_dir / "last-index.json", json.dumps(summary, ensure_ascii=False, indent=1).encode("utf-8"))
            return summary
        except BaseException:
            try:
                if tmp.exists():
                    tmp.unlink()
            except OSError:
                pass
            raise
        finally:
            self._set_progress(running=False, finishedAt=iso_now(), phase=None, current=None)

    @staticmethod
    def _disallowed_issues(con: sqlite3.Connection, issues: List[str]) -> None:
        marks = ",".join("?" * len(DISALLOWED_FAMILIES))
        packs, assets, used = con.execute("SELECT COUNT(*), COALESCE(SUM(asset_count),0), COALESCE(SUM(used_count),0)"
                                          " FROM packs WHERE family IN (%s)" % marks, DISALLOWED_FAMILIES).fetchone()
        if packs:
            issues.append("%d paquetes SIMPLE / POLYGON MINI (%d recursos) se muestran en la biblioteca, pero no están"
                          " permitidos en Bastión (otro estilo)." % (packs, assets))
        if used:
            issues.append("%d recursos de SIMPLE / POLYGON MINI aparecen como usados en el repositorio: revisar." % used)

    def _prune_cache(self, keep: List[str]) -> None:
        keep_set = set(keep)
        for p in self.cache.root.glob("*.sqlite"):
            if p.stem not in keep_set:
                try:
                    p.unlink()
                except OSError:
                    pass

    def _pack_list(self) -> List[Tuple[str, Optional[Path]]]:
        inv = json.loads((self.library_root / "inventory.json").read_text("utf-8"))
        names = [str(n) for n in (inv.get("packs") or [])]
        out = []
        for name in names + [n for n in EXTRA_WEB_PACKS if n not in names] + EXTRA_DIRS:
            folder = None
            for cand in (name, name.replace("/", "-")):
                p = self.library_root / cand
                if "/" not in cand and p.is_dir() and not p.is_symlink():
                    folder = p
                    break
            if name in EXTRA_DIRS and folder is None:
                continue
            out.append((name, folder))
        return out

    def _index_library(self, con: sqlite3.Connection, issues: List[str]) -> dict:
        imported = self._imported_files()
        packs = self._pack_list()
        cov: Dict[str, Any] = {
            "packs": len(packs), "packsAvailable": 0, "packsMissing": [], "unityPackages": 0, "sourceZips": 0,
            "extrasZips": 0, "unrealFallbackZips": 0, "looseFiles": 0, "skippedGodot": 0, "skippedUnreal": 0,
            "archiveErrors": 0, "cacheHits": 0, "cacheKeys": [], "importedLocalFiles": len(imported),
        }
        self._set_progress(total=len(packs))
        seen_ids = set()
        for n, (name, folder) in enumerate(packs):
            pid = slug(name)
            family = family_of(name)
            self._set_progress(done=n, current=name)
            files: List[str] = []
            if folder is not None:
                try:
                    man = json.loads((folder / "manifest.json").read_text("utf-8"))
                    files = [str(f.get("path")) for f in man.get("files") or [] if isinstance(f, dict) and f.get("path")]
                except (OSError, ValueError):
                    issues.append("%s: manifest.json ilegible; se omite el paquete." % name)
            files = [f for f in files if safe_label(f)]
            icons = [f for f in files if f.startswith("Extras/") and ext_of(f) in IMAGE_EXT]
            icon = next((f for f in icons if "_ICON" in f.upper()), icons[0] if icons else None)
            folder_rel = folder.name if folder is not None else None
            con.execute("INSERT INTO packs (id, name, family, available, folder, icon) VALUES (?,?,?,?,?,?)",
                        (pid, name, family, 1 if folder is not None else 0, folder_rel,
                         ("%s/%s" % (folder_rel, icon)) if (icon and folder_rel) else None))
            if folder is None:
                cov["packsMissing"].append(name)
                continue
            cov["packsAvailable"] += 1
            unity = sorted([f for f in files if f.startswith("Unity/") and f.endswith(".unitypackage")], key=version_key)
            source = [f for f in files if f.startswith("SourceFiles/") and f.lower().endswith(".zip")]
            extras_zip = [f for f in files if f.startswith("Extras/") and f.lower().endswith(".zip")]
            unreal_zip = sorted([f for f in files if f.startswith("Unreal/") and f.lower().endswith(".zip")], key=version_key)
            loose = [f for f in files if not f.lower().endswith((".zip", ".unitypackage")) and useful_member(f)
                     and not f.startswith(("Godot/", "Documentacion/")) and f not in icons]
            cov["skippedGodot"] += len([f for f in files if f.startswith("Godot/")])
            plan: List[Tuple[str, str]] = [("unitypackage", f) for f in unity]
            plan += [("zip", f) for f in source + extras_zip]
            if not unity and not source:
                if unreal_zip:
                    plan.append(("zip", unreal_zip[-1]))
                    cov["unrealFallbackZips"] += 1
                    cov["skippedUnreal"] += len(unreal_zip) - 1
            else:
                cov["skippedUnreal"] += len(unreal_zip)
                loose = [f for f in loose if not f.startswith("Unreal/")]
            rows: List[tuple] = []
            for fmt, rel in plan:
                arch_rel = "%s/%s" % (folder_rel, rel)
                self._set_progress(current="%s · %s" % (name, rel))
                entries, key = self._archive_entries(fmt, arch_rel, cov, issues)
                if entries is None:
                    continue
                if fmt == "unitypackage":
                    cov["unityPackages"] += 1
                elif rel.startswith("SourceFiles/"):
                    cov["sourceZips"] += 1
                elif rel.startswith("Extras/"):
                    cov["extrasZips"] += 1
                for e in entries:
                    member = e["member"]
                    aid = asset_id(pid, arch_rel, member)
                    if aid in seen_ids:
                        continue
                    seen_ids.add(aid)
                    local_root, local_rel = None, None
                    if fmt == "unitypackage" and member in imported:
                        local_root, local_rel = "repo", member
                    rows.append(self._row(aid, pid, name, family, member, fmt, arch_rel, member, e.get("guid"),
                                          local_root, local_rel, e["size"], key, e.get("hasPreview")))
            for rel in loose:
                p = contained(folder / rel, self.library_root)
                if p is None:
                    continue
                cov["looseFiles"] += 1
                aid = asset_id(pid, "", "%s/%s" % (folder_rel, rel))
                if aid in seen_ids:
                    continue
                seen_ids.add(aid)
                rows.append(self._row(aid, pid, name, family, rel, "file", None, None, None,
                                      "library", "%s/%s" % (folder_rel, rel), p.stat().st_size, None, False))
            con.executemany("INSERT INTO assets (id, name, lname, lpath, pack_id, pack_name, family, kind, path, fmt,"
                            " src_fmt, archive, member, guid, local_root, local_rel, size, cache_key, has_preview,"
                            " usages, used) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,0)", rows)
            con.commit()
        self._set_progress(done=len(packs), current=None)
        return cov

    @staticmethod
    def _row(aid: str, pid: str, pack: str, family: str, path: str, src_fmt: str, archive: Optional[str],
             member: Optional[str], guid: Optional[str], local_root: Optional[str], local_rel: Optional[str],
             size: int, key: Optional[str], has_preview: Any) -> tuple:
        ext = ext_of(path)
        name = stem_of(path)
        return (aid, name, name.lower(), ("%s %s" % (path, pack)).lower(), pid, pack, family, classify(path, ext, family),
                path, ext[1:], src_fmt, archive, member, guid, local_root, local_rel, int(size or 0), key,
                1 if has_preview else 0, "[]")

    def _archive_entries(self, fmt: str, arch_rel: str, cov: dict, issues: List[str]) -> Tuple[Optional[List[dict]], Optional[str]]:
        path = contained(self.library_root / arch_rel, self.library_root)
        if path is None:
            cov["archiveErrors"] += 1
            issues.append("%s: no es un archivo normal dentro de la biblioteca; se omite." % arch_rel)
            return None, None
        key = ArchiveCache.key_for(arch_rel, path.stat())
        cached = self.cache.load(key)
        if cached is not None:
            cov["cacheHits"] += 1
            cov["cacheKeys"].append(key)
            return cached, key
        try:
            entries = scan_unitypackage(path) if fmt == "unitypackage" else scan_zip(path)
        except (OSError, EOFError, tarfile.TarError, zipfile.BadZipFile, ValueError) as exc:
            cov["archiveErrors"] += 1
            issues.append("%s: no se pudo leer (%s); se omite." % (arch_rel, exc.__class__.__name__))
            return None, None
        out = self.cache.store(key, entries)
        cov["cacheKeys"].append(key)
        return out, key

    def _imported_files(self) -> Dict[str, bool]:
        """Regular files under unity/Bastion/Assets/Synty, keyed by Unity path (Assets/Synty/...)."""
        root = self.unity_root / "Assets" / "Synty"
        out: Dict[str, bool] = {}
        if not root.is_dir():
            return out
        for dirpath, dirnames, filenames in os.walk(str(root)):
            dirnames[:] = [d for d in dirnames if not os.path.islink(os.path.join(dirpath, d))]
            for fn in filenames:
                if fn.endswith(".meta"):
                    continue
                full = os.path.join(dirpath, fn)
                if os.path.islink(full):
                    continue
                rel = os.path.relpath(full, str(self.unity_root)).replace(os.sep, "/")
                out[rel] = True
        return out


# -- usage detection --------------------------------------------------------

class UsageDetector:
    def __init__(self, catalog: Catalog, con: sqlite3.Connection, issues: List[str]):
        self.cat = catalog
        self.con = con
        self.issues = issues
        self.usages: Dict[str, Dict[Tuple[str, str], dict]] = {}
        self.last_alts: List[str] = []
        self.cov: Dict[str, Any] = {}

    def add(self, aid: str, purpose: str, runtime: str, evidence: dict) -> None:
        bucket = self.usages.setdefault(aid, {})
        u = bucket.setdefault((purpose, runtime), {"purpose": purpose, "evidence": [], "runtime": runtime})
        if len(u["evidence"]) < 20 and evidence not in u["evidence"]:
            u["evidence"].append(evidence)

    def run(self) -> dict:
        self.unity_guids()
        self.unity_resource_jsons()
        self.three_sources()
        rows = [(json.dumps(list(b.values()), ensure_ascii=False), aid) for aid, b in self.usages.items()]
        self.con.executemany("UPDATE assets SET usages=?, used=1 WHERE id=?", rows)
        self.cov["assetsUsed"] = len(rows)
        return self.cov

    # Unity: GUID map from Assets/Synty metas, references from Assets/Bastion.
    def unity_guids(self) -> None:
        unity = self.cat.unity_root
        synty, bastion = unity / "Assets" / "Synty", unity / "Assets" / "Bastion"
        guid_path: Dict[str, str] = {}
        if synty.is_dir():
            for dirpath, dirnames, filenames in os.walk(str(synty)):
                for fn in filenames:
                    if not fn.endswith(".meta"):
                        continue
                    target = os.path.join(dirpath, fn[:-5])
                    if not os.path.isfile(target):
                        continue
                    try:
                        with open(os.path.join(dirpath, fn), "r", encoding="utf-8", errors="replace") as fh:
                            m = META_GUID_RE.search(fh.read(2048))
                    except OSError:
                        continue
                    if m:
                        guid_path[m.group(1)] = os.path.relpath(target, str(unity)).replace(os.sep, "/")
        refs: Dict[str, List[dict]] = {}
        files_scanned = 0
        if bastion.is_dir():
            for dirpath, dirnames, filenames in os.walk(str(bastion)):
                for fn in filenames:
                    if ext_of(fn) not in UNITY_REF_EXT:
                        continue
                    full = os.path.join(dirpath, fn)
                    rel = os.path.relpath(full, str(self.cat.repo_root)).replace(os.sep, "/")
                    files_scanned += 1
                    try:
                        with open(full, "r", encoding="utf-8", errors="replace") as fh:
                            for ln, line in enumerate(fh, 1):
                                if "guid" not in line:
                                    continue
                                for g in GUID_YAML_RE.findall(line) + GUID_USS_RE.findall(line):
                                    if g in guid_path:
                                        lst = refs.setdefault(g, [])
                                        if len(lst) < 20:
                                            lst.append({"path": rel, "line": ln, "detail": "guid %s → %s" % (g, guid_path[g])})
                    except OSError:
                        continue
        matched, unresolved = 0, []
        shared = 0
        for g, evs in refs.items():
            upath = guid_path[g]
            rows = self.con.execute("SELECT id, guid, path, pack_id, pack_name, src_fmt FROM assets"
                                    " WHERE src_fmt='unitypackage' AND (path=? OR guid=?)", (upath, g)).fetchall()
            both = [r for r in rows if r["guid"] == g and r["path"] == upath]
            by_path = [r for r in rows if r["path"] == upath]
            by_guid = [r for r in rows if r["guid"] == g]
            aid, alts = canonical(both or by_path or by_guid)
            if not aid:
                unresolved.append(upath)
                continue
            matched += 1
            shared += 1 if alts else 0
            for ev in evs:
                self.add(aid, "Referencia GUID en Unity", "Unity", with_alts(ev, alts))
        self.cov["unityMetaGuids"] = len(guid_path)
        self.cov["unityFilesScanned"] = files_scanned
        self.cov["unityGuidRefsToSynty"] = len(refs)
        self.cov["unityGuidRefsMatched"] = matched
        self.cov["unityGuidRefsUnresolved"] = len(unresolved)
        self.cov["unityGuidRefsSharedByPacks"] = shared
        if unresolved:
            self.issues.append("%d referencias GUID a Assets/Synty no están en ningún .unitypackage indexado (p. ej. %s)."
                               % (len(unresolved), ", ".join(sorted(unresolved)[:3])))

    def _line_of(self, lines: List[str], needle: str) -> int:
        for i, line in enumerate(lines, 1):
            if needle in line:
                return i
        return 1


    def _match_pack_fbx(self, pack_name: str, ref: str) -> Tuple[Optional[str], int]:
        pid = slug(pack_name)
        stem = ref.rsplit("/", 1)[-1].lower()
        suffix = "/" + ref.lower() + ".fbx"
        cur = self.con.execute("SELECT id, path, pack_id, pack_name, src_fmt FROM assets"
                               " WHERE pack_id=? AND lname=? AND fmt='fbx'", (pid, stem))
        rows = [r for r in cur.fetchall() if ("/" + r["path"].lower()).endswith(suffix)]
        aid, alts = canonical(rows)
        return aid, len(rows)

    def _match_sidekick_part(self, part: str) -> Tuple[Optional[str], int]:
        cur = self.con.execute("SELECT id, path, pack_id, pack_name, src_fmt FROM assets"
                               " WHERE family='SIDEKICK' AND lname=? AND fmt='fbx'", (part.lower(),))
        rows = cur.fetchall()
        aid, alts = canonical(rows)
        self.last_alts = alts
        return aid, len(rows)

    def unity_resource_jsons(self) -> None:
        res = self.cat.unity_root / "Assets" / "Bastion" / "Client" / "Resources"
        stats = {"refs": 0, "matched": 0, "ambiguous": 0, "notFound": 0, "nonSynty": 0, "unknownPack": 0}
        unknown: Dict[str, int] = {}
        for base in RESOURCE_JSONS:
            path = res / ("%s.json" % base)
            if not path.is_file():
                continue
            text = path.read_text("utf-8", errors="replace")
            try:
                data = json.loads(text)
            except ValueError:
                self.issues.append("%s.json no es JSON válido; se omite." % base)
                continue
            lines = text.splitlines()
            rel = os.path.relpath(str(path), str(self.cat.repo_root)).replace(os.sep, "/")
            for keypath, node in walk_json(data):
                if not isinstance(node, dict):
                    continue
                fbx, pack = node.get("fbx"), node.get("pack")
                if isinstance(fbx, str) and isinstance(pack, str):
                    stats["refs"] += 1
                    if pack in NON_SYNTY_ALIASES:
                        stats["nonSynty"] += 1
                        continue
                    pname = PACK_ALIASES.get(pack)
                    if not pname:
                        stats["unknownPack"] += 1
                        unknown[pack] = unknown.get(pack, 0) + 1
                        continue
                    aid, n = self._match_pack_fbx(pname, fbx)
                    if aid:
                        stats["matched"] += 1
                        self.add(aid, "Catálogo %s.json (Unity)" % base, "Unity",
                                 {"path": rel, "line": self._line_of(lines, json.dumps(fbx)),
                                  "detail": "%s → %s (%s)" % (keypath, fbx, pack)})
                    else:
                        stats["ambiguous" if n > 1 else "notFound"] += 1
                parts = node.get("parts")
                if isinstance(parts, list):
                    for part in parts:
                        if not (isinstance(part, str) and part.startswith("SK_")):
                            continue
                        stats["refs"] += 1
                        aid, n = self._match_sidekick_part(part)
                        if aid:
                            stats["matched"] += 1
                            self.add(aid, "Pieza Sidekick en %s.json (Unity)" % base, "Unity",
                                     with_alts({"path": rel, "line": self._line_of(lines, json.dumps(part)),
                                                "detail": "%s → %s" % (keypath, part)}, self.last_alts))
                        else:
                            stats["ambiguous" if n > 1 else "notFound"] += 1
        self.cov["unityJson"] = stats
        if unknown:
            self.issues.append("Alias de paquete sin equivalencia segura en los JSON de Unity: %s."
                               % ", ".join("%s (%d)" % kv for kv in sorted(unknown.items())))
        if stats["ambiguous"] or stats["notFound"]:
            self.issues.append("JSON de Unity: %d referencias ambiguas y %d sin encontrar (no se marcan como usadas)."
                               % (stats["ambiguous"], stats["notFound"]))

    def three_sources(self) -> None:
        path = self.cat.repo_root / "packages" / "client" / "public" / "models" / "synty" / "sources.json"
        stats = {"refs": 0, "matched": 0, "ambiguous": 0, "notFound": 0}
        if not path.is_file():
            self.cov["threeSources"] = stats
            return
        text = path.read_text("utf-8", errors="replace")
        lines = text.splitlines()
        rel = os.path.relpath(str(path), str(self.cat.repo_root)).replace(os.sep, "/")
        try:
            data = json.loads(text)
        except ValueError:
            self.issues.append("sources.json no es JSON válido; se omite.")
            return
        archives: Dict[str, List[str]] = {}
        for (arch,) in self.con.execute("SELECT DISTINCT archive FROM assets WHERE src_fmt='zip'"):
            archives.setdefault(stem_of(arch).lower(), []).append(arch)
        for glb, raw in (data.items() if isinstance(data, dict) else []):
            try:
                entry = json.loads(raw) if isinstance(raw, str) else raw
            except ValueError:
                continue
            if not isinstance(entry, dict):
                continue
            line = self._line_of(lines, json.dumps(glb))
            for field in ("fbx", "src", "atlasImage"):
                ref = entry.get(field)
                if not isinstance(ref, str) or not ref.startswith("/"):
                    continue
                stats["refs"] += 1
                self.last_alts = []
                aid, n = self._match_source_path(ref, archives)
                if aid:
                    stats["matched"] += 1
                    self.add(aid, "Fuente de conversión a GLB (Three.js)", "Three.js",
                             with_alts({"path": rel, "line": line, "detail": "%s ← %s" % (glb, ref)}, self.last_alts))
                else:
                    stats["ambiguous" if n > 1 else "notFound"] += 1
            parts = entry.get("parts")
            if isinstance(parts, list):
                for part in parts:
                    if isinstance(part, str) and part.startswith("SK_"):
                        stats["refs"] += 1
                        aid, n = self._match_sidekick_part(part)
                        if aid:
                            stats["matched"] += 1
                            self.add(aid, "Pieza Sidekick convertida (Three.js)", "Three.js",
                                     with_alts({"path": rel, "line": line, "detail": "%s ← %s" % (glb, part)},
                                               self.last_alts))
                        else:
                            stats["ambiguous" if n > 1 else "notFound"] += 1
        self.cov["threeSources"] = stats

    def _match_source_path(self, ref: str, archives: Dict[str, List[str]]) -> Tuple[Optional[str], int]:
        folder, _, rest = ref.lstrip("/").partition("/")
        if not rest:
            return None, 0
        if folder.endswith("_Extracted"):
            rows = self.con.execute("SELECT id, path, pack_id, pack_name, archive, src_fmt FROM assets"
                                    " WHERE src_fmt='unitypackage' AND path=?", (rest,)).fetchall()
            hint = archive_key(folder[:-len("_Extracted")])
            hinted = [r for r in rows if archive_key(stem_of(r["archive"])).startswith(hint)]
            aid, alts = canonical(hinted or rows)
            self.last_alts = alts
            return aid, len(rows)
        cands = archives.get(folder.lower())
        if not cands:
            key = archive_key(folder)
            cands = [a for k, lst in archives.items() if archive_key(k) == key for a in lst]
        if len(cands) != 1:
            return None, len(cands)
        rows = self.con.execute("SELECT id, member FROM assets WHERE archive=? AND (member=? OR member LIKE ?)",
                                (cands[0], rest, "%/" + rest)).fetchall()
        exact = [r[0] for r in rows if r[1] == rest]
        ids = exact or [r[0] for r in rows]
        return (ids[0] if len(ids) == 1 else None), len(ids)


ARCHIVE_VERSION_RE = re.compile(r"_v\d+(?:_\d+)*$")


def archive_key(stem: str) -> str:
    """Loose key for source archive names: drop version suffix, case and separators."""
    return re.sub(r"[^a-z0-9]", "", ARCHIVE_VERSION_RE.sub("", stem.lower()))


def canonical(rows: List[Any]) -> Tuple[Optional[str], List[str]]:
    """Pick one asset among rows describing the same file.

    Unity package copies win over source archives. If the remaining rows share a
    single path (identical member shipped by several packs, e.g. Sidekick base
    meshes), one canonical pack is chosen deterministically (SIDEKICK - Starter
    first, then alphabetical) and the other packs are returned as alternatives.
    Different paths mean real ambiguity: nothing is chosen.
    """
    if not rows:
        return None, []
    unity = [r for r in rows if r["src_fmt"] == "unitypackage"]
    pool = unity or rows
    if len(pool) == 1:
        return pool[0]["id"], []
    not_unreal = [r for r in pool if "unreal" not in r["path"].lower()]
    if len(not_unreal) == 1:
        return not_unreal[0]["id"], []
    if len({r["path"] for r in pool}) != 1:
        return None, []
    pool = sorted(pool, key=lambda r: (r["pack_id"] != "sidekick-starter", r["pack_id"]))
    return pool[0]["id"], sorted({r["pack_name"] for r in pool[1:]})


def with_alts(evidence: dict, alts: List[str]) -> dict:
    if not alts:
        return evidence
    ev = dict(evidence)
    ev["detail"] = "%s · el mismo archivo está también en %d paquetes más (%s); se marca solo uno" % (
        ev.get("detail") or "", len(alts), ", ".join(alts[:3]) + ("…" if len(alts) > 3 else ""))
    return ev


def walk_json(node: Any, path: str = "") -> Iterable[Tuple[str, Any]]:
    yield path, node
    if isinstance(node, dict):
        for k, v in node.items():
            yield from walk_json(v, "%s.%s" % (path, k) if path else str(k))
    elif isinstance(node, list):
        for i, v in enumerate(node):
            yield from walk_json(v, "%s[%d]" % (path, i))


# -- CLI --------------------------------------------------------------------

def main(argv: Optional[List[str]] = None) -> int:
    repo_default = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description="Indexador de la biblioteca Synty")
    parser.add_argument("--index", action="store_true", help="reindexa y guarda el resumen JSON")
    parser.add_argument("--repo-root", type=Path, default=repo_default)
    parser.add_argument("--library-root", type=Path, default=DEFAULT_LIBRARY)
    parser.add_argument("--data-dir", type=Path, default=None)
    parser.add_argument("--log", type=Path, default=None, help="ruta extra para el resumen JSON")
    args = parser.parse_args(argv)
    data_dir = args.data_dir or (args.repo_root / "data" / "asset-library")
    cat = Catalog(args.repo_root, args.library_root, data_dir)
    if not args.index:
        print(json.dumps(cat.status(), ensure_ascii=False, indent=1))
        return 0
    stop = threading.Event()

    def ticker() -> None:
        while not stop.wait(15):
            p = cat._progress
            sys.stderr.write("[%s] %s %s/%s %s\n" % (time.strftime("%H:%M:%S"), p.get("phase"), p.get("done"),
                                                       p.get("total"), p.get("current")))
            sys.stderr.flush()

    threading.Thread(target=ticker, daemon=True).start()
    try:
        summary = cat.reindex()
    except Exception as exc:
        stop.set()
        sys.stderr.write("La reindexación falló: %r. Se conserva el índice anterior.\n" % (exc,))
        return 1
    stop.set()
    text = json.dumps(summary, ensure_ascii=False, indent=1)
    if args.log:
        args.log.write_text(text, "utf-8")
    print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
