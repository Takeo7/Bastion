using System.Collections.Generic;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The shot over a shoulder kept clear (unitViews.ts clearView, playtest 20/10 #11): while it is up, everyone but the
    /// attacker and its target who stands near the camera, or across its view of the target short of the last stretch
    /// (the target's own neighbours stay), goes out of its way (Arte's UnitViews.Veil); the shot over, all back.
    /// </summary>
    public sealed partial class CombatController
    {
        /// <summary>
        /// Out of the way (unitViews.ts VEIL_*, its lengths in tiles, here in metres as ShotCam's): nearer the camera than
        /// this to a body this tall, or this near its view of the target, short of the last stretch.
        /// </summary>
        const float VeilNear = 1.3f * Board.Tile;
        const float VeilTall = 1.5f * Board.Tile;
        const float VeilAcross = 0.45f * Board.Tile;
        const float VeilSpare = 1.2f * Board.Tile;

        /// <summary>Who is out of the way now.</summary>
        readonly HashSet<string> veiled = new HashSet<string>();
        bool shoulder;

        /// <summary>Each frame: who stands in the shot's way, out of it; anyone no longer in it, back.</summary>
        void ClearView()
        {
            var units = UnitViews.Current;
            if (units == null || Shown == null) return;
            var close = CloseFocus?.Invoke();
            var camera = Rig != null ? Rig.Camera : null;
            var on = close != null && !string.IsNullOrEmpty(close.Value.target) && camera != null;
            if (on != shoulder)
            {
                shoulder = on;
                if (on) Debug.Log($"[hombro] plano de {close.Value.attacker} a {close.Value.target}");
            }
            if (!on && veiled.Count == 0) return;
            var eye = on ? camera.transform.position : Vector3.zero;
            var look = on ? units.Chest(close.Value.target) : Vector3.zero;
            var view = look - eye;
            var reach = view.magnitude;
            foreach (var u in Shown.Units)
            {
                var veil = false;
                if (on && u.Alive && u.Id != close.Value.attacker && u.Id != close.Value.target && FeetOf(u.Id) is Vector3 root)
                {
                    // Its body, from the feet up: how near the camera comes to it.
                    var y = Mathf.Clamp(eye.y, root.y, root.y + VeilTall);
                    veil = Vector3.Distance(eye, new Vector3(root.x, y, root.z)) < VeilNear;
                    if (!veil && reach > 0)
                    {
                        var chest = units.Chest(u.Id);
                        var t = Mathf.Clamp01(Vector3.Dot(chest - eye, view) / (reach * reach));
                        veil = t * reach < reach - VeilSpare && Vector3.Distance(eye + view * t, chest) < VeilAcross;
                    }
                }
                if (veil == veiled.Contains(u.Id)) continue;
                units.Veil(u.Id, veil);
                if (veil)
                {
                    veiled.Add(u.Id);
                    Debug.Log($"[hombro] {u.Name} fuera del plano");
                }
                else veiled.Remove(u.Id);
            }
            // Anyone gone from the state meanwhile (boarded, risen as another) back as well.
            if (!on && veiled.Count > 0)
            {
                foreach (var id in veiled) units.Veil(id, false);
                veiled.Clear();
            }
        }

        bool flagsClose;

        /// <summary>
        /// Over a shoulder, aiming or the attack that kills (game.ts: rig.closeUp, `close-shot`), the flags go but the
        /// target's (Arte: the names and bars cluttered it), and so do the tags over the map.
        /// </summary>
        void CloseFlags()
        {
            var on = Rig != null && Rig.CloseUp;
            if (on == flagsClose || Hud == null) return;
            flagsClose = on;
            Hud.SetCloseShot(on);
        }

        /// <summary>The aim's shot over the shoulder now: who aims at whom from where; empty, none (game.ts aimCam).</summary>
        string aimCam = "";

        /// <summary>
        /// Aiming at an enemy (Ana, 22/10 #19; game.ts updateAimCam): with a target picked that the squad sees, the camera
        /// goes over the shooter's shoulder at it (ShotCam.OverShoulder, as the attack that kills); another target, another
        /// shot; let go, back to where it was, unless the attack's own shot over the shoulder has taken over.
        /// </summary>
        void AimCam(Unit u, Unit target)
        {
            var units = UnitViews.Current;
            var on = u != null && target != null && target.Team == Teams.Alien && units != null && (Visible?.Invoke(target) ?? true);
            var key = on ? $"{u.Id}:{target.Id}:{u.Pos.X},{u.Pos.Y},{u.Pos.Z}" : "";
            if (key == aimCam || Rig == null) return;
            var was = aimCam != "";
            aimCam = key;
            if (!on)
            {
                if (was && CloseFocus?.Invoke()?.target == null) Rig.EndCloseShot();
                return;
            }
            var shot = AimShot(units, u, target);
            if (shot != null) Rig.CloseShot(shot.Value.pos, shot.Value.look);
            else if (was) Rig.EndCloseShot();
        }

        /// <summary>
        /// The aim's shot (game.ts aimShot): from in cover (a car, a wall to the chest) a shoulder's height doesn't see over
        /// it (Ana, 23/10 #22): then from higher, a little at a time, looking down over the cover; one with somebody else
        /// across its view of the target (the squad bunched where it set off), higher still, over their heads.
        /// </summary>
        (Vector3 pos, Vector3 look)? AimShot(UnitViews units, Unit u, Unit target)
        {
            var from = units.Chest(u.Id);
            var to = units.Chest(target.Id);
            (Vector3 pos, Vector3 look)? first = null;
            foreach (var rise in new[] { 0f, 0.6f, 1.2f, 1.8f, 2.6f })
            {
                var shot = ShotCam.OverShoulder(Shown, from + Vector3.up * (rise * Board.Tile), to, u.Id, target.Id);
                if (shot == null) continue;
                if (!BodyInWay(shot.Value.pos, to, u.Id, target.Id)) return shot;
                first ??= shot;
            }
            return first;
        }

        /// <summary>Whether a body in sight (but `skip`'s) stands across the line from `a` to `b`: a figure's height, a shoulder wide (game.ts bodyInWay).</summary>
        bool BodyInWay(Vector3 a, Vector3 b, params string[] skip)
        {
            var bodies = new List<Vector3>();
            foreach (var o in Shown.Units)
                if (o.Alive && System.Array.IndexOf(skip, o.Id) < 0 && (Visible?.Invoke(o) ?? true) && FeetOf(o.Id) is Vector3 f) bodies.Add(f);
            var length = Vector3.Distance(a, b);
            for (var d = 0.2f * Board.Tile; d < length - 0.4f * Board.Tile; d += 0.2f * Board.Tile)
            {
                var p = Vector3.Lerp(a, b, d / length);
                foreach (var f in bodies)
                    if (new Vector2(p.x - f.x, p.z - f.z).magnitude < 0.3f * Board.Tile && p.y > f.y && p.y < f.y + 1.8f * Board.Tile) return true;
            }
            return false;
        }
    }
}
