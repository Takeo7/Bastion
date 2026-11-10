using System;
using System.Linq;
using Bastion.Client.Units;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>A blow that fails (the review of 10/11, row 3: the combat waited for it for ever).</summary>
    public static partial class CombatLab
    {
        /// <summary>
        /// A soldier's blow at one of the dead next to it, its stroke failing as it begins: its figure taken away 0.1 s in
        /// (BASTION_FALLA=figura, unless said; «no», nothing taken). Logs whether the blow landed (onHit) and the wait for it
        /// (UnitViews.Lunge, as UI's event player waits) ended, and when: still waiting 3 s on, it waits for ever.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.GolpeFallido -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 40 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void GolpeFallido()
        {
            var first = MapLab.Golden("city-7");
            string striker = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            string struck = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            Plain(u => u.Id == striker || u.Id == struck, 4, 6);
            Put(state.Units.First(u => u.Id == striker), 4, 10, 0);
            Put(state.Units.First(u => u.Id == struck), 5, 10, Math.PI);
            string fail = Environment.GetEnvironmentVariable("BASTION_FALLA") ?? "figura";
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.NextFrameAsync();
                    await Awaitable.NextFrameAsync();
                    bool hit = false;
                    float began = Time.time;
                    var blow = units.Lunge(striker, struck, false, () => hit = true);
                    while (Time.time - began < 0.1f) await Awaitable.NextFrameAsync();
                    if (fail == "figura") UnityEngine.Object.Destroy(Figures.FeetOf(striker).gameObject);
                    while (!blow.IsCompleted && Time.time - began < 3) await Awaitable.NextFrameAsync();
                    Debug.Log(FormattableString.Invariant($"[golpe] {fail}: onHit {(hit ? "sí" : "no")}, Lunge {(blow.IsCompleted ? "acabado" : "esperando")}, a los {Time.time - began:0.00} s"));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
        }
    }
}
