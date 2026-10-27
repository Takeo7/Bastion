using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// A window barricaded from inside (mapView.ts barricadeWindow; playtest 21/10 #20, Ana: furniture piled up so the dead
    /// don't get in), on the tile in front of it: planks nailed across the window frame, and against them what the room
    /// had, a wardrobe pushed up to them, a table on its side, its top to the window, or a mattress stood up leaning on
    /// them; a box or two at their foot. From the street, the planks behind the glass. 0.10.1's matrices, in tiles.
    /// </summary>
    public sealed partial class MapBuilder
    {
        /// <summary>mapView.ts WINDOW_FACE, WINDOW_SILL: how far the wall's inner face is from the middle of the tile inside it, how high the sill is (tiles).</summary>
        const float WindowFace = 1 - 0.1f * 1.75f * (1 / 1.5f) * 1.2f, WindowSill = 0.62f;

        static readonly string[] Boards = { "boardsCross", "boardsMany", "boardsRails" };

        /// <summary>Tile i of storey k, (x, y), barricaded against the window on its side `facing` (0.10.1's DIRS4).</summary>
        void BarricadeWindow(int k, int i, int x, int y, float floor, int facing)
        {
            var d = Bastion.Engine.Grid.Dirs4[facing];
            // This tile's frame: z out to the window, x along the wall.
            var frame = Matrix4x4.TRS(new Vector3(x + 0.5f, floor / MapSpace.Tile, y + 0.5f), Quaternion.AngleAxis(Mathf.Atan2(d.X, d.Y) * Mathf.Rad2Deg, Vector3.up), Vector3.one);
            float R(int n) => MapArt.Hash(x, y, 70 + n);
            void Put(string id, Matrix4x4 m) => Own(PlaceGame(props[k], id, frame * m), k, new[] { i });
            Matrix4x4 T(float tx, float ty, float tz) => Matrix4x4.Translate(new Vector3(tx, ty, tz));
            Matrix4x4 Turn(Vector3 axis, float radians) => Matrix4x4.Rotate(Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis));
            float face = WindowFace - 0.05f;
            Put(Boards[Mathf.Min(2, Mathf.FloorToInt(R(0) * 3))], T((R(1) - 0.5f) * 0.06f, WindowSill, face) * Turn(Vector3.forward, (R(2) - 0.5f) * 0.06f));
            float pile = R(3), against = face - 0.05f;
            if (pile < 0.4f)
                // The wardrobe, its back to the window, its doors to the room.
                Put("wardrobe", T((R(4) - 0.5f) * 0.12f, 0, against - 0.23f) * Turn(Vector3.up, (R(5) - 0.5f) * 0.12f));
            else if (pile < 0.7f)
                // The table tipped onto its edge, its top against the planks, its legs to the room.
                Put("table", T((R(4) - 0.5f) * 0.1f, 0.43f, against - 0.58f) * Turn(Vector3.right, Mathf.PI / 2));
            else
            {
                // The mattress stood on its end, leaning on the planks.
                const float lean = 0.2f;
                Put("mattress", T((R(4) - 0.5f) * 0.2f, 0, against - 1.28f * Mathf.Sin(lean) - 0.02f) * Turn(Vector3.right, lean) * T(0, 0.64f, 0) * Turn(Vector3.right, -Mathf.PI / 2));
            }
            // A box or two at their foot, or none.
            int boxes = Mathf.FloorToInt(R(6) * 3);
            for (int n = 0; n < boxes; n++)
            {
                float side = (n > 0 ? -1 : 1) * (0.28f + R(7 + n) * 0.1f);
                Put(n > 0 ? "cardboard2" : "cardboard", T(side, 0, against - 0.62f - R(9 + n) * 0.12f) * Turn(Vector3.up, R(11 + n) * Mathf.PI));
            }
        }
    }
}
