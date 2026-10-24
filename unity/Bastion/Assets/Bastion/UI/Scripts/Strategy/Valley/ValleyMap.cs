using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The valley's geography (Resources/Valley/valley.json, written by tools/ui/valle-unity.mts from the 0.10.1's
    /// valleyGeography.ts): its size, the zones with their roads and the Marea's thresholds, the roads traced along the
    /// painting, the landmarks, Atalaya's works and the Bastion. In the 0.10.1's map units (x east, z south). The zones'
    /// names and roads stand in for the engine's REGIONS until campaign.ts is in C#.
    /// </summary>
    [Serializable]
    public sealed class ValleyData
    {
        [Serializable] public sealed class Size { public float w, d, px; }
        [Serializable] public sealed class Cells { public int w, d; }
        [Serializable] public sealed class Point { public float x, z; }
        [Serializable] public sealed class Works { public float x, z, rx, rz; }
        [Serializable] public sealed class Zone { public string id, name; public float x, z, threshold; public string[] roads; }
        [Serializable] public sealed class Road { public string a, b; public float[] points; }
        [Serializable] public sealed class Landmark { public string key, name; public float x, z; }

        public Size size;
        public Cells field;
        public int doomMax;
        public string mareaSource;
        public Works atalayaWorks;
        public Point bastion;
        public Zone[] zones;
        public Road[] roads;
        public Landmark[] landmarks;

        static ValleyData loaded;

        public static ValleyData Load()
        {
            if (loaded != null) return loaded;
            var text = Resources.Load<TextAsset>("Valley/valley");
            if (text == null) throw new Exception("Resources/Valley/valley.json missing (tools/ui/valle-unity.mts)");
            return loaded = JsonUtility.FromJson<ValleyData>(text.text);
        }

        public Zone ZoneOf(string id) => Array.Find(zones, z => z.id == id);
    }

    /// <summary>
    /// The valley map (geoscape.ts GeoscapeSet of the 0.10.1): the painting on a plane under a still camera looking
    /// straight down, the Marea over it (Valley.shader, its activity easing to the campaign's), fitted whole to the part
    /// of the screen the panels leave (no zoom), slid aside for a zone picked under a sheet; La Lata driving the roads
    /// to its zone. Below the world (y −500), so nothing of a scene shows in it. Points of the map to the panel for the
    /// markers (ToPanel).
    /// </summary>
    public sealed class ValleyMap : MonoBehaviour
    {
        /// <summary>The valley lies this far below everything else.</summary>
        const float Depth = -500;
        /// <summary>The free part of the screen keeps this much round the valley, and a picked zone this much from a sheet (UI pixels).</summary>
        const float Margin = 12;
        const float Clear = 150;
        /// <summary>What lies round the map: the panels' steel.</summary>
        static readonly Color Table = new Color32(0x1b, 0x1e, 0x20, 0xff);
        /// <summary>Where La Lata parks at a zone, from its pin (map units): below it, clear of the name and badges.</summary>
        static readonly Vector2 Park = new Vector2(0.3f, 0.72f);
        /// <summary>La Lata's speed on the roads (map units a second).</summary>
        const float Speed = 4f;

        ValleyData data;
        Material material;
        Camera cam;
        GameObject plane;
        float level;
        float levelGoal;
        bool levelKnown;
        float scale = 1;
        Vector2 centre;
        Vector2 aside;
        readonly List<Vector2> path = new List<Vector2>();
        float along;
        string lataZone;
        Vector2 lata;

        public ValleyData Data => data;
        /// <summary>La Lata's place now (map units) and whether it is driving.</summary>
        public Vector2 Lata => lata;
        public bool Driving => path.Count > 1 && along < Length(path);

        /// <summary>The camera that shows the map (its own unless given one: the lab's shot camera).</summary>
        public void Build(Camera camera = null)
        {
            if (data != null) return;
            data = ValleyData.Load();
            var shader = Shader.Find("Bastion/Valley");
            if (shader == null) throw new Exception("Bastion/Valley shader missing (Resources/Valley/Valley.shader)");
            material = new Material(shader);
            material.SetTexture("_MainTex", Resources.Load<Texture2D>("Valley/mapa"));
            material.SetTexture("_Field", Field());
            material.SetVector("_Size", new Vector4(data.size.w, data.size.d, 0, 0));
            material.SetVector("_Atalaya", new Vector4(data.atalayaWorks.x, data.atalayaWorks.z, data.atalayaWorks.rx, data.atalayaWorks.rz));
            material.SetVector("_Bastion", new Vector4(data.bastion.x, data.bastion.z, 0, 0));
            plane = new GameObject("Valle");
            plane.transform.SetParent(transform, false);
            plane.transform.position = new Vector3(0, Depth, 0);
            plane.AddComponent<MeshFilter>().sharedMesh = Quad(data.size.w, data.size.d);
            var renderer = plane.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            cam = camera != null ? camera : new GameObject("Cámara del valle").AddComponent<Camera>();
            if (camera == null) cam.transform.SetParent(transform, false);
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Table;
            cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            cam.nearClipPlane = 1;
            cam.farClipPlane = 20;
            var home = data.ZoneOf("weu");
            lataZone = home?.id;
            lata = home != null ? new Vector2(home.x, home.z) + Park : Vector2.zero;
        }

        public Camera Camera => cam;

        public void Show(bool on)
        {
            if (plane != null) plane.SetActive(on);
            if (cam != null && cam.gameObject != gameObject && cam.transform.parent == transform) cam.enabled = on;
        }

        /// <summary>The Marea's field: the activity at which it reaches each cell (half floats, from the north).</summary>
        Texture2D Field()
        {
            var bytes = Resources.Load<TextAsset>("Valley/marea");
            var t = new Texture2D(data.field.w, data.field.d, TextureFormat.RHalf, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.LoadRawTextureData(bytes.bytes);
            t.Apply(false, true);
            return t;
        }

        /// <summary>The painting's plane, W × D map units, north up (Unity's +z).</summary>
        static Mesh Quad(float w, float d)
        {
            var mesh = new Mesh { name = "Valle" };
            mesh.vertices = new[] { new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0, -d / 2), new Vector3(w / 2, 0, d / 2), new Vector3(-w / 2, 0, d / 2) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The activity drawn (the campaign's doom): the edge eases there; the first one is taken as it is.</summary>
        public void SetLevel(float doom)
        {
            levelGoal = doom;
            if (levelKnown) return;
            levelKnown = true;
            level = doom;
        }

        /// <summary>
        /// The valley fitted whole to `free` (panel pixels, y down; the panels' room left with no sheet open), and slid
        /// aside so that `keep` (a zone's place, map units) stays `Clear` pixels left of `sheet` (a sheet's left edge;
        /// none, no sheet).
        /// </summary>
        public void Fit(Rect free, Vector2 panel, Vector2? keep, float? sheet)
        {
            if (cam == null || panel.y <= 0) return;
            scale = Mathf.Min((free.width - 2 * Margin) / data.size.w, (free.height - 2 * Margin) / data.size.d);
            if (scale <= 0) return;
            centre = free.center;
            var goal = Vector2.zero;
            if (keep.HasValue && sheet.HasValue)
            {
                var x = centre.x + keep.Value.x * scale;
                if (x > sheet.Value - Clear) goal.x = sheet.Value - Clear - x;
            }
            aside = Vector2.Lerp(aside, goal, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 6));
            cam.orthographicSize = panel.y / (2 * scale);
            var mid = centre + aside;
            cam.transform.position = new Vector3(-(mid.x - panel.x / 2) / scale, Depth + 10, (mid.y - panel.y / 2) / scale);
            material.SetFloat("_Pixel", Application.isBatchMode ? 1 : Mathf.Max(1, Screen.height / panel.y));
        }

        /// <summary>A point of the map (map units, z south) on the panel (pixels, y down); null, off the screen.</summary>
        public Vector2? ToPanel(float x, float z, Vector2 panel)
        {
            if (cam == null) return null;
            var v = cam.WorldToViewportPoint(new Vector3(x, Depth, -z));
            if (v.z < 0) return null;
            return new Vector2(v.x * panel.x, (1 - v.y) * panel.y);
        }

        /// <summary>The pixels a map unit is on the panel now (the markers keep their size; only names come and go with it).</summary>
        public float Scale => scale;

        /// <summary>La Lata to `zone`: along the roads, the shortest way (roadRoute), parking by its pin.</summary>
        public void DriveTo(string zone)
        {
            if (data == null || zone == null || zone == lataZone) return;
            var route = Route(lataZone, zone);
            path.Clear();
            path.Add(lata);
            for (var i = 0; i + 1 < route.Count; i++) path.AddRange(RoadLine(route[i], route[i + 1]));
            var end = data.ZoneOf(zone);
            if (end != null) path.Add(new Vector2(end.x, end.z) + Park);
            along = 0;
            lataZone = zone;
        }

        void Update()
        {
            if (data == null) return;
            level = Mathf.MoveTowards(level, levelGoal, Time.unscaledDeltaTime * 0.8f);
            material.SetFloat("_Level", level);
            if (path.Count > 1)
            {
                along += Time.unscaledDeltaTime * Speed;
                lata = PointAt(path, along);
                if (along >= Length(path)) path.Clear();
            }
        }

        /// <summary>The shortest way by road from `a` to `b`, both included (campaign.ts roadRoute: ties by each zone's order of roads).</summary>
        List<string> Route(string a, string b)
        {
            var prev = new Dictionary<string, string> { [a] = null };
            var queue = new List<string> { a };
            for (var k = 0; k < queue.Count && !prev.ContainsKey(b); k++)
                foreach (var n in data.ZoneOf(queue[k])?.roads ?? new string[0])
                {
                    if (prev.ContainsKey(n)) continue;
                    prev[n] = queue[k];
                    queue.Add(n);
                }
            var route = new List<string>();
            if (!prev.ContainsKey(b)) return new List<string> { a, b };
            for (var at = b; at != null; at = prev[at]) route.Insert(0, at);
            return route;
        }

        /// <summary>The road from `a` to `b` traced along the painting, its points from `a`; straight if it wasn't.</summary>
        List<Vector2> RoadLine(string a, string b)
        {
            var line = new List<Vector2>();
            foreach (var r in data.roads)
            {
                if (!(r.a == a && r.b == b) && !(r.a == b && r.b == a)) continue;
                for (var i = 0; i + 1 < r.points.Length; i += 2) line.Add(new Vector2(r.points[i], r.points[i + 1]));
                if (r.a == b) line.Reverse();
                return line;
            }
            var za = data.ZoneOf(a);
            var zb = data.ZoneOf(b);
            if (za != null) line.Add(new Vector2(za.x, za.z));
            if (zb != null) line.Add(new Vector2(zb.x, zb.z));
            return line;
        }

        static float Length(List<Vector2> line)
        {
            var total = 0f;
            for (var i = 1; i < line.Count; i++) total += Vector2.Distance(line[i - 1], line[i]);
            return total;
        }

        static Vector2 PointAt(List<Vector2> line, float distance)
        {
            for (var i = 1; i < line.Count; i++)
            {
                var step = Vector2.Distance(line[i - 1], line[i]);
                if (distance <= step) return Vector2.Lerp(line[i - 1], line[i], step > 0 ? distance / step : 1);
                distance -= step;
            }
            return line[line.Count - 1];
        }
    }
}
