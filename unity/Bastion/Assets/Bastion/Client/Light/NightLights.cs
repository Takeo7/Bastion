using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Units;
using UnityEngine;
using UnityEngine.Rendering;
using Light = UnityEngine.Light;

namespace Bastion.Client
{
    /// <summary>
    /// The map by night (0.10.1's night.ts): the warm light of what still works. Every street light's head glows (UI's
    /// GroundView.Lamps), a few wrecks' headlights are left on (MapBuilder), and a fixed pool of real lights goes where it
    /// counts: the Bengalas and fires burning first (the engine's lights that last a few turns), then the street lights
    /// nearest the point the camera looks at, each Bengala and fire with its glowing ball. The lit windows are the panes' (Bastion/Building, _Night). Hidden by day.
    /// three's point lights in tiles and over π become Unity's in metres.
    /// </summary>
    public sealed class NightLights : MonoBehaviour, IShotStep
    {
        public static NightLights Current { get; private set; }

        /// <summary>For the labs, with no battle's views (MapLab: BASTION_BENGALAS): the state whose Bengalas and fires burn.</summary>
        public static Bastion.Engine.GameState StateForLab;

        /// <summary>Where a lamp shines from (a street light's head) or a car's front, which way it faces, how much stronger it is (m).</summary>
        public struct Glow
        {
            public Vector3 at;
            public Vector3 facing;
            public float power;
        }

        /// <summary>Real lights, wherever they're most needed (night.ts REAL_LIGHTS).</summary>
        const int RealLights = 6;
        /// <summary>three's colours (linear).</summary>
        static readonly Color Lamp = new Color(1.0f, 0.62f, 0.3f), LampHead = new Color(2.2f, 1.5f, 0.9f), Headlight = new Color(2.0f, 1.9f, 1.6f);
        static readonly Color Flare = new Color(1.0f, 0.1f, 0.08f), Fire = new Color(1.0f, 0.5f, 0.15f);
        /// <summary>How strong each light is (Arturo 17/10: «la luz deslumbra mucho de noche»), and how far a street light reaches (tiles).</summary>
        const float LampLight = 9, BurningLight = 16, LampReach = 7;
        /// <summary>
        /// A street light's cone (degrees across), pointing down: what is level with the lamp or above it (a pine's crown
        /// beside it) is left dark, while the ground it lights reaches 5.18 × tan 70° ≈ 14 m, past LampReach. The Bengalas and
        /// fires shine all round. The review of 10/11, L-08 (the crowns of the pines by a field's street light came out yellow).
        /// </summary>
        const float LampCone = 140;
        /// <summary>
        /// Whether the street lights point down (LampCone) or shine all round: down where pines stand by the lamps (the
        /// fields and the facilities), all round in town, where a lamp lights the house front beside it as before (Arte's
        /// review of L-08, 10/11: down in town left the fronts and the posts dark). MapBuilder sets it with the map.
        /// </summary>
        public bool LampsDown = true;
        /// <summary>Seconds a Bengala's or a fire's light takes to come up or die down.</summary>
        const float Fade = 0.5f;
        /// <summary>three's point light (candela, 1/d² in tiles, over π) as URP's (in metres): times 1.5² over π.</summary>
        static readonly float Unity = MapTile * MapTile / Mathf.PI;
        const float MapTile = Map.MapSpace.Tile;
        static readonly bool Logs = System.Environment.GetEnvironmentVariable("BASTION_LUCES") == "1";
        /// <summary>BASTION_SIN_FAROLAS=1 (the labs): the real lights off, the glows kept (to weigh what they light).</summary>
        static readonly bool Unlit = System.Environment.GetEnvironmentVariable("BASTION_SIN_FAROLAS") == "1";

        readonly List<Glow> lamps = new List<Glow>();
        readonly List<GameObject> glows = new List<GameObject>();
        Light[] pool;
        readonly Dictionary<string, (Vector3 at, Color colour, float radius, float level, bool flare, GameObject glow)> burning = new Dictionary<string, (Vector3, Color, float, float, bool, GameObject)>();
        /// <summary>The map's fires among them (GameState.fires): their flames are FireView's, no ball of glow.</summary>
        readonly HashSet<string> flamesOwn = new HashSet<string>();
        float time;
        /// <summary>The camping's fires (Map.CampFire.Of) of the state they were looked for in.</summary>
        Bastion.Engine.GameState campFor;
        List<Vector3> campFires = new List<Vector3>();
        /// <summary>A camp fire's light's reach, as an engine light's radius (tiles: its range 2.6 times it).</summary>
        const float CampFireRadius = 3;
        Material glowMaterial, burnMaterial, haloMaterial;
        Texture2D haloTexture;

