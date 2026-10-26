"""Synthetic regressions for appearance.py: Unity-only prefabs, single-read tar streaming, byte limits,
file/link safety, material fidelity and descriptor cache invalidation (stdlib unittest).

Run from the repo root:
    python3 -B -m unittest discover -s tools/asset-library/tests -v

Every fixture lives in a temp sandbox; the Synty disk, docs/assets and data/asset-library are never touched.
"""

from __future__ import annotations

import http.client
import io
import json
import os
import shutil
import sqlite3
import stat
import sys
import tarfile
import tempfile
import threading
import time
import unittest
import zipfile
from pathlib import Path
from unittest import mock

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))

import appearance  # noqa: E402
import catalog  # noqa: E402

SCRATCH = Path(os.environ.get("BASTION_TEST_TMP", "/Users/yume/.hermes/profiles/bastion/cache/scratch"))
if SCRATCH.is_dir():
    tempfile.tempdir = str(SCRATCH)

G_PREFAB, G_MAT, G_FBX, G_TEX = "a" * 32, "b" * 32, "c" * 32, "d" * 32
G_DETAIL, G_SHADER, G_BIG, G_LINK = "e" * 32, "f" * 32, "1" * 32, "2" * 32
BUILTIN = "0000000000000000f000000000000000"

FBX = (b'; FBX 7.4.0 project file\nFBXHeaderExtension:  {\n}\nObjects:  {\n'
       b'\tGeometry: 10, "Geometry::Cube", "Mesh" {\n\t}\n\tModel: 20, "Model::Cube", "Mesh" {\n\t}\n'
       b'\tMaterial: 30, "Material::lambert1", "" {\n\t}\n}\nConnections:  {\n'
       b'\tC: "OO",10,20\n\tC: "OO",30,20\n}\n')
FBX_META = ("fileFormatVersion: 2\nguid: %s\nModelImporter:\n  fileIDToRecycleName:\n    4300000: Cube\n"
            "  materials:\n    materialImportMode: 0\n" % G_FBX).encode()
PNG = b"\x89PNG\r\n\x1a\n" + b"\x00" * 40


def mat_text(name, tex=None, shader=BUILTIN, extra=None):
    envs = ""
    for key, guid in [("_BaseMap", tex)] + list((extra or {}).items()):
        ref = "{fileID: 2800000, guid: %s, type: 3}" % guid if guid else "{fileID: 0}"
        envs += "    - %s:\n        m_Texture: %s\n        m_Scale: {x: 1, y: 1}\n" % (key, ref)
    return ("%%YAML 1.1\n--- !u!21 &2100000\nMaterial:\n  m_Name: %s\n  m_Shader: {fileID: 4800000, guid: %s, type: 3}\n"
            "  m_SavedProperties:\n    m_TexEnvs:\n%s    m_Colors:\n    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}\n"
            % (name, shader, envs)).encode()


PREFAB = ("%%YAML 1.1\n--- !u!1 &100\nGameObject:\n  m_Name: Cube\n  m_IsActive: 1\n"
          "--- !u!4 &101\nTransform:\n  m_GameObject: {fileID: 100}\n  m_Father: {fileID: 0}\n"
          "--- !u!33 &102\nMeshFilter:\n  m_GameObject: {fileID: 100}\n  m_Mesh: {fileID: 4300000, guid: %s, type: 3}\n"
          "--- !u!23 &103\nMeshRenderer:\n  m_GameObject: {fileID: 100}\n  m_Enabled: 1\n  m_Materials:\n"
          "  - {fileID: 2100000, guid: %s, type: 2}\n" % (G_FBX, G_MAT)).encode()


def tar_add(tf, name, data, kind=tarfile.REGTYPE, link=""):
    ti = tarfile.TarInfo(name)
    ti.type = kind
    if kind == tarfile.REGTYPE:
        ti.size = len(data)
        tf.addfile(ti, io.BytesIO(data))
    else:
        ti.linkname = link
        tf.addfile(ti)


