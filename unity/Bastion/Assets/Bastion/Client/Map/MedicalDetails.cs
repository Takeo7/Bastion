using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The scenery of clinics and houses that no pack has (0.10.1's medicalDetails.ts and medicalDebris.ts, own POLYGON-style
    /// shapes): a bed's frame, stripped or with a body under a sheet, a body under a sheet, a folded screen, supplies,
    /// luggage, crutches, books, a cross and signs on a wall; blood pooled, in drops and on a wall, grime, papers about, what
    /// was spilled, linen, a chair over, a drip fallen. Each as faces with their colours (linear) in 0.10.1's frame of the
    /// tile they stand on (tiles: x east, y up, z south, its middle the origin), as three's geometries make them.
    /// </summary>
    public static partial class MedicalDetails
    {
        public struct Face
        {
            public Vector3[] points;
            public Color colour;
        }

        /// <summary>The kinds of medicalDebris.ts (flat ones, the stains and papers, cast no shadow: mapView.ts MEDICAL_FLAT).</summary>
        static readonly HashSet<string> Debris = new HashSet<string> { "bloodPool", "bloodDrops", "wallBlood", "floorGrime", "scatteredPapers", "medicalWaste", "overturnedChair", "fallenIV", "spilledSupplies", "rumpledLinen" };
        static readonly Dictionary<string, List<Face>> cache = new Dictionary<string, List<Face>>();
        const int Metal = 0x83918e, Rust = 0x746554, White = 0xd6d4c6, Dark = 0x343a37;
        const int DebrisMetal = 0x7e8a85, DebrisWhite = 0xcac6b3, DebrisDark = 0x383e38;

        /// <summary>Kind's faces (variant: which of a debris kind's 8), or none if it has no shape of its own (a corpse: a prop).</summary>
        public static IReadOnlyList<Face> Of(string kind, int variant = 0)
        {
            bool debris = Debris.Contains(kind);
            string key = debris ? $"{kind}:{variant & 7}" : kind;
            if (cache.TryGetValue(key, out var ready)) return ready;
            var faces = new List<Face>();
            if (debris) MakeDebris(faces, kind, variant);
            else MakeDetail(faces, kind);
            cache[key] = faces;
            return faces;
        }

        // ------------------------------------------------------------ three's geometries, as faces (counter-clockwise outside)

        static Color Hex(int hex) => new Color((hex >> 16 & 255) / 255f, (hex >> 8 & 255) / 255f, (hex & 255) / 255f).linear;

        static void Put(List<Face> to, IEnumerable<Vector3[]> polygons, int colour, Matrix4x4 at)
        {
            var c = Hex(colour);
            foreach (var p in polygons) to.Add(new Face { points = p.Select(v => at.MultiplyPoint3x4(v)).ToArray(), colour = c });
        }

        /// <summary>BoxGeometry(w, h, d), centred.</summary>
        static IEnumerable<Vector3[]> Box(float w, float h, float d)
        {
            var half = new Vector3(w / 2, h / 2, d / 2);
            // Each face: its normal n and u × v = n, so (−u−v, u−v, u+v, −u+v) goes round it counter-clockwise.
            foreach (var (n, u, v) in new[] { (Vector3.right, Vector3.up, Vector3.forward), (Vector3.left, Vector3.forward, Vector3.up), (Vector3.up, Vector3.forward, Vector3.right), (Vector3.down, Vector3.right, Vector3.forward), (Vector3.forward, Vector3.right, Vector3.up), (Vector3.back, Vector3.up, Vector3.right) })
            {
                Vector3 S(Vector3 a) => Vector3.Scale(a, half);
                var c = S(n);
                yield return new[] { c - S(u) - S(v), c + S(u) - S(v), c + S(u) + S(v), c - S(u) + S(v) };
            }
        }

        /// <summary>CylinderGeometry(top, bottom, height, segments): along y, centred; x = r sin θ, z = r cos θ as three's.</summary>
        static IEnumerable<Vector3[]> Cylinder(float top, float bottom, float height, int segments)
        {
            Vector3 At(float r, float y, int i)
            {
                float a = (float)i / segments * Mathf.PI * 2;
                return new Vector3(r * Mathf.Sin(a), y, r * Mathf.Cos(a));
            }
            float h = height / 2;
            for (int i = 0; i < segments; i++) yield return new[] { At(top, h, i), At(bottom, -h, i), At(bottom, -h, i + 1), At(top, h, i + 1) };
            if (top > 0) yield return Enumerable.Range(0, segments).Select(i => At(top, h, i)).ToArray();
            if (bottom > 0) yield return Enumerable.Range(0, segments).Select(i => At(bottom, -h, segments - i)).ToArray();
        }

        /// <summary>TorusGeometry(radius, tube, radial, tubular): round z, in the xy plane.</summary>
        static IEnumerable<Vector3[]> Torus(float radius, float tube, int radial, int tubular)
        {
            Vector3 At(int i, int j)
            {
                float u = (float)i / tubular * Mathf.PI * 2, v = (float)j / radial * Mathf.PI * 2;
                return new Vector3((radius + tube * Mathf.Cos(v)) * Mathf.Cos(u), (radius + tube * Mathf.Cos(v)) * Mathf.Sin(u), tube * Mathf.Sin(v));
            }
            for (int j = 0; j < radial; j++)
                for (int i = 0; i < tubular; i++) yield return new[] { At(i, j), At(i + 1, j), At(i + 1, j + 1), At(i, j + 1) };
        }

        static readonly float Golden = (1 + Mathf.Sqrt(5)) / 2;
        static readonly Vector3[] IcoPoints =
        {
            new Vector3(-1, Golden, 0), new Vector3(1, Golden, 0), new Vector3(-1, -Golden, 0), new Vector3(1, -Golden, 0),
            new Vector3(0, -1, Golden), new Vector3(0, 1, Golden), new Vector3(0, -1, -Golden), new Vector3(0, 1, -Golden),
            new Vector3(Golden, 0, -1), new Vector3(Golden, 0, 1), new Vector3(-Golden, 0, -1), new Vector3(-Golden, 0, 1),
        };
        static readonly int[] IcoFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        /// <summary>IcosahedronGeometry(radius, 0), scaled.</summary>
        static IEnumerable<Vector3[]> Icosahedron(float radius, Vector3 scale)
        {
            var p = IcoPoints.Select(v => Vector3.Scale(v.normalized * radius, scale)).ToArray();
            for (int f = 0; f < IcoFaces.Length; f += 3) yield return new[] { p[IcoFaces[f]], p[IcoFaces[f + 1]], p[IcoFaces[f + 2]] };
        }

        /// <summary>The faces turned so each looks away from `inside(face)` (three's winding, which these shapes don't need to match).</summary>
        static IEnumerable<Vector3[]> Outward(IEnumerable<Vector3[]> faces, Func<Vector3, Vector3> inside)
        {
            foreach (var f in faces)
            {
                var n = Vector3.Cross(f[1] - f[0], f[2] - f[0]);
                var mid = f.Aggregate(Vector3.zero, (a, b) => a + b) / f.Length;
                yield return Vector3.Dot(n, mid - inside(mid)) < 0 ? f.Reverse().ToArray() : f;
            }
        }

        static Matrix4x4 T(float x, float y, float z) => Matrix4x4.Translate(new Vector3(x, y, z));
        static Matrix4x4 Rx(float a) => Matrix4x4.Rotate(Quaternion.AngleAxis(a * Mathf.Rad2Deg, Vector3.right));
        static Matrix4x4 Ry(float a) => Matrix4x4.Rotate(Quaternion.AngleAxis(a * Mathf.Rad2Deg, Vector3.up));
        static Matrix4x4 Rz(float a) => Matrix4x4.Rotate(Quaternion.AngleAxis(a * Mathf.Rad2Deg, Vector3.forward));
        /// <summary>three's Euler (rx, ry, rz) in its order XYZ.</summary>
        static Matrix4x4 Euler(float rx, float ry, float rz) => Rx(rx) * Ry(ry) * Rz(rz);

        static IEnumerable<Vector3[]> Transformed(IEnumerable<Vector3[]> faces, Matrix4x4 m) => faces.Select(f => f.Select(v => m.MultiplyPoint3x4(v)).ToArray());

        // ------------------------------------------------------------ medicalDetails.ts

        static void MakeDetail(List<Face> to, string kind)
        {
            void Add(IEnumerable<Vector3[]> g, int c, float x, float y, float z, float rz = 0) => Put(to, g, c, T(x, y, z) * Rz(rz));
            void BoxAt(int c, float x, float y, float z, float w, float h, float d) => Add(Box(w, h, d), c, x, y, z);
            IEnumerable<Vector3[]> Cyl(float r, float h, int n) => Outward(Cylinder(r, r, h, n), p => new Vector3(0, p.y, 0) * (Mathf.Abs(p.y) < h / 2 - 1e-4f ? 1 : 0));
            void Sheet(float @base)
            {
                // A single faceted drape: raised at the head, shoulders, knees and feet, falling over each side.
                var sections = new[] { (-0.49f, 0.09f, 0.12f), (-0.35f, 0.18f, 0.14f), (-0.19f, 0.17f, 0.23f), (0.07f, 0.12f, 0.21f), (0.28f, 0.14f, 0.15f), (0.48f, 0.05f, 0.12f) };
                var v = new List<Vector3>();
                foreach (var (z, h, w) in sections)
                    foreach (var (x, y) in new[] { (-0.29f, -0.07f), (-w, h * 0.45f), (0f, h), (w, h * 0.45f), (0.29f, -0.07f) }) v.Add(new Vector3(x, @base + y, z));
                var tris = new List<Vector3[]>();
                for (int j = 0; j < sections.Length - 1; j++)
                    for (int i = 0; i < 4; i++)
                    {
                        int a = j * 5 + i, b = a + 5;
                        tris.Add(new[] { v[a], v[b], v[a + 1] });
                        tris.Add(new[] { v[a + 1], v[b], v[b + 1] });
                    }
                Put(to, tris, White, Matrix4x4.identity);
                // Blood following the faceted fabric.
                float Cloth(float x, float z)
                {
                    int i = Array.FindIndex(sections, s => s.Item1 >= z);
                    var b = sections[Mathf.Max(1, i)];
                    var a = sections[Mathf.Max(0, i - 1)];
                    float t = (z - a.Item1) / (b.Item1 - a.Item1);
                    float h = Mathf.LerpUnclamped(a.Item2, b.Item2, t), w = Mathf.LerpUnclamped(a.Item3, b.Item3, t);
                    return @base + h * (1 - Mathf.Abs(x) / w * 0.55f) + 0.004f;
                }
                var blood = new List<Vector3> { new Vector3(0.01f, Cloth(0.01f, -0.27f), -0.27f) };
                for (int n = 0; n < 10; n++)
                {
                    float a = n / 10f * Mathf.PI * 2, x = 0.01f + Mathf.Cos(a) * (n % 2 == 1 ? 0.055f : 0.08f), z = -0.27f + Mathf.Sin(a) * 0.065f;
                    blood.Add(new Vector3(x, Cloth(x, z), z));
                }
                Put(to, Enumerable.Range(0, 10).Select(n => new[] { blood[0], blood[(n + 1) % 10 + 1], blood[n + 1] }), 0x7c3433, Matrix4x4.identity);
            }
            switch (kind)
            {
                case "bareBed":
                case "coveredBed":
                    foreach (float x in new[] { -0.25f, 0.25f })
                        foreach (float z in new[] { -0.46f, 0.46f })
                        {
                            BoxAt(Rust, x, 0.37f, z, 0.035f, 0.66f, 0.035f);
                            Add(Cyl(0.055f, 0.04f, 8), Dark, x, 0.06f, z, Mathf.PI / 2);
                        }
                    foreach (float x in new[] { -0.25f, 0.25f }) BoxAt(Rust, x, 0.39f, 0, 0.035f, 0.06f, 0.96f);
                    for (int n = 0; n < 8; n++) BoxAt(Metal, 0, 0.39f, -0.43f + n * 0.123f, 0.48f, 0.025f, 0.045f);
                    foreach (float z in new[] { -0.46f, 0.46f })
                        foreach (float y in new[] { 0.54f, 0.66f }) BoxAt(Rust, 0, y, z, 0.53f, 0.03f, 0.03f);
                    if (kind == "coveredBed")
                    {
                        BoxAt(0x8c9b8d, 0, 0.43f, 0, 0.48f, 0.075f, 0.94f);
                        Sheet(0.48f);
                    }
                    break;
                case "coveredCorpse":
                    Sheet(0.075f);
                    break;
                case "privacyScreen":
                    foreach (float z in new[] { -0.42f, 0.06f, 0.38f })
                    {
                        BoxAt(Metal, 0.43f, 0.61f, z, 0.022f, 1.16f, 0.022f);
                        BoxAt(Metal, 0.40f, 0.04f, z, 0.15f, 0.025f, 0.08f);
                    }
                    BoxAt(Metal, 0.43f, 1.20f, -0.02f, 0.025f, 0.025f, 0.84f);
                    for (int n = 0; n < 12; n++)
                    {
                        float z = -0.39f + n * 0.065f;
                        BoxAt(n % 2 == 1 ? 0xc0c9bc : 0x9daea6, 0.43f + n % 2 * 0.025f, 0.75f, z, 0.025f, 0.79f, 0.065f);
                        Add(Transformed(Outward(Torus(0.023f, 0.006f, 4, 6), Ring(0.023f)), Ry(Mathf.PI / 2)), Metal, 0.43f, 1.18f, z);
                    }
                    break;
                case "medicalSupplies":
                    foreach (var (x, z, c) in new[] { (-0.14f, 0.04f, 0xa88c55), (0.02f, 0.10f, 0xe0e0d2), (0.15f, 0.03f, 0xb0b48c) })
                    {
                        Add(Cyl(0.035f, 0.105f, 7), c, x, 0.057f, z);
                        BoxAt(0x576a68, x, 0.116f, z, 0.055f, 0.025f, 0.055f);
                        BoxAt(White, x, 0.060f, z - 0.034f, 0.04f, 0.04f, 0.008f);
                    }
                    BoxAt(0xe1d9bf, -0.07f, 0.02f, -0.10f, 0.16f, 0.035f, 0.10f);
                    BoxAt(0x98493f, -0.07f, 0.04f, -0.10f, 0.07f, 0.008f, 0.022f);
                    foreach (float x in new[] { 0.08f, 0.16f }) Add(Transformed(Cyl(0.032f, 0.06f, 8), Rx(Mathf.PI / 2)), White, x, 0.04f, -0.10f);
                    break;
                case "luggage":
                    BoxAt(0x655646, 0.30f, 0.19f, -0.14f, 0.22f, 0.34f, 0.30f);
                    BoxAt(0x403a32, 0.30f, 0.20f, -0.30f, 0.17f, 0.24f, 0.025f);
                    BoxAt(Metal, 0.30f, 0.39f, -0.14f, 0.10f, 0.07f, 0.025f);
                    BoxAt(Dark, 0.30f, 0.42f, -0.14f, 0.10f, 0.025f, 0.035f);
                    foreach (float z in new[] { -0.24f, -0.05f }) BoxAt(Dark, 0.30f, 0.025f, z, 0.18f, 0.035f, 0.04f);
                    break;
                case "crutches":
                    foreach (float x in new[] { 0.24f, 0.35f })
                    {
                        foreach (float dx in new[] { -0.025f, 0.025f }) Add(Cyl(0.009f, 0.83f, 5), Metal, x + dx, 0.46f, -0.29f, -0.12f);
                        BoxAt(0x8c785a, x + 0.04f, 0.88f, -0.29f, 0.14f, 0.045f, 0.055f);
                        BoxAt(Dark, x, 0.045f, -0.29f, 0.035f, 0.06f, 0.035f);
                        BoxAt(0x8c785a, x, 0.57f, -0.29f, 0.09f, 0.035f, 0.035f);
                    }
                    break;
                case "books":
                    for (int n = 0; n < 3; n++)
                    {
                        BoxAt(new[] { 0x706e46, 0x83564d, 0x4c6a70 }[n], -0.10f + n * 0.04f, 0.015f + n * 0.033f, 0.04f, 0.22f, 0.025f, 0.16f);
                        BoxAt(0xd8d0b7, -0.10f + n * 0.04f, 0.017f + n * 0.033f, 0.04f, 0.20f, 0.016f, 0.14f);
                    }
                    break;
                case "wallCross":
                    BoxAt(0x69503d, 0, 1.40f, 0.80f, 0.045f, 0.33f, 0.035f);
                    BoxAt(0x69503d, 0, 1.47f, 0.80f, 0.20f, 0.045f, 0.035f);
                    break;
                case "cautionSign":
                case "safeSign":
                    bool safe = kind == "safeSign";
                    BoxAt(Dark, 0, 1.33f, 0.80f, 0.65f, 0.36f, 0.035f);
                    BoxAt(safe ? 0x4a7060 : 0xc6ad50, 0, 1.33f, 0.776f, 0.61f, 0.32f, 0.015f);
                    void Text(string value, float y, float size)
                    {
                        float total = value.Sum(ch => (Glyphs[ch][0].Length + 1) * size) - size;
                        float left = -total / 2;
                        foreach (char ch in value)
                        {
                            var rows = Glyphs[ch];
                            for (int j = 0; j < rows.Length; j++)
                                for (int i = 0; i < rows[j].Length; i++)
                                    if (rows[j][i] == '1') BoxAt(safe ? White : Dark, -(left + i * size), y - j * size, 0.764f, size * 0.9f, size * 0.9f, 0.008f);
                            left += (rows[0].Length + 1) * size;
                        }
                    }
                    if (safe)
                    {
                        Text("ZONA", 1.43f, 0.016f);
                        Text("SEGURA", 1.29f, 0.014f);
                    }
                    else
                    {
                        Text("ATENCION", 1.35f, 0.011f);
                        BoxAt(Dark, 0, 1.23f, 0.76f, 0.45f, 0.018f, 0.008f);
                    }
                    break;
            }
        }

        /// <summary>What a torus's face looks away from: the nearest point of its ring (radius, round z).</summary>
        static Func<Vector3, Vector3> Ring(float radius) => p => new Vector3(p.x, p.y, 0).normalized * radius;

        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
            ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
            ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
            ['N'] = new[] { "10001", "11001", "11001", "10101", "10011", "10011", "10001" },
            ['C'] = new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" },
            ['I'] = new[] { "111", "010", "010", "010", "010", "010", "111" },
            ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
            ['Z'] = new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" },
            ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
            ['G'] = new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01110" },
            ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
            ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
        };

        // ------------------------------------------------------------ medicalDebris.ts

        static void MakeDebris(List<Face> to, string kind, int variant)
        {
            double Rand(int n)
            {
                double t = Math.Sin((variant + 1) * 127.1 + n * 311.7) * 43758.5453;
                return t - Math.Floor(t);
            }
            float R(int n) => (float)Rand(n);
            var parts = new List<(IEnumerable<Vector3[]> faces, int colour, Matrix4x4 at)>();
            void Add(IEnumerable<Vector3[]> g, int c, float x, float y, float z, float rx = 0, float ry = 0, float rz = 0) => parts.Add((g, c, T(x, y, z) * Euler(rx, ry, rz)));
            void BoxAt(int c, float x, float y, float z, float w, float h, float d, float ry = 0) => Add(Box(w, h, d), c, x, y, z, 0, ry);
            // A stain: a fan of 15 round its middle, flat, facing up.
            IEnumerable<Vector3[]> Stain(float rx, float rz, int salt = 0)
            {
                var v = new List<Vector3> { Vector3.zero };
                const int n = 15;
                for (int k = 0; k < n; k++)
                {
                    float a = k / (float)n * Mathf.PI * 2, r = 0.68f + R(k + salt) * 0.32f;
                    v.Add(new Vector3(Mathf.Cos(a) * rx * r, 0, Mathf.Sin(a) * rz * r));
                }
                return Enumerable.Range(0, n).Select(k => new[] { v[0], v[(k + 1) % n + 1], v[k + 1] }).ToArray();
            }
            void Drop(int c, float x, float z, float rx, float rz, int salt, float y = 0.014f) => Add(Stain(rx, rz, salt), c, x, y, z);
            IEnumerable<Vector3[]> Cyl(float r, float h, int n) => Outward(Cylinder(r, r, h, n), p => new Vector3(0, p.y, 0) * (Mathf.Abs(p.y) < h / 2 - 1e-4f ? 1 : 0));
            switch (kind)
            {
                case "bloodPool":
                    Drop(0x604035, 0.04f, 0.04f, 0.38f, 0.29f, 3);
                    Drop(0x651c20, 0.06f, 0.03f, 0.32f, 0.235f, 3, 0.016f);
                    Drop(0x401719, 0.06f, 0.02f, 0.22f, 0.16f, 3, 0.018f);
                    for (int n = 0; n < 8; n++) Drop(n % 2 == 1 ? 0x72272a : 0x58302b, (R(n + 31) - 0.5f) * 0.80f, (R(n + 50) - 0.5f) * 0.82f, 0.012f + R(n + 70) * 0.035f, 0.01f + R(n + 90) * 0.025f, n);
                    break;
                case "bloodDrops":
                    for (int n = 0; n < 4; n++)
                    {
                        float x = n % 2 == 1 ? 0.10f : -0.10f, z = -0.32f + n * 0.20f;
                        Drop(0x68312c, x, z, 0.05f, 0.085f, n);
                        Drop(0x602522, x, z + 0.11f, 0.037f, 0.035f, n + 9);
                    }
                    for (int n = 0; n < 6; n++) Drop(0x752a2b, (R(n + 5) - 0.5f) * 0.7f, (R(n + 15) - 0.5f) * 0.7f, 0.015f, 0.019f, n + 22);
                    break;
                case "wallBlood":
                    Add(Stain(0.28f, 0.20f, 5), 0x68252a, -0.04f, 0.96f, 0.785f, -Mathf.PI / 2);
                    Add(Stain(0.16f, 0.12f, 5), 0x4f1c20, -0.02f, 0.96f, 0.780f, -Mathf.PI / 2);
                    for (int n = 0; n < 9; n++)
                    {
                        float x = (R(n + 11) - 0.5f) * 0.72f, y = 0.64f + R(n + 32) * 0.72f;
                        Add(Stain(0.014f + R(n + 55) * 0.025f, 0.02f, n), 0x742c2d, x, y, 0.778f, -Mathf.PI / 2);
                    }
                    for (int n = 0; n < 4; n++)
                    {
                        float h = 0.10f + R(n + 99) * 0.31f;
                        BoxAt(0x69302b, -0.20f + n * 0.10f, 0.78f - h / 2, 0.778f, 0.014f, h, 0.003f);
                    }
                    break;
                case "floorGrime":
                    Drop(0x827b61, 0.02f, -0.04f, 0.44f, 0.28f, 7, 0.006f);
                    Drop(0x5d5c48, -0.02f, -0.04f, 0.30f, 0.14f, 7, 0.008f);
                    for (int n = 0; n < 4; n++) Drop(0x686954, (R(n + 33) - 0.5f) * 0.20f, -0.15f + n * 0.09f, 0.28f, 0.018f, n + 34, 0.010f);
                    for (int n = 0; n < 8; n++) Drop(0x77735e, (R(n + 3) - 0.5f) * 0.8f, (R(n + 13) - 0.5f) * 0.8f, 0.025f, 0.035f, n, 0.010f);
                    break;
                case "scatteredPapers":
                    for (int n = 0; n < 6; n++)
                    {
                        float x = (R(n + 3) - 0.5f) * 0.65f, z = (R(n + 13) - 0.5f) * 0.64f, yaw = R(n + 42) * 2;
                        var at = T(x, 0.018f + n * 0.002f, z) * Ry(yaw);
                        int start = parts.Count;
                        BoxAt(n == 0 ? 0x8b684e : DebrisWhite, 0, 0, 0, 0.16f, 0.004f, 0.22f);
                        for (int line = 0; line < 4; line++) BoxAt(0x8c9080, 0, 0.003f, -0.05f + line * 0.028f, 0.105f, 0.002f, 0.005f);
                        BoxAt(0x7d5049, -0.04f, 0.004f, -0.08f, 0.05f, 0.002f, 0.015f);
                        for (int p = start; p < parts.Count; p++) parts[p] = (parts[p].faces, parts[p].colour, at * parts[p].at);
                    }
                    break;
                case "spilledSupplies":
                case "medicalWaste":
                    BoxAt(0xa59876, -0.12f, 0.07f, 0.03f, 0.24f, 0.12f, 0.22f, 0.3f);
                    BoxAt(DebrisDark, -0.12f, 0.134f, 0.03f, 0.19f, 0.004f, 0.17f, 0.3f);
                    BoxAt(DebrisWhite, 0.13f, 0.025f, 0.21f, 0.23f, 0.02f, 0.20f, -0.4f);
                    BoxAt(0x885a4e, 0.12f, 0.038f, 0.20f, 0.08f, 0.006f, 0.05f, -0.4f);
                    for (int n = 0; n < 4; n++)
                    {
                        float x = (R(n + 11) - 0.5f) * 0.70f, z = -0.11f - R(n + 21) * 0.21f;
                        if (n % 2 == 1)
                        {
                            Add(Cyl(0.026f, 0.11f, 7), n == 1 ? 0x9d7846 : 0xaab6a5, x, 0.035f, z, Mathf.PI / 2, R(n) * 2);
                            BoxAt(0x597778, x + 0.05f, 0.025f, z, 0.03f, 0.025f, 0.035f);
                        }
                        else Add(Transformed(Outward(Torus(0.043f, 0.017f, 5, 9), Ring(0.043f)), Rx(Mathf.PI / 2)), DebrisWhite, x, 0.024f, z);
                    }
                    if (kind == "medicalWaste")
                    {
                        Add(Outward(Icosahedron(0.12f, new Vector3(1.3f, 0.6f, 1)), p => Vector3.zero), 0x777f68, 0.20f, 0.08f, -0.02f);
                        BoxAt(0x969a83, 0.20f, 0.16f, -0.02f, 0.035f, 0.07f, 0.035f);
                    }
                    break;
                case "rumpledLinen":
                    for (int n = 0; n < 5; n++) Add(Outward(Icosahedron(0.16f, new Vector3(1.4f, 0.25f, 0.85f)), p => Vector3.zero), n % 2 == 1 ? 0xabae9c : 0xc6c5b1, -0.20f + n * 0.09f, 0.04f + n % 2 * 0.02f, (R(n) - 0.5f) * 0.15f, 0, n * 0.4f);
                    Drop(0x7e5148, 0.06f, 0.02f, 0.08f, 0.04f, 3, 0.088f);
                    break;
                case "overturnedChair":
                    foreach (float x in new[] { -0.17f, 0.17f })
                        foreach (float z in new[] { -0.15f, 0.15f }) BoxAt(DebrisMetal, x, 0.17f, z, 0.025f, 0.32f, 0.025f);
                    BoxAt(0x526c68, 0, 0.34f, 0, 0.41f, 0.055f, 0.38f);
                    BoxAt(0x526c68, 0, 0.54f, 0.17f, 0.41f, 0.36f, 0.045f);
                    var over = T(-0.30f, 0.23f, 0) * Rz(-Mathf.PI / 2);
                    for (int p = 0; p < parts.Count; p++) parts[p] = (parts[p].faces, parts[p].colour, over * parts[p].at);
                    break;
                case "fallenIV":
                    Add(Cyl(0.011f, 1.02f, 6), DebrisMetal, 0, 0.09f, 0, 0, 0, Mathf.PI / 2);
                    BoxAt(DebrisMetal, 0.40f, 0.09f, 0, 0.035f, 0.035f, 0.30f);
                    foreach (float z in new[] { -0.15f, 0.15f }) BoxAt(DebrisDark, 0.40f, 0.06f, z, 0.08f, 0.06f, 0.04f);
                    BoxAt(DebrisMetal, -0.44f, 0.10f, 0.04f, 0.025f, 0.025f, 0.14f);
                    BoxAt(0xb5c6b8, -0.30f, 0.055f, 0.15f, 0.19f, 0.06f, 0.11f);
                    BoxAt(DebrisWhite, -0.30f, 0.09f, 0.15f, 0.09f, 0.005f, 0.06f);
                    Add(Tube(new[] { new Vector3(-0.20f, 0.035f, 0.15f), new Vector3(-0.10f, 0.03f, 0.30f), new Vector3(0.12f, 0.025f, 0.24f) }, 8, 0.006f, 4), 0xbac1af, 0, 0, 0);
                    for (int p = 0; p < parts.Count; p++) parts[p] = (parts[p].faces, parts[p].colour, Ry(Mathf.PI / 4) * parts[p].at);
                    break;
            }
            foreach (var (faces, colour, at) in parts) Put(to, faces, colour, at);
        }

        /// <summary>TubeGeometry over a Catmull–Rom curve through `points` (its ends held), a few rings round it.</summary>
        static IEnumerable<Vector3[]> Tube(Vector3[] points, int segments, float radius, int radial)
        {
            Vector3 On(float t)
            {
                float f = t * (points.Length - 1);
                int i = Mathf.Min(points.Length - 2, (int)f);
                float u = f - i;
                Vector3 p0 = points[Mathf.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(points.Length - 1, i + 2)];
                return 0.5f * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u);
            }
            var rings = new List<Vector3[]>();
            for (int s = 0; s <= segments; s++)
            {
                float t = (float)s / segments;
                var at = On(t);
                var along = (On(Mathf.Min(1, t + 0.01f)) - On(Mathf.Max(0, t - 0.01f))).normalized;
                var side = Vector3.Cross(along, Vector3.up).normalized;
                var up = Vector3.Cross(side, along);
                rings.Add(Enumerable.Range(0, radial).Select(k => at + (side * Mathf.Cos(k * Mathf.PI * 2 / radial) + up * Mathf.Sin(k * Mathf.PI * 2 / radial)) * radius).ToArray());
            }
            var faces = new List<Vector3[]>();
            for (int s = 0; s < segments; s++)
                for (int k = 0; k < radial; k++) faces.Add(new[] { rings[s][k], rings[s + 1][k], rings[s + 1][(k + 1) % radial], rings[s][(k + 1) % radial] });
            // Away from the curve's middle line, ring by ring.
            return faces.Select((f, n) =>
            {
                var middle = (rings[n / radial].Aggregate(Vector3.zero, (a, b) => a + b) + rings[n / radial + 1].Aggregate(Vector3.zero, (a, b) => a + b)) / (2 * radial);
                var normal = Vector3.Cross(f[1] - f[0], f[2] - f[0]);
                var mid = f.Aggregate(Vector3.zero, (a, b) => a + b) / 4;
                return Vector3.Dot(normal, mid - middle) < 0 ? f.Reverse().ToArray() : f;
            });
        }
    }
}
