using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The map from above (Arturo, 31/10 #12; 01/11: «el minimapa es muy minimapa»): M or the map's key by «Menú» take
    /// the camera straight up over the map, north up and far enough for all the squad has explored (the whole map with no
    /// fog), with what the fog lets be seen; a click on the ground brings it back down there, and M or Esc back where it
    /// was. Up there the marks of a move go, a click orders nothing, and nothing else moves the camera (CameraRig.Overhead).
    /// </summary>
    public sealed partial class CombatController
    {
        /// <summary>The share of the screen's height the map takes from above (the rest, the turn's strip and the bar).</summary>
        const float OverheadRoom = 0.86f;
        /// <summary>Tiles round what has been explored, and the least it frames (tiles a side).</summary>
        const int OverheadMargin = 2;
        const int OverheadLeast = 20;

        bool Overhead => Rig != null && Rig.Overhead;

        public void OnMiniMap() => ToggleMiniMap();

        void ToggleMiniMap()
        {
            if (Rig == null || State == null) return;
            if (Overhead) Rig.LeaveOverhead();
            else
            {
                // Not over a shoulder: an aim let go first.
                if (mode == Mode.Target) CancelMode();
                var box = Explored();
                var w = box.width * Board.Tile;
                var h = box.height * Board.Tile;
                var tan = Mathf.Tan(Rig.Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                var metres = Mathf.Max(h / 2 / tan, w / 2 / (tan * Rig.Camera.aspect)) / OverheadRoom;
                Rig.EnterOverhead(new Vector3(box.center.x * Board.Tile, 0, -box.center.y * Board.Tile), metres);
                hover = null;
                ShowWay();
                RefreshFlags();
            }
            Hud?.SetMiniMap(Overhead);
        }

        /// <summary>
        /// What the squad has explored on the ground (Arte's Fog) and where its soldiers stand, in tiles (x east, y south),
        /// a margin round it, at least OverheadLeast a side, within the map; with no fog, the whole map.
        /// </summary>
        Rect Explored()
        {
            var s = State;
            var fog = Bastion.Client.Map.Fog.Current;
            int x0 = s.Width, y0 = s.Height, x1 = -1, y1 = -1;
            void Take(int x, int y)
            {
                x0 = Mathf.Min(x0, x);
                y0 = Mathf.Min(y0, y);
                x1 = Mathf.Max(x1, x);
                y1 = Mathf.Max(y1, y);
            }
            if (fog == null) return new Rect(0, 0, s.Width, s.Height);
            for (var i = 0; i < s.Width * s.Height; i++)
                if (fog.Explored(i)) Take(i % s.Width, i / s.Width);
            foreach (var u in s.Units)
                if (u.Alive && u.Team == Bastion.Engine.Teams.Xcom && !u.Evacuated) Take(u.Pos.X, u.Pos.Y);
            if (x1 < 0) return new Rect(0, 0, s.Width, s.Height);
            var w = Mathf.Max(OverheadLeast, x1 - x0 + 1 + 2 * OverheadMargin);
            var h = Mathf.Max(OverheadLeast, y1 - y0 + 1 + 2 * OverheadMargin);
            var cx = Mathf.Clamp((x0 + x1 + 1) / 2f, w / 2f, s.Width - w / 2f);
            var cy = Mathf.Clamp((y0 + y1 + 1) / 2f, h / 2f, s.Height - h / 2f);
            return new Rect(cx - w / 2f, cy - h / 2f, w, h);
        }

        /// <summary>Esc: back down from above, if up there (then Esc does nothing else).</summary>
        bool CloseMiniMap()
        {
            if (!Overhead) return false;
            ToggleMiniMap();
            return true;
        }

        /// <summary>A click on the map from above: the camera back down, over that tile.</summary>
        internal void OverheadClick(Bastion.Engine.Pos tile)
        {
            if (!Overhead || tile == null) return;
            Rig.LeaveOverhead(Board.TileToWorld(Shown, tile));
            Hud?.SetMiniMap(false);
        }
    }
}