class Box:
    """Sandbox: library with one .unitypackage and one zip, a repo Unity copy and a catalog.sqlite."""

    def __init__(self):
        self.root = Path(tempfile.mkdtemp(prefix="appearance-test-"))
        self.lib, self.repo, self.data = self.root / "lib", self.root / "repo", self.root / "data"
        self.unity = self.repo / "unity" / "Bastion"
        for d in (self.lib / "Pack", self.unity / "Assets" / "Synty", self.data):
            d.mkdir(parents=True)
        self.outside = self.root / "outside.txt"
        self.outside.write_bytes(b"SECRET-OUTSIDE")
        self.pkg = "Pack/Town.unitypackage"
        self.write_package()
        zp = self.lib / "Pack" / "Tex.zip"
        with zipfile.ZipFile(str(zp), "w") as zf:
            zf.writestr("Tex/plain.png", PNG)  # no Unix mode (external_attr 0): accepted
            reg = zipfile.ZipInfo("Tex/reg.png")
            reg.external_attr = (stat.S_IFREG | 0o644) << 16
            zf.writestr(reg, PNG)
            lnk = zipfile.ZipInfo("Tex/link.png")
            lnk.external_attr = (stat.S_IFLNK | 0o777) << 16
            zf.writestr(lnk, "/etc/passwd")
            fifo = zipfile.ZipInfo("Tex/fifo.png")
            fifo.external_attr = (stat.S_IFIFO | 0o644) << 16
            zf.writestr(fifo, PNG)
        local_mat = self.unity / "Assets" / "Synty" / "Local.mat"
        local_mat.write_bytes(mat_text("Local", G_TEX))
        (self.unity / "Assets" / "Synty" / "Big.png").write_bytes(PNG * 4)
        os.symlink(str(self.outside), str(self.unity / "Assets" / "Synty" / "Link.png"))
        self.cat = catalog.Catalog(self.repo, self.lib, self.data)
        self.build_db()

    def write_package(self, extra_member=None):
        path = self.lib / self.pkg
        with tarfile.open(str(path), "w:gz") as tf:
            for guid, name, data in ((G_PREFAB, "Cube.prefab", PREFAB), (G_MAT, "Mat1.mat", mat_text("Mat1", G_TEX)),
                                     (G_FBX, "Cube.fbx", FBX), (G_TEX, "Tex.png", PNG),
                                     (G_DETAIL, "Detail.png", PNG), (G_SHADER, "Toon.shadergraph", b"{}"),
                                     (G_BIG, "Big.png", PNG * 4)):
                tar_add(tf, "%s/pathname" % guid, ("Assets/Pack/" + name).encode())
                tar_add(tf, "%s/asset" % guid, data)
                if name.endswith(".fbx"):
                    tar_add(tf, "%s/asset.meta" % guid, FBX_META)
            tar_add(tf, "%s/pathname" % G_LINK, b"Assets/Pack/Link.png")
            tar_add(tf, "%s/asset" % G_LINK, b"", tarfile.SYMTYPE, "/etc/passwd")

    def key(self):
        return catalog.ArchiveCache.key_for(self.pkg, (self.lib / self.pkg).stat())

    def build_db(self):
        db = self.data / "catalog.sqlite"
        tmp = db.with_name("catalog.sqlite.tmp")
        if tmp.exists():
            tmp.unlink()
        con = sqlite3.connect(str(tmp))
        con.executescript(catalog.SCHEMA + catalog.INDEXES)
        con.execute("INSERT INTO packs VALUES ('town','POLYGON Town','POLYGON',1,'Pack','',0,0)")
        key = self.key()
        self.ids = {}

        def add(tag, path, src, archive, member, guid, root=None, rel=None, size=None):
            aid = "id-%s" % tag
            self.ids[tag] = aid
            name = catalog.stem_of(path)
            ext = catalog.ext_of(path)
            con.execute("INSERT INTO assets VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                        (aid, name, name.lower(), path.lower(), "town", "POLYGON Town", "POLYGON", "model", path, ext[1:],
                         src, archive, member, guid, root, rel, size if size is not None else 64,
                         key if src == "unitypackage" else None, 0, "[]", 0))

        u = "unitypackage"
        add("prefab", "Assets/Pack/Cube.prefab", u, self.pkg, "Assets/Pack/Cube.prefab", G_PREFAB)
        add("mat", "Assets/Pack/Mat1.mat", u, self.pkg, "Assets/Pack/Mat1.mat", G_MAT)
        add("fbx", "Assets/Pack/Cube.fbx", u, self.pkg, "Assets/Pack/Cube.fbx", G_FBX)
        add("tex", "Assets/Pack/Tex.png", u, self.pkg, "Assets/Pack/Tex.png", G_TEX, size=len(PNG))
        add("detail", "Assets/Pack/Detail.png", u, self.pkg, "Assets/Pack/Detail.png", G_DETAIL)
        add("shader", "Assets/Pack/Toon.shadergraph", u, self.pkg, "Assets/Pack/Toon.shadergraph", G_SHADER)
        add("big", "Assets/Pack/Big.png", u, self.pkg, "Assets/Pack/Big.png", G_BIG, size=len(PNG) * 4)
        add("tarlink", "Assets/Pack/Link.png", u, self.pkg, "Assets/Pack/Link.png", G_LINK)
        add("zplain", "Tex/plain.png", "zip", "Pack/Tex.zip", "Tex/plain.png", None)
        add("zreg", "Tex/reg.png", "zip", "Pack/Tex.zip", "Tex/reg.png", None)
        add("zlink", "Tex/link.png", "zip", "Pack/Tex.zip", "Tex/link.png", None)
        add("zfifo", "Tex/fifo.png", "zip", "Pack/Tex.zip", "Tex/fifo.png", None)
        add("local", "Assets/Synty/Local.mat", "repo", "", "", "9" * 32, "repo", "Assets/Synty/Local.mat")
        add("localbig", "Assets/Synty/Big.png", "repo", "", "", None, "repo", "Assets/Synty/Big.png")
        add("locallink", "Assets/Synty/Link.png", "repo", "", "", None, "repo", "Assets/Synty/Link.png")
        add("escape", "x.png", "repo", "", "", None, "repo", "../../../outside.txt")
        add("script", "Assets/Pack/Code.cs", u, self.pkg, "Assets/Pack/Code.cs", "3" * 32)
        con.commit()
        con.close()
        os.replace(str(tmp), str(db))

    def cleanup(self):
        shutil.rmtree(str(self.root), ignore_errors=True)