        void OnEnable() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Publish(true);
            if (glowMaterial != null) DestroyImmediate(glowMaterial);
            if (burnMaterial != null) DestroyImmediate(burnMaterial);
            if (haloMaterial != null) DestroyImmediate(haloMaterial);
            if (haloTexture != null) DestroyImmediate(haloTexture);
        }

        /// <summary>The map's street lights and headlights left on (Unity's metres), made again.</summary>
        public void Show(IReadOnlyList<(Vector3 at, Vector3 facing)> streetLights, IReadOnlyList<Glow> headlights)
        {
            Current = this;
            lamps.Clear();
            foreach (var (at, facing) in streetLights ?? new List<(Vector3, Vector3)>()) lamps.Add(new Glow { at = at, facing = facing, power = 1 });
            foreach (var o in glows) DestroyNow(o);
            glows.Clear();
            // The lamps' heads and the headlights (HDR, for the bloom), boxes in one mesh each.
            var heads = new List<(Vector3 centre, Vector3 size)>();
            foreach (var l in lamps) heads.Add((l.at, new Vector3(0.32f, 0.08f, 0.2f) * MapTile));
            var fronts = new List<(Vector3 centre, Vector3 size)>();
            foreach (var h in headlights ?? new List<Glow>())
            {
                // Across the car's nose, either side (night.ts: (-dz, dx) a quarter of a tile out).
                var side = new Vector3(h.facing.z, 0, -h.facing.x) * (0.24f * MapTile);
                foreach (int s in new[] { -1, 1 }) fronts.Add((h.at + side * s, new Vector3(0.1f, 0.07f, 0.1f) * MapTile));
            }
            glows.Add(Boxes("Farolas", heads, LampHead));
            glows.Add(Boxes("Faros", fronts, Headlight));
            glows.RemoveAll(o => o == null);
            // The map made again under the same root takes its children with it (MapView.Build): the pool too.
            if (pool != null && pool.Any(l => l == null)) pool = null;
            if (pool == null)
            {
                pool = new Light[RealLights];
                for (int k = 0; k < RealLights; k++)
                {
                    pool[k] = new GameObject($"Luz {k}").AddComponent<Light>();
                    pool[k].type = LightType.Point;
                    pool[k].shadows = LightShadows.None;
                    pool[k].transform.SetParent(transform, false);
                }
            }
            Step(0);
            Debug.Log($"[night] {lamps.Count} street lights, {headlights?.Count ?? 0} headlights; lights on: {string.Join(", ", pool.Where(l => l.enabled).Select(l => $"{l.transform.position} {l.intensity:0.##} {l.range:0.#}m"))}");
        }

        GameObject Boxes(string name, List<(Vector3 centre, Vector3 size)> boxes, Color colour)
        {
            if (boxes.Count == 0) return null;
            if (glowMaterial == null)
            {
                var shader = Resources.Load<Effects.FxArt>("FxArt")?.unlit ?? Shader.Find("Bastion/FxUnlit");
                glowMaterial = new Material(shader) { name = "Brillo de noche", renderQueue = (int)RenderQueue.AlphaTest };
                glowMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
                glowMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
                glowMaterial.SetFloat("_ZWrite", 1);
                glowMaterial.SetFloat("_Cull", (float)CullMode.Back);
            }
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var unit = cube.GetComponent<MeshFilter>().sharedMesh;
            DestroyNow(cube);
            var parts = boxes.Select(b => new CombineInstance { mesh = unit, transform = Matrix4x4.TRS(b.centre, Quaternion.identity, b.size) }).ToArray();
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts, true, true);
            var o = new GameObject(name);
            o.transform.SetParent(transform, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = glowMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            // A material's colour is taken as sRGB: three's linear HDR values given as such.
            block.SetColor("_BaseColor", colour.gamma);
            r.SetPropertyBlock(block);
            return o;
        }

        /// <summary>A Bengala's or a fire's glow (night.ts flareGeometry: a ball 0.32 tiles across, a fire's half as big again), drawn through.</summary>
        GameObject Burn(Vector3 at, bool flare)
        {
            if (burnMaterial == null)
            {
                var shader = Resources.Load<Effects.FxArt>("FxArt")?.unlit ?? Shader.Find("Bastion/FxUnlit");
                burnMaterial = new Material(shader) { name = "Brillo de bengala", renderQueue = (int)RenderQueue.Transparent };
                burnMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                burnMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                burnMaterial.SetFloat("_ZWrite", 0);
                burnMaterial.SetFloat("_Cull", (float)CullMode.Back);
            }
            var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            o.name = flare ? "Brillo de bengala" : "Brillo de fuego";
            DestroyNow(o.GetComponent<Collider>());
            o.transform.SetParent(transform, false);
            o.transform.position = at;
            o.transform.localScale = Vector3.one * ((flare ? 0.32f : 0.32f * 1.6f) * MapTile);
            var r = o.GetComponent<MeshRenderer>();
            r.sharedMaterial = burnMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            // Round it, the light it spills (0.10.1's bloom of its HDR colour; ours, gentler, leaves it a hard ball): a soft
            // glow facing the camera, added (Step turns it), drawn a little towards the camera so the ground doesn't cut it.
            if (haloMaterial == null)
            {
                haloMaterial = new Material(burnMaterial) { name = "Halo de bengala", renderQueue = (int)RenderQueue.Transparent + 1 };
                haloMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
                haloMaterial.SetFloat("_Cull", (float)CullMode.Off);
                haloTexture = Effects.Fx.Glow();
                haloMaterial.SetTexture("_BaseMap", haloTexture);
            }
            var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = "Halo";
            DestroyNow(halo.GetComponent<Collider>());
            halo.transform.SetParent(o.transform, false);
            halo.transform.localScale = Vector3.one * HaloSize;
            var hr = halo.GetComponent<MeshRenderer>();
            hr.sharedMaterial = haloMaterial;
            hr.shadowCastingMode = ShadowCastingMode.Off;
            hr.receiveShadows = false;
            return o;
        }

        /// <summary>A glow's halo across, in balls, and how bright against the ball.</summary>
        const float HaloSize = 2.6f, HaloLight = 0.3f;

        /// <summary>A glow's colour (three's linear HDR, for the bloom) at `level` of its opacity.</summary>
        static void Level(GameObject glow, Color colour, float level)
        {
            var c = colour.gamma;
            c.a = level;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            glow.GetComponent<Renderer>().SetPropertyBlock(block);
            if (glow.transform.childCount == 0) return;
            var halo = glow.transform.GetChild(0);
            c = (colour * HaloLight).gamma;
            c.a = level;
            block.SetColor("_BaseColor", c);
            halo.GetComponent<Renderer>().SetPropertyBlock(block);
            var camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (camera == null) return;
            halo.rotation = camera.transform.rotation;
            halo.position = glow.transform.position - camera.transform.forward * (0.6f * MapTile);
        }

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        public void ShotStep(float dt) => Step(dt);

        static readonly Vector4[] lightAt = new Vector4[RealLights], lightColour = new Vector4[RealLights];

        /// <summary>The real lights as the fog counts them (Fog.shader: _NightLightAt, _NightLightColour); none if `off`.</summary>
        void Publish(bool off)
        {
            for (int k = 0; k < RealLights; k++)
            {
                var l = !off && pool != null && k < pool.Length ? pool[k] : null;
                bool on = l != null && l.enabled && l.intensity > 0;
                var at = on ? l.transform.position : Vector3.zero;
                lightAt[k] = on ? new Vector4(at.x, at.y, at.z, 1 / (l.range * l.range)) : new Vector4(0, 0, 0, 1);
                // (Its colour is given as sRGB, and URP takes it to linear.)
                lightColour[k] = on ? (Vector4)(l.color.linear * l.intensity) : Vector4.zero;
            }
            Shader.SetGlobalVectorArray("_NightLightAt", lightAt);
            Shader.SetGlobalVectorArray("_NightLightColour", lightColour);
        }

        /// <summary>Fades and flicker, and the real lights put where they count.</summary>
        void Step(float dt)
        {
            bool night = Mood.IsNight;
            foreach (var o in glows) if (o != null && o.activeSelf != night) o.SetActive(night);
            foreach (var b in burning.Values) if (b.glow != null && b.glow.activeSelf != night) b.glow.SetActive(night);
            // (Gone with the map made again, till Show makes them anew.)
            if (pool == null || pool.Any(l => l == null))
            {
                Publish(true);
                return;
            }
            if (!night)
            {
                foreach (var l in pool) l.enabled = false;
                Publish(true);
                return;
            }
            time += dt;
            // The Bengalas and fires burning now (the engine's lights with turns): new ones come up, gone ones die down.
            var s = UnitViews.Current?.State ?? StateForLab;
            var now = new HashSet<string>();
            if (s?.Lights != null)
                foreach (var l in s.Lights)
                {
                    // A map's fire (GameState.fires, 1/11) burns the whole mission, its light without turns; its flames are
                    // FireView's, so no glowing ball, and it is a fire whatever its radius (a Bengala's is 3 too).
                    bool mapFire = s.Fires != null && s.Fires.Any(f => f.Id == l.Id);
                    if (l.Turns == null && !mapFire) continue;
                    now.Add(l.Id);
                    if (burning.ContainsKey(l.Id)) continue;
                    bool flare = !mapFire && l.Radius >= 3;
                    var at = Map.MapSpace.TileToWorld(s, l.Tile) + Vector3.up * (flare ? 1.4f : mapFire ? 0.7f : 0.35f) * MapTile;
                    burning[l.Id] = (at, flare ? Flare : Fire, (float)l.Radius, 0, flare, null);
                    if (mapFire) flamesOwn.Add(l.Id);
                }
            // The camping's fire (runs, H9 §5: its `firePit` marks, Map.CampFire's flames) burns the whole mission, as a map's.
            if (s != campFor)
            {
                campFor = s;
                campFires = Map.CampFire.Of(s);
            }
            foreach (var at in campFires)
            {
                string id = $"hoguera {at.x:0.0},{at.z:0.0}";
                now.Add(id);
                if (burning.ContainsKey(id)) continue;
                burning[id] = (at + Vector3.up * 0.7f * MapTile, Fire, CampFireRadius, 0, false, null);
                flamesOwn.Add(id);
            }
            foreach (string id in burning.Keys.ToList())
            {
                var b = burning[id];
                b.level = Mathf.Clamp01(b.level + (now.Contains(id) ? dt : -dt) / Fade);
                if (!now.Contains(id) && b.level <= 0)
                {
                    DestroyNow(b.glow);
                    burning.Remove(id);
                    continue;
                }
                // (Made again if the map made again took it with it; none for a map's fire, whose flames are drawn.)
                if (!flamesOwn.Contains(id))
                {
                    if (b.glow == null) b.glow = Burn(b.at, b.flare);
                    Level(b.glow, (b.flare ? Flare * 2.2f : Fire * 2.5f), b.level);
                }
                burning[id] = b;
            }
            int n = 0;
            foreach (var b in burning.Values)
            {
                if (n >= pool.Length) break;
                var light = pool[n++];
                float flicker = b.flare ? 0.9f + 0.1f * Mathf.Sin(time * 23) : 0.75f + 0.25f * Mathf.Sin(time * 11) * Mathf.Sin(time * 7.3f);
                light.enabled = true;
                // (Only when it changes: a light's type set again every frame may not be drawn that frame.)
                if (light.type != LightType.Point) light.type = LightType.Point;
                light.color = b.colour.gamma;
                light.intensity = BurningLight * b.level * flicker * Unity;
                light.range = b.radius * 2.6f * MapTile;
                // A fire's light over its flames, not in them.
                light.transform.position = b.flare ? b.at : b.at + Vector3.up * 0.55f * MapTile;
            }
            // Then the street lights nearest the point the camera looks at.
            var camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            var focus = Vector3.zero;
            if (camera != null)
            {
                var ray = camera.transform.forward;
                float t = ray.y < -0.01f ? -camera.transform.position.y / ray.y : 0;
                focus = camera.transform.position + ray * t;
            }
            // On a map that is one building (H7, the hospital), its rooms' ceiling lights too (Ceiling), among the street lights.
            var inside = s != null && Bastion.Engine.Buildings.ByRoom(s) ? Ceiling(s) : null;
            // The camping's caravans found (runs, H9 §5), a ceiling's light in each, as the hospital's rooms (CaravanLights).
            if (s != null && Map.Caravans.Any(s)) inside = (inside ?? new List<Glow>()).Concat(CaravanLights(s)).ToList();
            var candidates = inside == null || inside.Count == 0 ? lamps : lamps.Concat(inside).ToList();
            if (inside != null && inside.Count > 0 && camera != null)
            {
                // (Where the camera looks on the storey in view's floor, not the ground's.)
                var ray = camera.transform.forward;
                float floorY = ceilingStorey * Map.MapSpace.Storey;
                float t = ray.y < -0.01f ? (floorY - camera.transform.position.y) / ray.y : 0;
                focus = camera.transform.position + ray * t;
            }
            var nearest = Enumerable.Range(0, candidates.Count).OrderBy(i => (candidates[i].at.x - focus.x) * (candidates[i].at.x - focus.x) + (candidates[i].at.z - focus.z) * (candidates[i].at.z - focus.z)).Take(pool.Length - n).ToList();
            foreach (int i in nearest)
            {
                var light = pool[n++];
                var l = candidates[i];
                if (l.power < 0)
                {
                    // A ceiling's (Ceiling): cold, all round, under the ceiling, nearer than a street light's; a hall's
                    // (power −2: a shed's floor, H9) higher up, stronger and further.
                    bool hall = l.power < -1.5f;
                    light.enabled = true;
                    if (light.type != LightType.Point) light.type = LightType.Point;
                    light.color = CeilingLight.gamma;
                    light.intensity = (hall ? HallPower : CeilingPower) * Unity;
                    light.range = (hall ? HallReach : CeilingReach) * MapTile;
                    light.transform.position = l.at;
                    continue;
                }
                light.enabled = true;
                if (LampsDown && light.type != LightType.Spot)
                {
                    light.type = LightType.Spot;
                    light.spotAngle = LampCone;
                    light.innerSpotAngle = LampCone * 0.6f;
                }
                else if (!LampsDown && light.type != LightType.Point) light.type = LightType.Point;
                light.color = Lamp.gamma;
                light.intensity = LampLight * l.power * Unity;
                light.range = LampReach * Mathf.Sqrt(l.power) * MapTile;
                // Well under the lamp's own head: a point light right at a surface burnt it white.
                light.transform.position = l.at - Vector3.up * 0.6f * MapTile;
                light.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }
            for (; n < pool.Length; n++) pool[n].enabled = false;
            if (Unlit) foreach (var l in pool) l.enabled = false;
            // BASTION_LUCES=1 (the labs): where the camera looks and the street lights given the real ones, now and then.
            if (Logs)
                Debug.Log($"[luces] focus {focus:F1}, camera {(camera != null ? camera.transform.position.ToString("F1") + " looking " + camera.transform.forward.ToString("F3") : "-")}: {string.Join(" ", nearest.Select(i => candidates[i].at.ToString("F1")))}");
            Publish(false);
        }

        /// <summary>
        /// The ceiling lights of a map that is one building (H7, the hospital: Buildings.ByRoom), the battle's all inside: a
        /// cold white, the colour of a tube, not the street lights' amber, whose pools are the engine's lit tiles (where one
        /// is seen from afar); dimmer and nearer, so a room reads lit round where the camera looks and not as a lit tile.
        /// Without them the rooms by night were the moon's teal, barely to be told from the fog. Linear, three's as the rest.
        /// </summary>
        static readonly Color CeilingLight = new Color(0.78f, 0.9f, 1.0f);
        /// <summary>How strong, how far it reaches (tiles), how high over its storey's floor (tiles: a storey is 2.3, the cut walls 0.95), how far apart in a room (tiles).</summary>
        const float CeilingPower = 7, CeilingReach = 5, CeilingHigh = 1.6f, CeilingApart = 4;
        /// <summary>
        /// A room this big (tiles) is a hall (H9, the shed's floor: one room of some 400 tiles found at once): its lights are
        /// a warehouse's high bays, so far apart (tiles) that the six real ones light most of what the camera takes in, as
        /// high, as strong and as far as these. With the hospital's, the shed by night was lit only round where it looked.
        /// </summary>
        const float HallRoom = 150, HallPower = 14, HallReach = 9, HallHigh = 2.4f, HallApart = 7;

        /// <summary>
        /// A light under the roof of each of the camping's caravans the squad has found (runs, H9 §5; Map.Caravans.Rooms), in
        /// its room's middle: a ceiling's (power −1, CeilingLight). Worked out again when what is found changes.
        /// </summary>
        List<Glow> CaravanLights(Bastion.Engine.GameState s)
        {
            if (s == caravansFor && s.Seq == caravansSeq && caravanLights != null) return caravanLights;
            caravansFor = s;
            caravansSeq = s.Seq;
            caravanLights = new List<Glow>();
            var found = Bastion.Engine.Buildings.FoundTiles(s);
            foreach (var r in Map.Caravans.Rooms(s))
            {
                bool any = false;
                for (int y = r.Y; y < r.Y + r.H && !any; y++)
                    for (int x = r.X; x < r.X + r.W && !any; x++) any = found.Contains(y * s.Width + x);
                if (any) caravanLights.Add(new Glow { at = Map.MapSpace.World(r.X + (r.W - 1) / 2f, r.Y + (r.H - 1) / 2f) + Vector3.up * CeilingHigh * MapTile, power = -1 });
            }
            return caravanLights;
        }

        List<Glow> caravanLights;
        Bastion.Engine.GameState caravansFor;
        int caravansSeq;

        List<Glow> ceiling;
        Bastion.Engine.GameState ceilingFor;
        int ceilingSeq = -1, ceilingFound = -1, ceilingStorey;

        /// <summary>
        /// The ceiling lights of the rooms found on the storey in view (the fog's: MapView.Level), each a Glow with power −1:
        /// in each room the tile nearest its middle, then again and again the one of its tiles furthest from those, while
        /// it is CeilingApart or more from them all (a room a light, the corridor one every four tiles, a room joined to it
        /// as its shape goes). Worked out again when what is found or the storey changes.
        /// </summary>
        List<Glow> Ceiling(Bastion.Engine.GameState s)
        {
            var view = Map.MapView.Current;
            int storey = view == null || view.Level >= view.Levels ? 0 : view.Level;
            int found = s.Discovered?.Count ?? 0;
            if (ceiling != null && s == ceilingFor && s.Seq == ceilingSeq && found == ceilingFound && storey == ceilingStorey) return ceiling;
            (ceilingFor, ceilingSeq, ceilingFound, ceilingStorey) = (s, s.Seq, found, storey);
            ceiling = new List<Glow>();
            var known = Bastion.Engine.Buildings.FoundTiles(s);
            int area = s.Width * s.Height;
            foreach (var room in Bastion.Engine.Buildings.Rooms(s))
            {
                if (room.Count == 0 || room[0] / area != storey || !known.Contains(room[0])) continue;
                var tiles = room.Select(node => new Vector2(node % area % s.Width, node % area / s.Width)).ToList();
                bool hall = tiles.Count >= HallRoom;
                var middle = tiles.Aggregate(Vector2.zero, (a, b) => a + b) / tiles.Count;
                var lit = new List<Vector2> { tiles.OrderBy(p => (p - middle).sqrMagnitude).First() };
                while (true)
                {
                    var far = tiles.OrderByDescending(p => lit.Min(q => (p - q).sqrMagnitude)).First();
                    if (lit.Min(q => (far - q).magnitude) < (hall ? HallApart : CeilingApart)) break;
                    lit.Add(far);
                }
                foreach (var p in lit)
                    ceiling.Add(new Glow { at = Map.MapSpace.World(p.x, p.y, storey) + Vector3.up * ((hall ? HallHigh : CeilingHigh) * MapTile), power = hall ? -2 : -1 });
            }
            return ceiling;
        }

        static void DestroyNow(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
