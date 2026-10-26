"""Tests for catalog.py with tiny on-disk fixtures (zip + unitypackage + fake repo)."""

import io
import json
import os
import sqlite3
import stat
import sys
import tarfile
import tempfile
import unittest
import zipfile
from pathlib import Path
from typing import Optional

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import catalog as cat_mod  # noqa: E402
from catalog import Catalog  # noqa: E402

PNG = b"\x89PNG\r\n\x1a\n" + b"\x00" * 32
G_PREFAB = "a" * 32
G_FBX = "b" * 32
G_IMPORTED_ONLY = "c" * 32
G_MAT = "d" * 32


def make_unitypackage(path: Path, entries: dict, previews: Optional[dict] = None) -> None:
    previews = previews or {}
    with tarfile.open(str(path), "w:gz") as tf:
        for guid, (pathname, data) in entries.items():
            d = tarfile.TarInfo(guid)
            d.type = tarfile.DIRTYPE
            tf.addfile(d)
            for leaf, blob in (("pathname", pathname.encode() + b"\n00"), ("asset", data), ("asset.meta", b"guid: x")):
                ti = tarfile.TarInfo("%s/%s" % (guid, leaf))
                ti.size = len(blob)
                tf.addfile(ti, io.BytesIO(blob))
            if guid in previews:
                ti = tarfile.TarInfo("%s/preview.png" % guid)
                ti.size = len(previews[guid])
                tf.addfile(ti, io.BytesIO(previews[guid]))


def make_zip(path: Path, members: dict, symlink: Optional[str] = None) -> None:
    with zipfile.ZipFile(str(path), "w") as zf:
        for name, data in members.items():
            zf.writestr(name, data)
        zf.writestr("Textures/", b"")
        if symlink:
            info = zipfile.ZipInfo(symlink)
            info.external_attr = (stat.S_IFLNK | 0o777) << 16
            zf.writestr(info, "/etc/passwd")


def write_manifest(folder: Path, name: str, files: list) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "manifest.json").write_text(json.dumps({"pack": name, "files": [{"path": f} for f in files]}))