class AppearanceTest(unittest.TestCase):
    def setUp(self):
        self.box = Box()
        self.res = appearance.AppearanceResolver(self.box.cat)
        self.ids = self.box.ids
        self.log = mock.patch.object(appearance.AppearanceResolver, "_log", staticmethod(lambda msg: None))
        self.log.start()

    def tearDown(self):
        self.log.stop()
        self.box.cleanup()

    def row(self, tag):
        return self.res.row(self.ids[tag])

    # -- Unity-only prefab and single-read streaming --------------------------------------------------

    def test_unity_only_prefab_resolves_from_one_stream_without_repo(self):
        desc = self.res.describe(self.ids["prefab"])
        self.assertEqual(desc["model"]["id"], self.ids["fbx"])
        self.assertEqual(desc["model"]["source"], "unitypackage")
        self.assertEqual(desc["fidelity"], "resolved", desc["warnings"])
        mat = desc["materials"][0]
        self.assertEqual((mat["unityMaterial"], mat["meshes"], mat["slot"]), ("Mat1", ["Cube"], 0))
        self.assertEqual(mat["map"], "/api/assets/%s/file" % self.ids["tex"])
        self.assertEqual([t["id"] for t in desc["textures"]], [self.ids["tex"]])
        # prefetch copied the texture blob: reading it now needs no new pass over the package
        with mock.patch.object(self.res, "_stream", side_effect=AssertionError("unexpected pass")):
            self.assertEqual(self.res.read_bytes(self.row("tex")), PNG)

    def test_member_wanted_as_blob_and_sidecar_text_is_read_once(self):
        # Building the sidecar for a .prefab/.mat also caches its own blob: the same streamed member feeds both.
        for tag, data in (("prefab", PREFAB), ("mat", mat_text("Mat1", G_TEX))):
            row = self.row(tag)
            self.assertEqual(self.res.text(row).encode(), data)
            self.assertTrue(self.res._blob_path(row).is_file(), tag)
            with mock.patch.object(self.res, "_stream", side_effect=AssertionError("unexpected pass")):
                self.assertEqual(self.res.read_bytes(row), data)
            shutil.rmtree(str(self.res.side_dir))
            self.res.side_dir.mkdir()
            for p in self.res.blob_dir.glob("*.bin"):
                p.unlink()

    def test_stream_reads_each_member_at_most_once(self):
        counts = {}
        orig = tarfile.TarFile.extractfile

        def counting(tf, member):
            counts[member.name] = counts.get(member.name, 0) + 1
            return orig(tf, member)

        with mock.patch.object(tarfile.TarFile, "extractfile", counting):
            self.res.describe(self.ids["prefab"])
        self.assertTrue(counts)
        self.assertEqual({k: v for k, v in counts.items() if v > 1}, {})

    def test_stream_skips_links_and_oversized_members(self):
        with self.assertRaises(FileNotFoundError):
            self.res.read_bytes(self.row("tarlink"))
        with mock.patch.object(appearance, "MAX_MEDIA", len(PNG) * 2):
            with self.assertRaises(FileNotFoundError):
                self.res.read_bytes(self.row("big"))
        self.assertEqual(self.res.read_bytes(self.row("big")), PNG * 4)

    def test_stream_refuses_package_changed_since_index(self):
        row = self.row("tex")
        later = time.time() + 120
        os.utime(str(self.box.lib / self.box.pkg), (later, later))
        with self.assertRaises(FileNotFoundError):
            self.res.read_bytes(row)

    def touch_package(self):
        later = time.time() + 120
        os.utime(str(self.box.lib / self.box.pkg), (later, later))

    def test_warm_blob_refused_after_package_changed(self):
        # A cached blob is keyed by the indexed cache_key: a replaced package must not be served from it.
        row = self.row("tex")
        self.assertEqual(self.res.read_bytes(row), PNG)
        self.assertTrue(self.res._blob_path(row).is_file())
        self.touch_package()
        with mock.patch.object(self.res, "_stream", side_effect=AssertionError("unexpected pass")):
            with self.assertRaises(FileNotFoundError):
                self.res.read_bytes(row)
            with self.assertRaises(FileNotFoundError):
                self.res._tar_assets(row["archive"], row["cache_key"], {row["guid"]: row})

    def test_warm_sidecar_refused_after_package_changed(self):
        row = self.row("mat")
        self.assertEqual(self.res.text(row).encode(), mat_text("Mat1", G_TEX))
        self.assertTrue((self.res.side_dir / ("%s.sqlite" % row["cache_key"])).is_file())
        self.touch_package()
        with mock.patch.object(self.res, "_stream", side_effect=AssertionError("unexpected pass")):
            with self.assertRaises(FileNotFoundError):
                self.res._sidecar(row)
            with self.assertRaises(FileNotFoundError):
                self.res.text(row)

    def test_zip_read_is_a_descriptor_dependency(self):
        zp = self.box.lib / "Pack" / "Tex.zip"
        self.res._deps.paths = set()
        try:
            self.assertEqual(self.res.read_bytes(self.row("zplain")), PNG)
            self.assertIn(str(zp), self.res._deps.paths)
        finally:
            self.res._deps.paths = None
        # A memoized descriptor whose build read zip bytes is rebuilt once the zip changes.
        aid, zrow = self.ids["zplain"], self.row("zplain")
        builds = []
        orig = appearance._Builder.build

        def build(b):
            builds.append(1)
            self.res.read_bytes(zrow)
            return orig(b)

        with mock.patch.object(appearance._Builder, "build", build):
            self.res.describe(aid)
            self.assertIn(str(zp), [p for p, _, _ in self.res._memo[aid][1]])
            self.res.describe(aid)
            self.assertEqual(builds, [1])
            later = time.time() + 120
            os.utime(str(zp), (later, later))
            self.res.describe(aid)
            self.assertEqual(builds, [1, 1])

    # -- byte limits and file safety ----------------------------------------------------------------

    def test_custom_limit_applies_to_cached_blob(self):
        row = self.row("big")
        self.assertEqual(self.res.read_bytes(row), PNG * 4)
        self.assertTrue(self.res._blob_path(row).is_file())
        big = dict(row, size=0)  # catalog size must not be the only guard
        with self.assertRaises(ValueError):
            self.res.read_bytes(big, limit=len(PNG))
        with self.assertRaises(ValueError):
            self.res.read_bytes(big, limit=len(PNG) * 4 - 1)
        self.assertEqual(self.res.read_bytes(big, limit=len(PNG) * 4), PNG * 4)

    def test_symlinked_blob_is_ignored(self):
        row = self.row("tex")
        blob = self.res._blob_path(row)
        os.symlink(str(self.box.outside), str(blob))
        self.assertEqual(self.res.read_bytes(row), PNG)
        self.assertFalse(blob.is_symlink())
        self.assertEqual(self.box.outside.read_bytes(), b"SECRET-OUTSIDE")

    def test_local_limits_links_and_escape(self):
        with self.assertRaises(ValueError):
            self.res.read_bytes(self.row("localbig"), limit=len(PNG))
        self.assertEqual(self.res.read_bytes(self.row("localbig")), PNG * 4)
        for tag in ("locallink", "escape"):
            with self.assertRaises(FileNotFoundError, msg=tag):
                self.res.read_bytes(self.row(tag))

    def test_local_file_growing_during_read_is_an_error_not_truncation(self):
        real = os.fstat

        def shrunk(fd):
            st = real(fd)
            return os.stat_result((st.st_mode, st.st_ino, st.st_dev, st.st_nlink, st.st_uid, st.st_gid, 1,
                                   int(st.st_atime), int(st.st_mtime), int(st.st_ctime)))

        row = dict(self.row("localbig"), size=0)  # index and fstat both saw a small file; it then grew
        with mock.patch.object(appearance.os, "fstat", shrunk):
            with self.assertRaises(ValueError):
                self.res.read_bytes(row, limit=len(PNG))

    def test_zip_members_only_regular(self):
        self.assertEqual(self.res.read_bytes(self.row("zplain")), PNG)
        self.assertEqual(self.res.read_bytes(self.row("zreg")), PNG)
        for tag in ("zlink", "zfifo"):
            with self.assertRaises(ValueError, msg=tag):
                self.res.read_bytes(self.row(tag))
        with self.assertRaises(ValueError):
            self.res.read_bytes(self.row("zreg"), limit=len(PNG) - 1)

    def test_read_file_by_id_only_serves_known_types(self):
        data, mime = self.res.read_file(self.ids["tex"])
        self.assertEqual((data, mime), (PNG, "image/png"))
        with self.assertRaises(ValueError):
            self.res.read_file(self.ids["script"])
        with self.assertRaises(KeyError):
            self.res.read_file("../../outside.txt")

    # -- material fidelity ----------------------------------------------------------------------------

    def describe_local(self, text):
        (self.box.unity / "Assets" / "Synty" / "Local.mat").write_bytes(text)
        self.res._memo.clear()
        return self.res.describe(self.ids["local"])

    def test_plain_material_is_resolved(self):
        desc = self.describe_local(mat_text("Local", G_TEX))
        self.assertEqual(desc["fidelity"], "resolved", desc["warnings"])

    def test_extra_maps_lower_fidelity_even_when_found(self):
        desc = self.describe_local(mat_text("Local", G_TEX, extra={"_DetailAlbedoMap": G_DETAIL}))
        self.assertEqual(desc["fidelity"], "partial")
        self.assertIn("_DetailAlbedoMap", desc["material"]["extraMaps"])
        self.assertTrue(any("_DetailAlbedoMap" in w for w in desc["warnings"]))
        self.assertFalse(any(w.startswith("!") for w in desc["warnings"]))

    def test_extra_map_missing_from_catalog_lowers_fidelity(self):
        desc = self.describe_local(mat_text("Local", G_TEX, extra={"_MaskMap": "4" * 32}))
        self.assertEqual(desc["fidelity"], "partial")
        self.assertNotIn("extraMaps", desc["material"])

    def test_pack_shader_is_not_resolved(self):
        desc = self.describe_local(mat_text("Local", G_TEX, shader=G_SHADER))
        self.assertEqual(desc["material"]["shader"], "Toon")
        self.assertEqual(desc["fidelity"], "partial")

    def test_material_without_base_lowers_fidelity(self):
        text = mat_text("Local", None).replace(b"    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}\n", b"")
        self.assertEqual(self.describe_local(text)["fidelity"], "partial")

    # -- descriptor cache -----------------------------------------------------------------------------

    def test_memo_invalidated_when_material_file_changes(self):
        path = self.box.unity / "Assets" / "Synty" / "Local.mat"
        self.assertEqual(self.res.describe(self.ids["local"])["fidelity"], "resolved")
        self.assertEqual(self.res.describe(self.ids["local"])["fidelity"], "resolved")  # memo hit
        path.write_bytes(mat_text("Local", G_TEX, extra={"_DetailAlbedoMap": G_DETAIL}))
        st = path.stat()
        os.utime(str(path), ns=(st.st_atime_ns, st.st_mtime_ns + 5_000_000_000))
        self.assertEqual(self.res.describe(self.ids["local"])["fidelity"], "partial")

    def test_memo_hit_does_not_rebuild_and_returns_copy(self):
        first = self.res.describe(self.ids["prefab"])
        first["materials"].clear()
        with mock.patch.object(appearance, "_Builder", side_effect=AssertionError("rebuilt")):
            again = self.res.describe(self.ids["prefab"])
        self.assertEqual(len(again["materials"]), 1)

    def test_memo_dependencies_cover_package(self):
        self.res.describe(self.ids["prefab"])
        _, deps = self.res._memo[self.ids["prefab"]]
        self.assertIn(str(self.box.lib / self.box.pkg), [p for p, _, _ in deps])


