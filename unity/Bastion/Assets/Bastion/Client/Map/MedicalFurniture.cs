using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// A clinic's own furniture where no pack has a model (0.10.1's medicalFurniture.ts, low-poly, each fitting its single
    /// cover tile): a medical cabinet, a cart, a wheelchair, a reception or a consulting desk, waiting seats, an examination
    /// table, a monitor, an X-ray stand, a bedside table, a visitor's armchair, a sink, a sterilizer, an ultrasound, a
    /// waiting room's table, a water dispenser, a toilet, a clinical bin. As MedicalDetails' faces, in the tile's frame.
    /// </summary>
    public static partial class MedicalDetails
    {
        static readonly HashSet<string> FurnitureKinds = new HashSet<string>
        {
            "medicalCabinet", "medicalCart", "wheelchair", "receptionDesk", "waitingSeats", "examTable", "medicalMonitor", "xray", "bedsideTable",
            "visitorChair", "clinicalSink", "sterilizer", "ultrasound", "waitingTable", "waterDispenser", "toilet", "clinicalBin", "consultDesk",
        };

        /// <summary>medicalFurniture.ts: whether a kind is one of these, and the tile it stands on (the tall ones high cover, the rest low).</summary>
        public static bool IsFurniture(string kind) => kind != null && FurnitureKinds.Contains(kind);
        public static bool FurnitureHigh(string kind) => kind == "medicalCabinet" || kind == "xray" || kind == "waterDispenser";

        const int FMetal = 0x8b9c9c, FWhite = 0xdedfd5, FRubber = 0x353d40, FBlue = 0x416b78;

        /// <summary>A kind's faces (`black`: its dark variant, the wheelchair's seat, the armchair's fabric); null if it isn't one.</summary>
        public static IReadOnlyList<Face> Furniture(string kind, bool black = false)
        {
            if (!IsFurniture(kind)) return null;
            string key = $"furniture:{kind}:{black}";
            if (cache.TryGetValue(key, out var ready)) return ready;
            var to = new List<Face>();
            void Add(IEnumerable<Vector3[]> g, int colour, float x, float y, float z, float rz = 0) => Put(to, g, colour, T(x, y, z) * Rz(rz));
            void B(int c, float x, float y, float z, float w, float h, float d) => Add(Box(w, h, d), c, x, y, z);
            void Wheel(float x, float y, float z, float radius)
            {
                Add(Cylinder(radius, radius, 0.05f, 10), FRubber, x, y, z, Mathf.PI / 2);
                Add(Cylinder(radius * 0.68f, radius * 0.68f, 0.055f, 10), FMetal, x, y, z, Mathf.PI / 2);
            }
            switch (kind)
            {
                case "medicalCabinet":
                    B(FMetal, 0, 0.09f, 0, 0.65f, 0.14f, 0.42f);
                    B(FWhite, 0, 0.72f, 0, 0.66f, 1.18f, 0.44f);
                    foreach (float x in new[] { -0.16f, 0.16f })
                    {
                        B(0xb5c8c5, x, 0.91f, -0.229f, 0.27f, 0.65f, 0.018f);
                        B(FMetal, x + (x < 0 ? 0.09f : -0.09f), 0.85f, -0.25f, 0.018f, 0.15f, 0.025f);
                        foreach (float y in new[] { 0.69f, 0.96f, 1.18f }) B(FWhite, x, y, -0.245f, 0.27f, 0.02f, 0.02f);
                        B(0xc7cac1, x, 0.35f, -0.233f, 0.29f, 0.29f, 0.025f);
                    }
                    B(0x91453c, 0, 1.23f, -0.245f, 0.13f, 0.045f, 0.02f);
                    B(0x91453c, 0, 1.23f, -0.25f, 0.045f, 0.13f, 0.02f);
                    foreach (float y in new[] { 0.72f, 0.99f })
                        for (int n = 0; n < 5; n++)
                        {
                            float x = -0.25f + n * 0.12f;
                            B(n % 2 == 1 ? 0xb08c50 : 0xe1e8dc, x, y, -0.26f, 0.055f, 0.11f, 0.025f);
                            B(FBlue, x, y + 0.06f, -0.26f, 0.055f, 0.025f, 0.03f);
                        }
                    break;
                case "medicalCart":
                    foreach (float x in new[] { -0.25f, 0.25f })
                        foreach (float z in new[] { -0.18f, 0.18f })
                        {
                            Wheel(x, 0.07f, z, 0.065f);
                            B(FMetal, x, 0.36f, z, 0.025f, 0.56f, 0.025f);
                        }
                    foreach (float y in new[] { 0.20f, 0.56f })
                    {
                        B(FWhite, 0, y, 0, 0.57f, 0.035f, 0.42f);
                        B(FMetal, 0, y + 0.035f, 0.20f, 0.57f, 0.055f, 0.02f);
                    }
                    B(FWhite, 0, 0.43f, 0, 0.48f, 0.18f, 0.34f);
                    B(FMetal, 0, 0.43f, -0.18f, 0.15f, 0.025f, 0.02f);
                    B(FBlue, -0.13f, 0.24f, 0, 0.18f, 0.06f, 0.26f);
                    foreach (float x in new[] { 0.12f, 0.20f })
                    {
                        Add(Cylinder(0.025f, 0.025f, 0.10f, 6), 0xe1d5b6, x, 0.63f, 0.10f);
                        Add(Cylinder(0.027f, 0.027f, 0.025f, 6), FBlue, x, 0.69f, 0.10f);
                    }
                    B(FMetal, 0.31f, 0.64f, 0, 0.025f, 0.025f, 0.40f);
                    B(FMetal, -0.10f, 0.59f, -0.08f, 0.22f, 0.025f, 0.17f);
                    foreach (float x in new[] { -0.15f, -0.10f }) B(FRubber, x, 0.61f, -0.08f, 0.018f, 0.012f, 0.13f);
                    break;
                case "receptionDesk":
                case "consultDesk":
                    bool consult = kind == "consultDesk";
                    B(FWhite, 0, consult ? 0.28f : 0.37f, 0.08f, 0.85f, consult ? 0.50f : 0.72f, 0.35f);
                    B(FMetal, 0, 0.75f, 0, 0.92f, 0.055f, 0.65f);
                    B(FBlue, 0, 0.53f, -0.105f, 0.80f, 0.09f, 0.025f);
                    B(FMetal, 0.17f, 0.84f, 0.08f, 0.045f, 0.16f, 0.04f);
                    B(FRubber, 0.17f, 0.96f, 0.08f, 0.29f, 0.21f, 0.045f);
                    B(0x75a3a5, 0.17f, 0.96f, 0.053f, 0.245f, 0.165f, 0.012f);
                    B(0xf5f0dd, -0.22f, 0.79f, -0.05f, 0.19f, 0.025f, 0.26f);
                    B(FBlue, -0.23f, 0.40f, 0.40f, 0.30f, 0.06f, 0.24f);
                    B(FRubber, -0.23f, 0.56f, 0.48f, 0.30f, 0.29f, 0.035f);
                    B(FRubber, -0.12f, 0.79f, 0.12f, 0.13f, 0.025f, 0.08f);
                    if (consult)
                    {
                        B(FBlue, 0.19f, 0.33f, -0.40f, 0.28f, 0.05f, 0.26f);
                        B(FBlue, 0.19f, 0.49f, -0.49f, 0.28f, 0.28f, 0.035f);
                    }
                    break;
                case "waitingSeats":
                    B(FMetal, 0, 0.22f, 0, 0.83f, 0.04f, 0.06f);
                    foreach (float x in new[] { -0.30f, 0, 0.30f })
                    {
                        B(FBlue, x, 0.31f, 0, 0.27f, 0.06f, 0.30f);
                        B(FBlue, x, 0.49f, 0.14f, 0.27f, 0.32f, 0.04f);
                    }
                    foreach (float x in new[] { -0.32f, 0.32f })
                    {
                        B(FMetal, x, 0.13f, 0.05f, 0.04f, 0.23f, 0.04f);
                        B(FMetal, x, 0.03f, 0.05f, 0.13f, 0.04f, 0.30f);
                    }
                    break;
                case "examTable":
                    foreach (float x in new[] { -0.21f, 0.21f })
                        foreach (float z in new[] { -0.30f, 0.30f }) B(FMetal, x, 0.28f, z, 0.045f, 0.50f, 0.045f);
                    B(FWhite, 0, 0.42f, 0, 0.51f, 0.20f, 0.77f);
                    B(FBlue, 0, 0.56f, -0.13f, 0.55f, 0.09f, 0.54f);
                    B(FBlue, 0, 0.64f, 0.27f, 0.55f, 0.12f, 0.25f);
                    B(0xf3eedf, 0, 0.71f, 0.27f, 0.45f, 0.025f, 0.19f);
                    B(FMetal, 0.32f, 0.25f, -0.23f, 0.16f, 0.03f, 0.17f);
                    Add(Cylinder(0.055f, 0.055f, 0.48f, 10), FWhite, 0, 0.48f, -0.41f, Mathf.PI / 2);
                    B(0xf3eedf, 0, 0.615f, -0.25f, 0.45f, 0.012f, 0.27f);
                    break;
                case "medicalMonitor":
                    foreach (float x in new[] { -0.19f, 0.19f })
                        foreach (float z in new[] { -0.16f, 0.16f }) Wheel(x, 0.055f, z, 0.05f);
                    B(FMetal, 0, 0.40f, 0, 0.05f, 0.73f, 0.05f);
                    B(FWhite, 0, 0.62f, 0, 0.50f, 0.09f, 0.35f);
                    B(FWhite, 0, 0.84f, 0.04f, 0.43f, 0.34f, 0.16f);
                    B(FRubber, 0, 0.85f, -0.048f, 0.35f, 0.25f, 0.016f);
                    for (int n = 0; n < 8; n++) B(0x69ae91, -0.14f + n * 0.04f, 0.85f + (n % 3 == 0 ? 0.045f : -0.015f), -0.061f, 0.045f, 0.012f, 0.01f);
                    B(FBlue, 0.18f, 0.63f, -0.08f, 0.05f, 0.02f, 0.04f);
                    break;
                case "xray":
                    B(FMetal, 0, 0.055f, 0.12f, 0.68f, 0.09f, 0.55f);
                    B(FWhite, 0, 0.77f, 0.28f, 0.12f, 1.40f, 0.12f);
                    B(FWhite, 0, 0.85f, 0.15f, 0.62f, 0.78f, 0.14f);
                    B(0x65787b, 0, 0.85f, 0.067f, 0.49f, 0.62f, 0.015f);
                    B(0xb0c3c0, 0, 0.84f, 0.052f, 0.04f, 0.35f, 0.012f);
                    foreach (float y in new[] { 0.74f, 0.83f, 0.92f }) B(0xb0c3c0, 0, y, 0.05f, 0.25f, 0.018f, 0.015f);
                    B(FMetal, 0, 1.44f, 0.03f, 0.08f, 0.06f, 0.52f);
                    B(FWhite, 0, 1.29f, -0.21f, 0.25f, 0.25f, 0.24f);
                    B(FRubber, 0, 1.14f, -0.21f, 0.13f, 0.06f, 0.13f);
                    break;
                case "bedsideTable":
                    B(FWhite, 0, 0.28f, 0, 0.39f, 0.48f, 0.39f);
                    B(FMetal, 0, 0.54f, 0, 0.42f, 0.035f, 0.42f);
                    foreach (float y in new[] { 0.22f, 0.40f })
                    {
                        B(0xb8c6c2, 0, y, -0.205f, 0.34f, 0.15f, 0.025f);
                        B(FMetal, 0, y, -0.23f, 0.10f, 0.02f, 0.03f);
                    }
                    Add(Cylinder(0.035f, 0.03f, 0.09f, 8), 0xc1d5d8, -0.10f, 0.60f, 0.05f);
                    B(FBlue, 0.10f, 0.58f, -0.06f, 0.11f, 0.035f, 0.14f);
                    break;
                case "visitorChair":
                    // A padded visitor armchair, as in patient rooms and waiting areas.
                    int fabric = black ? 0x776349 : 0x6b8178;
                    foreach (float x in new[] { -0.20f, 0.20f })
                        foreach (float z in new[] { -0.19f, 0.19f }) B(FMetal, x, 0.10f, z, 0.04f, 0.18f, 0.04f);
                    B(fabric, 0, 0.25f, 0, 0.57f, 0.20f, 0.53f);
                    B(fabric, 0, 0.38f, -0.03f, 0.43f, 0.12f, 0.43f);
                    B(fabric, 0, 0.58f, 0.21f, 0.57f, 0.55f, 0.14f);
                    foreach (float x in new[] { -0.255f, 0.255f }) B(fabric, x, 0.45f, 0, 0.11f, 0.30f, 0.53f);
                    break;
                case "clinicalSink":
                    B(FWhite, 0, 0.35f, 0.08f, 0.64f, 0.62f, 0.43f);
                    B(FWhite, 0, 0.69f, 0.02f, 0.70f, 0.08f, 0.53f);
                    B(0x728a8a, 0, 0.735f, -0.02f, 0.44f, 0.012f, 0.30f);
                    B(0xc8dbd8, 0, 0.74f, -0.02f, 0.34f, 0.014f, 0.22f);
                    B(FMetal, 0, 0.82f, 0.21f, 0.025f, 0.22f, 0.025f);
                    B(FMetal, 0, 0.92f, 0.15f, 0.025f, 0.025f, 0.15f);
                    B(FWhite, -0.22f, 1.08f, 0.28f, 0.20f, 0.25f, 0.10f);
                    B(FBlue, -0.22f, 1.01f, 0.215f, 0.08f, 0.06f, 0.035f);
                    B(0xf0ecda, 0.21f, 0.80f, 0.11f, 0.08f, 0.16f, 0.07f);
                    B(FMetal, 0.21f, 0.89f, 0.11f, 0.08f, 0.025f, 0.05f);
                    break;
                case "sterilizer":
                    B(FWhite, 0, 0.31f, 0.04f, 0.78f, 0.58f, 0.53f);
                    B(FMetal, 0, 0.63f, 0, 0.85f, 0.05f, 0.59f);
                    B(0xb9c5c2, -0.16f, 0.83f, 0.08f, 0.41f, 0.35f, 0.37f);
                    B(FRubber, -0.16f, 0.83f, -0.113f, 0.29f, 0.23f, 0.02f);
                    B(0x668e8c, -0.16f, 0.83f, -0.126f, 0.22f, 0.15f, 0.01f);
                    foreach (float x in new[] { -0.27f, -0.08f }) B(FMetal, x, 0.99f, -0.12f, 0.05f, 0.025f, 0.03f);
                    B(FMetal, 0.24f, 0.68f, -0.05f, 0.21f, 0.03f, 0.25f);
                    foreach (float x in new[] { 0.18f, 0.24f }) B(FRubber, x, 0.70f, -0.05f, 0.015f, 0.015f, 0.17f);
                    break;
                case "ultrasound":
                    foreach (float x in new[] { -0.22f, 0.22f })
                        foreach (float z in new[] { -0.19f, 0.19f }) Wheel(x, 0.07f, z, 0.06f);
                    B(FWhite, 0, 0.37f, 0.05f, 0.43f, 0.53f, 0.37f);
                    B(FWhite, 0, 0.69f, 0, 0.60f, 0.13f, 0.48f);
                    B(FMetal, 0, 0.88f, 0.12f, 0.06f, 0.35f, 0.06f);
                    B(FWhite, 0, 1.04f, 0.07f, 0.48f, 0.32f, 0.10f);
                    B(FRubber, 0, 1.04f, 0.012f, 0.40f, 0.25f, 0.015f);
                    B(0x9aac9d, 0, 1.03f, 0, 0.23f, 0.16f, 0.01f);
                    B(0xc7d9d5, 0, 0.77f, -0.13f, 0.34f, 0.025f, 0.15f);
                    Add(Tube(new[] { new Vector3(0.24f, 0.70f, 0.06f), new Vector3(0.37f, 0.29f, 0), new Vector3(0.31f, 0.75f, -0.13f) }, 10, 0.009f, 4), FRubber, 0, 0, 0);
                    B(FWhite, 0.31f, 0.80f, -0.13f, 0.06f, 0.13f, 0.045f);
                    break;
                case "waitingTable":
                    foreach (float x in new[] { -0.20f, 0.20f })
                        foreach (float z in new[] { -0.15f, 0.15f }) B(FMetal, x, 0.18f, z, 0.025f, 0.32f, 0.025f);
                    B(FWhite, 0, 0.36f, 0, 0.53f, 0.055f, 0.42f);
                    int[] covers = { FBlue, 0xa16c4d, 0xc2bb77 };
                    for (int n = 0; n < 3; n++) B(covers[n], -0.15f + n * 0.14f, 0.40f, -0.02f, 0.11f, 0.015f, 0.20f);
                    B(FWhite, 0.15f, 0.43f, 0.10f, 0.12f, 0.10f, 0.10f);
                    break;
                case "waterDispenser":
                    B(FWhite, 0, 0.47f, 0, 0.36f, 0.84f, 0.36f);
                    B(FRubber, 0, 0.62f, -0.19f, 0.25f, 0.23f, 0.025f);
                    B(FMetal, 0, 0.52f, -0.24f, 0.23f, 0.025f, 0.14f);
                    foreach (float x in new[] { -0.07f, 0.07f }) B(x < 0 ? FBlue : 0x98443b, x, 0.69f, -0.23f, 0.05f, 0.045f, 0.07f);
                    Add(Cylinder(0.15f, 0.12f, 0.29f, 10), 0x719fac, 0, 1.05f, 0);
                    B(FBlue, 0, 0.89f, 0, 0.13f, 0.05f, 0.13f);
                    break;
                case "toilet":
                    B(FWhite, 0, 0.24f, 0, 0.28f, 0.39f, 0.36f);
                    Add(Cylinder(0.21f, 0.14f, 0.12f, 12), FWhite, 0, 0.43f, -0.05f);
                    Put(to, Transformed(Torus(0.15f, 0.035f, 6, 12), Rx(Mathf.PI / 2)), 0x9dada8, T(0, 0.50f, -0.05f));
                    B(FWhite, 0, 0.52f, 0.22f, 0.40f, 0.43f, 0.18f);
                    B(FMetal, 0.10f, 0.75f, 0.22f, 0.06f, 0.015f, 0.04f);
                    break;
                case "clinicalBin":
                    B(0xcbb146, 0, 0.24f, 0, 0.27f, 0.42f, 0.28f);
                    B(FRubber, 0, 0.47f, 0, 0.31f, 0.045f, 0.32f);
                    B(FMetal, 0, 0.06f, -0.18f, 0.11f, 0.025f, 0.12f);
                    B(FRubber, 0, 0.28f, -0.145f, 0.13f, 0.025f, 0.012f);
                    B(FRubber, 0, 0.28f, -0.146f, 0.025f, 0.13f, 0.012f);
                    break;
                default:
                    // The wheelchair.
                    foreach (float x in new[] { -0.28f, 0.28f })
                    {
                        Wheel(x, 0.26f, 0.12f, 0.25f);
                        Wheel(x, 0.08f, -0.30f, 0.075f);
                        B(FMetal, x, 0.28f, -0.09f, 0.025f, 0.025f, 0.48f);
                        B(FMetal, x, 0.46f, 0.20f, 0.025f, 0.63f, 0.025f);
                        B(FRubber, x, 0.57f, -0.03f, 0.05f, 0.035f, 0.32f);
                        B(FMetal, x * 0.7f, 0.19f, -0.35f, 0.15f, 0.03f, 0.15f);
                    }
                    B(black ? 0x242b2d : FBlue, 0, 0.36f, 0, 0.45f, 0.055f, 0.42f);
                    B(black ? 0x242b2d : FBlue, 0, 0.56f, 0.20f, 0.45f, 0.40f, 0.055f);
                    B(FMetal, 0, 0.27f, 0.15f, 0.56f, 0.025f, 0.025f);
                    break;
            }
            cache[key] = to;
            return to;
        }
    }
}
