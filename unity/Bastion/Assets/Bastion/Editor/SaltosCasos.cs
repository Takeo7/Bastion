// M11: the jumps SaltosLab found over the bots' games, each played at will on the plain floor, with the Sync an event's
// settling gives in the middle of it. A lab alone.
using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.EditorTools
{
    public static partial class CombatLab
    {
        /// <summary>
        /// CombatLab.Agarres' row (BASTION_ARMAS=rifle: one soldier) with one of the dead before each, and SaltosLab watching;
        /// then, a line [caso] before each:
        ///   asoma     the soldier steps out to the tile beside it (UI's shot from cover: UnitViews.Slide) and a Sync falls
        ///             0.3 s on, as the next event settles; back home 1.2 s after
        ///   golpe     the soldier's blow at the dead one, a Sync as it lands
        ///   mordisco  the dead one's headbutt at the soldier, a Sync as it lands
        ///   levanta   the soldier hunkers, and stands again
        /// BASTION_SIN_CAMINO=1: as before M11's mends.
        ///   BASTION_ARMAS=rifle BASTION_TIRAS=&lt;folder&gt; unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Casos -shotPlay
        ///     -shotCamera "Shot camera" -shotFrames 600 -shotFps 60 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Casos()
        {
            Agarres();
            SaltosLab.Saltos();
            var soldiers = state.Units.Where(u => u.Id.StartsWith("lab-")).ToList();
            var dead = Targets(soldiers.Select(u => (u, u.Id.Substring(4))).ToList());
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    Debug.Log($"[caso] {Time.time:0.00} asoma");
                    foreach (var u in soldiers) _ = units.Slide(u.Id, MapSpace.World(u.Pos.X + 1, u.Pos.Y), 0.22f);
                    await Awaitable.WaitForSecondsAsync(0.52f);
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.2f);
                    foreach (var u in soldiers) _ = units.Slide(u.Id, MapSpace.World(u.Pos.X, u.Pos.Y), 0.25f);
                    await Awaitable.WaitForSecondsAsync(1.0f);
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(0.5f);
                    // (A second shot on the heels of the first: out, a Sync (it starts home), and 0.1 s on out again.)
                    Debug.Log($"[caso] {Time.time:0.00} asoma-dos");
                    foreach (var u in soldiers) _ = units.Slide(u.Id, MapSpace.World(u.Pos.X + 1, u.Pos.Y), 0.22f);
                    await Awaitable.WaitForSecondsAsync(0.52f);
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(0.1f);
                    foreach (var u in soldiers) _ = units.Slide(u.Id, MapSpace.World(u.Pos.X + 1, u.Pos.Y), 0.22f);
                    await Awaitable.WaitForSecondsAsync(0.6f);
                    foreach (var u in soldiers) Debug.Log(FormattableString.Invariant($"[caso] {Time.time:0.00} asoma-dos: {u.Id} {(units.Feet(u.Id) - MapSpace.World(u.Pos.X, u.Pos.Y)).magnitude:0.00} m from its own tile (1.50: out to fire again; 0: behind its cover)"));
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(0.6f);
                    foreach (var u in soldiers) Debug.Log(FormattableString.Invariant($"[caso] {Time.time:0.00} asoma-dos, a Sync later: {u.Id} {(units.Feet(u.Id) - MapSpace.World(u.Pos.X, u.Pos.Y)).magnitude:0.00} m from its own tile"));
                    Debug.Log($"[caso] {Time.time:0.00} golpe");
                    for (int k = 0; k < soldiers.Count; k++) _ = units.Lunge(soldiers[k].Id, dead[k].Id, false, () => units.Sync(state));
                    await Awaitable.WaitForSecondsAsync(2.5f);
                    units.Sync(state);
                    Debug.Log($"[caso] {Time.time:0.00} mordisco");
                    for (int k = 0; k < soldiers.Count; k++) _ = units.Lunge(dead[k].Id, soldiers[k].Id, false, () => units.Sync(state));
                    await Awaitable.WaitForSecondsAsync(2.0f);
                    units.Sync(state);
                    Debug.Log($"[caso] {Time.time:0.00} levanta");
                    foreach (var u in soldiers) u.Hunkered = true;
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    foreach (var u in soldiers) u.Hunkered = false;
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
        }
    }
}
