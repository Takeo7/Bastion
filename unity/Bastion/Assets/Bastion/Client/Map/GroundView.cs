using System.Collections.Generic;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The ground of the map (0.10.1's ground.ts and the ground parts of mapView.ts; UI's, on Arte's MapSpace): one plane
    /// at the ground's height under the whole map and well past it, painted by Bastion/Ground from the GroundMap (what each
    /// tile's floor is), and what lies about on it (GroundCover). The buildings' floors and the raised ground's tops are
    /// Arte's, over it. Built with the map (MapBuilder), and brought up to date when tiles change.
    /// </summary>
    public sealed class GroundView
    {
        /// <summary>Tiles of ground past each edge of the map (0.10.1's surroundings: 160 in all), into the haze.</summary>
        const int Margin = 80;

        static readonly Color Pave = Hex(0xbdb5ac), Kerb = Hex(0xd3cbc1), Asphalt = Hex(0xc9b8b0), Moss = Hex(0x6c6e3e), Grime = Hex(0x564840),
            Dust = Hex(0x9c8a72), Paint = Hex(0xe6e2d4), Yellow = Hex(0xd8aa3a), Grass = Hex(0xd2dab8), GrassDry = Hex(0xf0dca2), Dirt = Hex(0xffffff),
            Concrete = Hex(0x9a9a96), Deck = Hex(0x8a8c8e);

        GameState s;
        BuildingPlan plan;
        GroundMap map;
        Texture2D texture;
        Material material;
        GameObject plane;
        PropParts props;
        GroundCover cover;
        GroundOcclusion occlusion;

        /// <summary>Where each street light's lamp hangs (Unity's metres, over the road) and the way its arm points: the night's lights hang there.</summary>
        public IReadOnlyList<(Vector3 at, Vector3 facing)> Lamps => cover?.Lamps ?? new List<(Vector3, Vector3)>();

        /// <summary>What each tile's floor is, as last built (the scattered details read it).</summary>
        public GroundMap Map => map;

        /// <summary>The ground of `state` under `parent`: its plane, its material and the map it paints from.</summary>
        public GameObject Build(GameState state, BuildingPlan buildings, Transform parent)
        {
            s = state;
            plan = buildings;
            map = Mapped(state);
            texture = map.Texture();
            var source = Resources.Load<Material>("Ground");
            if (source == null) Debug.LogError("Ground: Resources/Ground.mat missing (Bastion.EditorTools.GroundMake.Run)");
            material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "Suelo";
            Look(material);
            plane = new GameObject("Suelo");
            plane.transform.SetParent(parent, false);
            plane.AddComponent<MeshFilter>().sharedMesh = Plane(state);
            var renderer = plane.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            // What lies about on it: papers, pebbles, tufts, junk, manholes and drains, the pumps' islands.
            props = new PropParts();
            cover = new GroundCover(props);
            cover.Build(state, map, plane.transform);
            // Over the pumps, a canopy a station that fades when the fight is under it (mapView.ts placeCanopies).
            PumpCanopies.Build(state, props, parent);
            // What closes the map round what is played (the outline), beside the ground: it hides the sky too.
            FrameView.Build(state, props, parent);
            // The contact occlusion, baked once the map is whole (before its next frame) from what stands on it.
            Shader.SetGlobalTexture("_GroundAO", Texture2D.whiteTexture);
            occlusion = plane.AddComponent<GroundOcclusion>();
            occlusion.Map = plane.GetComponentInParent<MapView>()?.transform ?? parent.root;
            occlusion.Ground = plane.transform;
            occlusion.Width = state.Width;
            occlusion.Height = state.Height;
            occlusion.MarkDirty();
            return plane;
        }

        /// <summary>
        /// The map brought to `next` (the same map: a door, cover blown away; MapBuilder.Update knows which tiles changed):
        /// what each tile is worked out again whole (a few thousand tiles, a texel each) and the texture refilled.
        /// </summary>
        public void Rebuild(GameState next, IReadOnlyCollection<int> changed)
        {
            if (plane == null) return;
            s = next;
            plan?.Follow(next);
            map = Mapped(next);
            texture = map.Texture(texture);
            material.SetTexture("_GroundMap", texture);
            cover?.Rebuild(next, map, changed);
            occlusion?.MarkDirty();
        }

        /// <summary>
        /// What each tile is, with a wall tile's outer half the street's (the building's floor covers the rest: mapView.ts
        /// build, ground.outdoors).
        /// </summary>
        GroundMap Mapped(GameState state)
        {
            var m = new GroundMap(state);
            if (plan != null)
                for (int i = 0; i < plan.Owner.Length && i < m.Kinds.Length; i++)
                    if (plan.Owner[i] >= 0 && plan.Inside[i] == 0) m.Outdoors(i);
            return m;
        }

        /// <summary>The material's map, the map's size, and the biome's look (ground.ts material()).</summary>
        void Look(Material m)
        {
            m.SetTexture("_GroundMap", texture);
            m.SetVector("_GroundSize", new Vector4(s.Width, s.Height, 0, 0));
            m.SetFloat("_Biome", s.Biome == Biomes.Wilds ? 1 : s.Biome == Biomes.Facility ? 2 : 0);
            m.SetFloat("_Tarmac", s.Biome == Biomes.Wilds && s.Site == Sites.Roadside ? 1 : 0);
            // Still winter in the campaign (the engine says so); before it did, only the opening chapter (snow.ts).
            m.SetFloat("_Snow", (s.Snow ?? s.CampaignMission?.Story == 1) ? 1 : 0);
            var band = map.Band;
            m.SetVector("_Water", new Vector4(band != null && MapOutline.Sunken.Contains(band.Kind) ? band.Side : -1, MapOutline.FarBank, 0, 0));
            // sRGB as the 0.10.1 had them: Unity takes a material's colours to linear itself.
            m.SetColor("_Pave", Pave);
            m.SetColor("_Kerb", Kerb);
            m.SetColor("_AsphaltTint", Asphalt);
            m.SetColor("_Moss", Moss);
            m.SetColor("_Grime", Grime);
            m.SetColor("_Dust", Dust);
            m.SetColor("_Paint", Paint);
            m.SetColor("_Yellow", Yellow);
            m.SetColor("_GrassTint", Grass);
            m.SetColor("_GrassDry", GrassDry);
            m.SetColor("_DirtTint", Dirt);
            m.SetColor("_Concrete", Concrete);
            m.SetColor("_Deck", Deck);
        }

        /// <summary>One quad from Margin tiles before the map to Margin past it, facing up, at the ground's height (0).</summary>
        static Mesh Plane(GameState state)
        {
            float x0 = -Margin * MapSpace.Tile, x1 = (state.Width + Margin) * MapSpace.Tile;
            float z0 = Margin * MapSpace.Tile, z1 = -(state.Height + Margin) * MapSpace.Tile;
            var mesh = new Mesh { name = "Suelo" };
            mesh.vertices = new[] { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
