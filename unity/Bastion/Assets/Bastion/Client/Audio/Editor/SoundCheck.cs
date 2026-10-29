using System;
using System.Linq;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.Client.Audio.EditorTools
{
    /// <summary>
    /// The combat's sounds the 0.10.1 didn't have (Ana's notes of 29/10), played as the combat asks for them, in play mode
    /// in batchmode, with the game's mixer and manifest. With BASTION_SONIDO_TRAZA=1 each sound goes to the log
    /// («[sonido] … melee.pan grabados/…»). It plays a blow with each melee weapon, by the id Content.AttackWeapon gives for
    /// a soldier carrying it at the belt (the knife and the machete too, to compare). Then the Hinchado: its steps (its
    /// growl is now and then), its death and its burst. It fails if any of their groups has no takes, and quits Unity:
    ///   QUIT=0 BASTION_SONIDO_TRAZA=1 unity/tools/unity.sh -executeMethod Bastion.Client.Audio.EditorTools.SoundCheck.Combate
    /// </summary>
    public static class SoundCheck
    {
        private static readonly string[] Groups = { "bloater.voice", "bloater.step", "bloater.death", "bloater.burst", "melee.blunt", "melee.pan", "melee.bat", "melee.crowbar", "melee.chainsaw.rev", "melee.chainsaw.cut" };

        /// <summary>What each soldier carries at the belt (the Armería's ids); none, the knife.</summary>
        private static readonly string?[] Belts = { null, "machete", "pan", "bat", "crowbar", "chainsaw" };

        private static bool optionsWere;
        private static EnterPlayModeOptions optionsHad;

        public static void Combate()
        {
            // As the captures in play mode (Shots): no domain or scene reload, the project's options put back on quitting.
            optionsWere = EditorSettings.enterPlayModeOptionsEnabled;
            optionsHad = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorApplication.playModeStateChanged += Started;
            EditorApplication.EnterPlaymode();
        }

        private static void Started(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= Started;
            Run();
        }

        private static async void Run()
        {
            var code = 1;
            try
            {
                await Seconds(0.5);
                // (Without the player, its manifest, or with the sound off (M), nothing plays: the 0.10.1's groups say so too.)
                if (!Sound.HasRecorded("zombie.growl")) throw new Exception("no suena nada en el modo de juego: sin SoundPlayer, sin manifiesto o en silencio");
                var missing = Groups.Where((g) => !Sound.HasRecorded(g)).ToList();
                Debug.Log(missing.Count == 0 ? $"[prueba] los {Groups.Length} grupos nuevos tienen tomas" : $"[prueba] sin tomas: {string.Join(", ", missing)}");
                var ear = new GameObject("Oído").AddComponent<Camera>();
                ear.transform.position = new Vector3(0, 12, -12);
                ear.transform.LookAt(Vector3.zero);
                var sfx = new CombatSound();
                sfx.Attach(ear, () => Vector3.zero, 1, (_, p) => new Vector3(p.X, 0, p.Y));
                Vector3? at = Vector3.zero;
                foreach (var belt in Belts)
                {
                    var soldier = new Unit { Id = "s1", Template = TemplateIds.Rookie, Team = Teams.Xcom, Arms = new Arms { Melee = belt } };
                    var weapon = Content.AttackWeapon(soldier, AbilityIds.Slash).Id;
                    Debug.Log($"[prueba] en el cinto {belt ?? "nada"}: el arma del golpe es {weapon}");
                    sfx.Melee("swing", at, weapon);
                    await Seconds(0.6);
                    sfx.Melee("sword", at, weapon);
                    await Seconds(2);
                }
                var bloater = new Unit { Id = "a1", Template = TemplateIds.Bloater, Team = Teams.Alien };
                Debug.Log("[prueba] el Hinchado anda (gruñe uno de cada dos pasos, como mucho cada 4 s)");
                for (var i = 0; i < 6; i++)
                {
                    sfx.Step(bloater, at);
                    await Seconds(4.3);
                }
                Debug.Log("[prueba] el Hinchado muere y revienta");
                sfx.Death(bloater, at);
                await Seconds(0.15);
                sfx.Burst(at);
                await Seconds(2.5);
                code = missing.Count == 0 ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[prueba] " + e);
            }
            finally
            {
                EditorSettings.enterPlayModeOptionsEnabled = optionsWere;
                EditorSettings.enterPlayModeOptions = optionsHad;
                EditorApplication.Exit(code);
            }
        }

        /// <summary>Real time, as the sounds' own waits (the growl's gap, SoundPlayer.After) count it.</summary>
        private static async Awaitable Seconds(double seconds)
        {
            var until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) await Awaitable.NextFrameAsync();
        }
    }
}