class AppearanceHttpTest(unittest.TestCase):
    """GET /api/assets/<id>/{file,appearance} through the real server on port 0."""

    def setUp(self):
        import server
        self.box = Box()
        sel = self.box.root / "sel"
        web = self.box.root / "web"
        three = self.box.root / "three"
        for d in (sel, web, three):
            d.mkdir()
        self.app = server.App(self.box.repo, self.box.lib, self.box.data, sel, catalog=self.box.cat, web_dir=web,
                              three_dir=three)
        self.log = mock.patch.object(appearance.AppearanceResolver, "_log", staticmethod(lambda msg: None))
        self.log.start()
        self.srv = server.make_server(self.app, "127.0.0.1", 0, quiet=True)
        self.port = self.srv.server_address[1]
        self.thread = threading.Thread(target=self.srv.serve_forever, daemon=True)
        self.thread.start()

    def tearDown(self):
        self.srv.shutdown()
        self.srv.server_close()
        self.thread.join(5)
        self.log.stop()
        self.box.cleanup()

    def get(self, path):
        conn = http.client.HTTPConnection("127.0.0.1", self.port, timeout=20)
        conn.request("GET", path, headers={"Host": "127.0.0.1:%d" % self.port})
        r = conn.getresponse()
        body = r.read()
        conn.close()
        return r.status, r.getheader("Content-Type"), r.getheader("Content-Security-Policy"), body

    def test_file_and_appearance_by_indexed_id(self):
        ids = self.box.ids
        st, ct, _, body = self.get("/api/assets/%s/file" % ids["tex"])
        self.assertEqual((st, ct, body), (200, "image/png", PNG))
        st, ct, csp, body = self.get("/api/assets/%s/file" % ids["mat"])
        self.assertEqual((st, ct), (200, "text/plain; charset=utf-8"))
        self.assertIn("sandbox", csp)
        st, ct, _, body = self.get("/api/assets/%s/appearance" % ids["prefab"])
        self.assertEqual(st, 200)
        self.assertEqual(json.loads(body)["fidelity"], "resolved")

    def test_file_rejects_unknown_ids_paths_types_and_links(self):
        ids = self.box.ids
        for path in ("/api/assets/nope/file", "/api/assets/..%2F..%2Foutside.txt/file",
                     "/api/assets/%s/file" % ids["script"], "/api/assets/%s/file" % ids["locallink"],
                     "/api/assets/%s/file" % ids["escape"], "/api/assets/%s/file" % ids["zlink"],
                     "/api/assets/%s/file" % ids["tarlink"]):
            st, _, _, body = self.get(path)
            self.assertIn(st, (400, 404), path)
            self.assertNotIn(b"SECRET", body)


if __name__ == "__main__":
    unittest.main()