class Fixture:
    def __init__(self, root: Path):
        self.root = root
        lib = root / "lib"
        repo = root / "repo"
        self.lib, self.repo, self.data = lib, repo, root / "data"
        lib.mkdir()
        packs = ["POLYGON - Town Pack", "SIDEKICK - Starter", "SIDEKICK - Unreal Plugin", "POLYGON - Missing Pack",
                 "SIDEKICK - Fantasy Knights", "SIMPLE - Zombies"]
        (lib / "inventory.json").write_text(json.dumps({"packs": packs}))

        simple = lib / "SIMPLE - Zombies"
        write_manifest(simple, "SIMPLE - Zombies", ["SourceFiles/SIMPLE_Zombies_SourceFiles.zip"])
        (simple / "SourceFiles").mkdir()
        make_zip(simple / "SourceFiles" / "SIMPLE_Zombies_SourceFiles.zip", {"Textures/Simple_Zombie_01.png": PNG})

        town = lib / "POLYGON - Town Pack"
        write_manifest(town, packs[0], [
            "Unity/POLYGON_Town_Unity_2022_3_v1_9_2.unitypackage", "Extras/POLYGON_Town_ICON.png",
            "SourceFiles/POLYGON_Town_SourceFiles_v7.zip", "Unreal/POLYGON_Town_Unreal.zip",
            "Godot/POLYGON_Town_Godot.zip"])
        for sub in ("Unity", "Extras", "SourceFiles", "Unreal", "Godot"):
            (town / sub).mkdir()
        (town / "Extras" / "POLYGON_Town_ICON.png").write_bytes(PNG)
        make_unitypackage(town / "Unity" / "POLYGON_Town_Unity_2022_3_v1_9_2.unitypackage", {
            G_PREFAB: ("Assets/Synty/PolygonTown/Prefabs/SM_Prop_Bench_01.prefab", b"prefab"),
            G_FBX: ("Assets/Synty/PolygonTown/Models/SM_Prop_Bench_01.fbx", b"fbxdata"),
            G_IMPORTED_ONLY: ("Assets/Synty/PolygonTown/Models/SM_Prop_Lamp_01.fbx", b"lamp"),
            G_MAT: ("Assets/Synty/PolygonTown/Materials/Town.mat", b"mat"),
            "e" * 32: ("Assets/Synty/PolygonTown/Scripts/Thing.cs", b"cs"),
            "f" * 32: ("Assets/Synty/PolygonTown/Models/SM_Prop_Bench_01.fbx.meta", b"meta"),
        }, previews={G_PREFAB: PNG})
        make_zip(town / "SourceFiles" / "POLYGON_Town_SourceFiles_v7.zip", {
            "FBX/SM_Prop_Bench_01.fbx": b"srcfbx", "Textures/PolygonTown_01_A.png": PNG,
            "FBX/SM_Prop_Bench_01.obj": b"obj", "../evil.png": PNG,
            "FBX/SK_Chr_Clerk_01.fbx": b"c", "FBX/Unreal_Characters/SK_Chr_Clerk_01.fbx": b"u",
        }, symlink="Textures/link.png")
        make_zip(town / "Unreal" / "POLYGON_Town_Unreal.zip", {"Content/SM_X.uasset": b"u"})
        make_zip(town / "Godot" / "POLYGON_Town_Godot.zip", {"x.glb": b"g"})

        sk = lib / "SIDEKICK - Starter"
        write_manifest(sk, packs[1], ["Unity/SIDEKICK_Starter_Unity_v1.unitypackage", "Extras/SIDEKICK_Starter_ICON.png"])
        (sk / "Unity").mkdir()
        (sk / "Extras").mkdir()
        os.symlink("/etc/hosts", str(sk / "Extras" / "SIDEKICK_Starter_ICON.png"))
        make_unitypackage(sk / "Unity" / "SIDEKICK_Starter_Unity_v1.unitypackage", {
            "1" * 32: ("Assets/Synty/SidekickCharacters/Meshes/SK_HUMN_BASE_01_09FCHR_HU01.fbx", b"part"),
        })

        knights = lib / "SIDEKICK - Fantasy Knights"
        write_manifest(knights, "SIDEKICK - Fantasy Knights", ["Unity/SIDEKICK_Fantasy_Knights_Unity_2021_3_v1_0_0.unitypackage"])
        (knights / "Unity").mkdir()
        make_unitypackage(knights / "Unity" / "SIDEKICK_Fantasy_Knights_Unity_2021_3_v1_0_0.unitypackage", {
            "1" * 32: ("Assets/Synty/SidekickCharacters/Meshes/SK_HUMN_BASE_01_09FCHR_HU01.fbx", b"part"),
        })

        up = lib / "SIDEKICK - Unreal Plugin"
        write_manifest(up, packs[2], ["Unreal/Tool_UE53.zip", "Unreal/Tool_UE58.zip"])
        (up / "Unreal").mkdir()
        make_zip(up / "Unreal" / "Tool_UE53.zip", {"Content/SK_Old.uasset": b"u"})
        make_zip(up / "Unreal" / "Tool_UE58.zip", {"Content/SK_New.uasset": b"u", "Content/T_Tex.uasset": b"t"})

        # Fake repo: imported Synty files + Bastion references.
        unity = repo / "unity" / "Bastion" / "Assets"
        tm = unity / "Synty" / "PolygonTown"
        (tm / "Prefabs").mkdir(parents=True)
        (tm / "Models").mkdir(parents=True)
        (tm / "Prefabs" / "SM_Prop_Bench_01.prefab").write_text("prefab")
        (tm / "Prefabs" / "SM_Prop_Bench_01.prefab.meta").write_text("fileFormatVersion: 2\nguid: %s\n" % G_PREFAB)
        (tm / "Models" / "SM_Prop_Bench_01.fbx").write_bytes(b"localfbx")
        (tm / "Models" / "SM_Prop_Bench_01.fbx.meta").write_text("guid: %s\n" % G_FBX)
        (tm / "Models" / "SM_Prop_Lamp_01.fbx").write_bytes(b"lamp")
        (tm / "Models" / "SM_Prop_Lamp_01.fbx.meta").write_text("guid: %s\n" % G_IMPORTED_ONLY)
        res = unity / "Bastion" / "Client" / "Resources"
        res.mkdir(parents=True)
        (res / "MapCatalog.asset").write_text(
            "MonoBehaviour:\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  prefabs:\n"
            "  - {fileID: 1, guid: %s, type: 3}\n" % ("9" * 32, G_PREFAB))
        (res / "MapArt.json").write_text(json.dumps({"props": {
            "bench": {"pack": "town", "fbx": "SM_Prop_Bench_01"},
            "ghost": {"pack": "town", "fbx": "SM_Prop_Nope_01"},
            "clerk": {"pack": "town", "fbx": "SK_Chr_Clerk_01"},
            "gun": {"pack": "mixamo", "fbx": "Rifle"}}}, indent=1))
        (res / "Wardrobe.json").write_text(json.dumps({"pieces": [{"id": "b1", "parts": ["SK_HUMN_BASE_01_09FCHR_HU01"]}]}))
        three = repo / "packages" / "client" / "public" / "models" / "synty"
        three.mkdir(parents=True)
        (three / "sources.json").write_text(json.dumps({
            "models/synty/town/town.webp": json.dumps({"kind": "texture", "src": "/POLYGON_Town_SourceFiles/Textures/PolygonTown_01_A.png"}),
            "models/synty/sk/a.glb": json.dumps({"kind": "character", "atlasImage":
                                                 "/SIDEKICK_FantasyKnights_Extracted/Assets/Synty/SidekickCharacters/Meshes/SK_HUMN_BASE_01_09FCHR_HU01.fbx"}),
        }, indent=1))

    def catalog(self) -> Catalog:
        return Catalog(self.repo, self.lib, self.data)


class CatalogTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.fx = Fixture(Path(cls.tmp.name))
        cls.cat = cls.fx.catalog()
        cls.summary = cls.cat.reindex()

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def find(self, **filters):
        filters.setdefault("limit", 100)
        return self.cat.search(filters)

    def by_path(self, path, pack=None):
        items = [a for a in self.find(q=path.rsplit("/", 1)[-1].rsplit(".", 1)[0])["items"] if a["path"] == path]
        if pack:
            items = [a for a in items if a["packName"] == pack]
        self.assertEqual(len(items), 1, path)
        return items[0]

    def test_status_and_packs(self):
        st = self.cat.status()
        self.assertGreater(st["assets"], 0)
        self.assertIsNotNone(st["indexedAt"])
        packs = {p["name"]: p for p in self.cat.packs()}
        self.assertIn("SIDEKICK - Modern Thugs", packs)
        self.assertFalse(packs["SIDEKICK - Modern Thugs"]["available"])
        self.assertEqual(packs["SIDEKICK - Modern Thugs"]["assetCount"], 0)
        self.assertFalse(packs["POLYGON - Missing Pack"]["available"])
        town = packs["POLYGON - Town Pack"]
        self.assertTrue(town["available"])
        self.assertEqual(town["thumbnailUrl"], "/api/packs/%s/thumbnail" % town["id"])
        self.assertGreaterEqual(town["usedCount"], 1)
        self.assertTrue((self.fx.data / "last-index.json").is_file())

    def test_indexing_skips_meta_scripts_dirs_and_unsafe(self):
        paths = [a["path"] for a in self.find(pack="polygon-town-pack")["items"]]
        self.assertIn("Assets/Synty/PolygonTown/Models/SM_Prop_Bench_01.fbx", paths)
        self.assertIn("FBX/SM_Prop_Bench_01.obj", paths)
        self.assertFalse([p for p in paths if p.endswith((".meta", ".cs", "/"))])
        self.assertNotIn("../evil.png", paths)
        self.assertNotIn("Textures/link.png", paths)
        # Unity/SourceFiles present: Unreal and Godot are skipped for this pack.
        self.assertFalse([p for p in paths if p.endswith((".uasset", ".glb"))])

    def test_unreal_fallback_only_latest(self):
        names = sorted(a["name"] for a in self.find(pack="sidekick-unreal-plugin")["items"])
        self.assertEqual(names, ["SK_New", "T_Tex"])

    def test_filters(self):
        self.assertTrue(all(a["kind"] == "textura" for a in self.find(kind="textura")["items"]))
        self.assertTrue(all(a["family"] == "SIDEKICK" for a in self.find(family="SIDEKICK")["items"]))
        r = self.cat.search({"q": "bench", "limit": 1, "offset": 1})
        self.assertEqual(r["limit"], 1)
        self.assertEqual(len(r["items"]), 1)
        self.assertGreaterEqual(r["total"], 3)
        self.assertEqual(self.cat.search({"q": "100%_", "limit": 5})["total"], 0)
        self.assertEqual(self.cat.search({"limit": 1000})["limit"], 100)
        some = self.find(q="bench")["items"][0]["id"]
        r = self.cat.search({"ids": [some, "nope"]})
        self.assertEqual([a["id"] for a in r["items"]], [some])
        self.assertEqual(self.cat.search({"ids": []})["total"], 0)
        with self.assertRaises(ValueError):
            self.cat.search({"state": "bogus"})

    def test_guid_usage_and_imported_not_used(self):
        prefab = self.by_path("Assets/Synty/PolygonTown/Prefabs/SM_Prop_Bench_01.prefab")
        self.assertTrue(prefab["usages"])
        u = prefab["usages"][0]
        self.assertEqual(u["runtime"], "Unity")
        self.assertTrue(u["evidence"][0]["path"].endswith("MapCatalog.asset"))
        self.assertEqual(u["evidence"][0]["line"], 4)
        lamp = self.by_path("Assets/Synty/PolygonTown/Models/SM_Prop_Lamp_01.fbx")
        self.assertEqual(lamp["usages"], [])
        self.assertNotIn(lamp["id"], self.cat.used_ids())
        # Imported Unity files live under unity/Bastion: the exposed locator is relative to the repo root.
        self.assertEqual(lamp["source"]["localRoot"], "repo")
        self.assertEqual(lamp["source"]["localPath"], "unity/Bastion/Assets/Synty/PolygonTown/Models/SM_Prop_Lamp_01.fbx")
        self.assertTrue((self.fx.repo / lamp["source"]["localPath"]).is_file())
        self.assertEqual(self.cat.read_media(lamp["id"])[0], b"lamp")

    def test_disallowed_families_visible_with_issue(self):
        tex = self.by_path("Textures/Simple_Zombie_01.png")
        self.assertEqual(tex["family"], "SIMPLE")
        self.assertEqual(tex["usages"], [])
        issues = self.cat.status()["issues"]
        self.assertTrue([i for i in issues if "SIMPLE / POLYGON MINI" in i and "no están permitidos" in i], issues)
        self.assertFalse([i for i in issues if "aparecen como usados" in i], issues)

    def test_json_usages_conservative(self):
        fbx = self.by_path("Assets/Synty/PolygonTown/Models/SM_Prop_Bench_01.fbx")
        self.assertIn("MapArt.json", json.dumps(fbx["usages"]))
        src = self.by_path("FBX/SM_Prop_Bench_01.fbx")
        self.assertEqual(src["usages"], [])  # Unity copy preferred, source FBX not double-counted
        # Same Sidekick mesh shipped by two packs: one canonical usage (Starter), alternatives in the evidence.
        part = self.by_path("Assets/Synty/SidekickCharacters/Meshes/SK_HUMN_BASE_01_09FCHR_HU01.fbx", "SIDEKICK - Starter")
        self.assertIn("Wardrobe.json", json.dumps(part["usages"]))
        self.assertIn("SIDEKICK - Fantasy Knights", part["usages"][0]["evidence"][0]["detail"])
        # The *_Extracted folder name picks the Knights copy for the Three.js usage (version-less archive key).
        other = self.by_path("Assets/Synty/SidekickCharacters/Meshes/SK_HUMN_BASE_01_09FCHR_HU01.fbx", "SIDEKICK - Fantasy Knights")
        self.assertEqual([u["runtime"] for u in other["usages"]], ["Three.js"])
        tex = self.by_path("Textures/PolygonTown_01_A.png")
        self.assertEqual(tex["usages"][0]["runtime"], "Three.js")
        cov = self.cat.status()["coverage"]["usages"]
        self.assertEqual(cov["unityJson"]["notFound"], 1)
        self.assertEqual(cov["unityJson"]["nonSynty"], 1)
        used = set(self.cat.used_ids())
        self.assertEqual(used, {a["id"] for a in self.find(state="used")["items"]})
        clerk = self.by_path("FBX/SK_Chr_Clerk_01.fbx")
        self.assertTrue(clerk["usages"])  # Unreal_Characters duplicate is not counted as ambiguity
        self.assertEqual(self.by_path("FBX/Unreal_Characters/SK_Chr_Clerk_01.fbx")["usages"], [])
        self.assertEqual(len(used), 6)

    def test_ids_stable_across_reindex(self):
        before = sorted(a["id"] for a in self.find(pack="polygon-town-pack")["items"])
        self.cat.reindex()
        after = sorted(a["id"] for a in self.find(pack="polygon-town-pack")["items"])
        self.assertEqual(before, after)

    def test_media_and_previews(self):
        tex = self.by_path("Textures/PolygonTown_01_A.png")
        self.assertEqual(tex["preview"]["kind"], "image")
        self.assertEqual(self.cat.read_media(tex["id"]), (PNG, "image/png"))
        self.assertEqual(self.cat.read_media(tex["id"], mode="thumbnail")[0], PNG)
        local = self.by_path("Assets/Synty/PolygonTown/Models/SM_Prop_Bench_01.fbx")
        self.assertEqual(local["preview"]["kind"], "fbx")
        self.assertEqual(self.cat.read_media(local["id"])[0], b"localfbx")
        prefab = self.by_path("Assets/Synty/PolygonTown/Prefabs/SM_Prop_Bench_01.prefab")
        self.assertIsNone(prefab["preview"])
        self.assertFalse(prefab["thumbnailIsPack"])
        self.assertEqual(self.cat.read_media(prefab["id"], mode="thumbnail"), (PNG, "image/png"))
        with self.assertRaises(FileNotFoundError):
            self.cat.read_media(prefab["id"])
        obj = self.by_path("FBX/SM_Prop_Bench_01.obj")
        with self.assertRaises(FileNotFoundError):
            self.cat.read_media(obj["id"])
        self.assertTrue(obj["thumbnailIsPack"])
        with self.assertRaises(KeyError):
            self.cat.read_media("nope")
        with self.assertRaises(ValueError):
            self.cat.read_media(tex["id"], mode="raw")

    def test_pack_thumbnail_security(self):
        data, mime = self.cat.read_pack_thumbnail("polygon-town-pack")
        self.assertEqual((data, mime), (PNG, "image/png"))
        with self.assertRaises(FileNotFoundError):
            self.cat.read_pack_thumbnail("sidekick-starter")  # icon is a symlink
        with self.assertRaises(KeyError):
            self.cat.read_pack_thumbnail("../../etc")
        with self.assertRaises(FileNotFoundError):
            self.cat.read_pack_thumbnail("sidekick-modern-thugs")

    def test_read_guards(self):
        with self.assertRaises(ValueError):
            self.cat._read_zip("../x.zip", "a.png")
        with self.assertRaises(ValueError):
            self.cat._read_zip("POLYGON - Town Pack/SourceFiles/POLYGON_Town_SourceFiles_v7.zip", "a.txt")
        with self.assertRaises(ValueError):
            self.cat._read_zip("POLYGON - Town Pack/SourceFiles/POLYGON_Town_SourceFiles_v7.zip", "Textures/link.png")
        with self.assertRaises(ValueError):
            self.cat._read_file(self.fx.lib, "../outside.png")

    def test_failed_reindex_keeps_previous(self):
        before = self.cat.status()["assets"]
        broken = Catalog(self.fx.repo, self.fx.root / "no-disk", self.fx.data)
        with self.assertRaises(FileNotFoundError):
            broken.reindex()
        self.assertEqual(broken.status()["assets"], before)

        orig = cat_mod.UsageDetector.run

        def boom(self):
            raise RuntimeError("fallo")

        cat_mod.UsageDetector.run = boom
        try:
            with self.assertRaises(RuntimeError):
                self.cat.reindex()
        finally:
            cat_mod.UsageDetector.run = orig
        self.assertEqual(self.cat.status()["assets"], before)
        self.assertFalse((self.fx.data / "catalog.sqlite.tmp").exists())

    def test_empty_catalog_before_index(self):
        with tempfile.TemporaryDirectory() as d:
            c = Catalog(self.fx.repo, self.fx.lib, Path(d))
            self.assertEqual(c.search({})["total"], 0)
            self.assertEqual(c.packs(), [])
            self.assertIsNone(c.get_asset("x"))
            self.assertEqual(c.status()["assets"], 0)


if __name__ == "__main__":
    unittest.main()
