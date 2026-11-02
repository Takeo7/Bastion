using System;
using System.Collections.Generic;
using Bastion.Client.Audio;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The molotov's sounds (Arturo's #10, 1/11) as the combat asks for them, in play mode in batchmode, with the game's
    /// mixer and manifest (as Client/Audio's SoundCheck): its breaking (CombatSound.Molotov: the glass, the flame catching),
    /// then a fire of it burning three seconds in the state shown (CombatSound.Fires: its crackle looping) and gone out of
    /// it, dying away. With BASTION_SONIDO_TRAZA=1 each sound goes to the log. It fails if the new groups have no takes or
    /// the crackle doesn't start and stop, and quits Unity (BASTION_SONIDO_GRABAR=&lt;f32&gt;: what came out, to measure):
    ///   QUIT=0 BASTION_SONIDO_TRAZA=1 unity/tools/unity.sh -executeMethod Bastion.EditorTools.SonidoMolotov.Probar
    /// </summary>
    public static class SonidoMolotov
    {
        static bool optionsWere;
        static EnterPlayModeOptions optionsHad;

        public static void Probar()
        {
            optionsWere = EditorSettings.enterPlayModeOptionsEnabled;
            optionsHad = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorApplication.playModeStateChanged += Started;
            EditorApplication.EnterPlaymode();
        }

        static void Started(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= Started;
            Run();
        }

        static async void Run()
        {
            int code = 1;
            try
            {
                await Seconds(0.5);
                if (!Sound.HasRecorded("zombie.growl")) throw new Exception("no suena nada en el modo de juego: sin SoundPlayer, sin manifiesto o en silencio");
                bool groups = Sound.HasRecorded("fire.ignite") && Sound.HasRecorded("fire.crackle");
                Debug.Log(groups ? "[prueba] fire.ignite y fire.crackle tienen tomas" : "[prueba] faltan tomas de fire.ignite o fire.crackle");
                var ear = new GameObject("Oído").AddComponent<Camera>();
                ear.transform.position = new Vector3(0, 12, -12);
                ear.transform.LookAt(Vector3.zero);
                var sfx = new CombatSound();
                sfx.Attach(ear, () => Vector3.zero, 1, (_, p) => new Vector3(p.X, 0, p.Y));
                Debug.Log("[prueba] la botella se rompe y prende");
                sfx.Molotov(Vector3.zero);
                await Seconds(0.3);
                // The state as shown: a molotov's fire on (4, 4) of an 8×8 map, three seconds, then gone (FireOutEvent).
                var shown = new GameState { Width = 8, Height = 8, Fires = new List<Fire> { new Fire { Id = "m1", Source = 4 * 8 + 4, Turns = 2 } } };
                sfx.Fires(() => shown);
                Debug.Log("[prueba] arde (crepita)");
                await Seconds(3);
                shown.Fires = new List<Fire>();
                Debug.Log("[prueba] se apaga");
                await Seconds(2.5);
                sfx.Fires(null);
                code = groups ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[prueba] " + e);
            }
            finally
            {
                EditorSettings.enterPlayModeOptionsEnabled = optionsWere;
                EditorSettings.enterPlayModeOptions = optionsHad;
                // (Out of play mode first: what BASTION_SONIDO_GRABAR kept is written as the game quits, SoundPlayer.)
                EditorApplication.playModeStateChanged += c =>
                {
                    if (c == PlayModeStateChange.EnteredEditMode) EditorApplication.Exit(code);
                };
                EditorApplication.ExitPlaymode();
            }
        }

        /// <summary>Real time, as the sounds' own waits (SoundPlayer.After) count it.</summary>
        static async Awaitable Seconds(double seconds)
        {
            var until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) await Awaitable.NextFrameAsync();
        }
    }
}
