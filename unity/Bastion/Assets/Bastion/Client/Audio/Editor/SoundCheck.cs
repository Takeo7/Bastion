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
    /// growl is now and then), its death and its burst; the reloads, the rifle's and the pistol's; and the silent
    /// takedown, with a knife's cut after it to hear how far under it is; and El Toro (H6): its roar, its steps into the
    /// charge's crash and the soldier knocked down, the soldier getting up, its pound with a knife's cut after it, and its
    /// death; and the bed inside a building (R5), then the town's again. It fails if any of their groups has no takes, or
    /// the rifle's reload, the takedown or one of El Toro's no recipe, or the bed inside isn't in the sound folder, and
    /// quits Unity:
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
                // The rifle's reload, with its recipe of its own (recetas.ts, 9/11), and the pistol's, the 0.10.1's.
                var rifle = Sound.Exists("reload.rifle");
                Debug.Log(rifle ? "[prueba] recarga del fusil y de la pistola" : "[prueba] sin la receta de la recarga del fusil (reload.rifle)");
                sfx.Reload(at, rifle: true);
                await Seconds(1.5);
                sfx.Reload(at);
                await Seconds(1.5);
                // The silent takedown (recetas.ts, 16/11), then a knife's cut.
                var takedown = Sound.Exists("melee.takedown");
                Debug.Log(takedown ? "[prueba] golpe silencioso, y un tajo de cuchillo para compararlo" : "[prueba] sin la receta del golpe silencioso (melee.takedown)");
                sfx.Takedown(at);
                await Seconds(1);
                sfx.Melee("sword", at);
                await Seconds(1);
                // El Toro (recetas.ts, H6): the roar; four steps into the charge's crash, the soldier down; up; the pound, a knife's cut; its death.
                var bull = new[] { "boss.roar", "boss.step", "boss.impact", "boss.pound", "body.down" }.Where((n) => !Sound.Exists(n)).ToList();
                Debug.Log(bull.Count == 0 ? "[prueba] El Toro: rugido, embestida, tumbado, levantarse, mazazo (y un tajo) y muerte" : $"[prueba] El Toro sin receta: {string.Join(", ", bull)}");
                var toro = new Unit { Id = "a2", Template = AlienIds.Brute, Team = Teams.Alien };
                var down = new Unit { Id = "s2", Template = TemplateIds.Rookie, Team = Teams.Xcom, Appearance = new Appearance { Sex = "m" } };
                sfx.Roar(at);
                await Seconds(2.5);
                for (var i = 0; i < 4; i++)
                {
                    sfx.Step(toro, at);
                    await Seconds(0.14);
                }
                sfx.ChargeHit(at);
                sfx.KnockedDown(down, at);
                await Seconds(1.5);
                sfx.GotUp(at);
                await Seconds(1);
                sfx.Pound(at);
                await Seconds(1);
                sfx.Melee("sword", at);
                await Seconds(1);
                sfx.Death(toro, at);
                await Seconds(2);
                // The bed inside a building (interior.ts, R5): a map that is one building plays it; one out of doors, its biome's.
                var inside = Ambience.Has(CombatSound.InsideBed);
                Debug.Log(inside ? "[prueba] ambiente de interior, y luego el de la ciudad" : "[prueba] sin el ambiente de interior (ambience.inside)");
                sfx.Start(new GameState { Biome = "city", Mission = new MissionState { Inside = true } }, false);
                await Seconds(4);
                sfx.Start(new GameState { Biome = "city" }, false);
                await Seconds(3);
                code = missing.Count == 0 && rifle && takedown && bull.Count == 0 && inside ? 0 : 1;
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
