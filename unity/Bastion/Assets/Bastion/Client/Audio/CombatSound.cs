using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Audio
{
    /// <summary>
    /// The combat's sound (0.10.1's game/sfx.ts Sfx): what the combat calls as it plays each event. Each sound is placed
    /// where it happens (stereo by its place on screen, quieter far from where the camera looks); guns sound like their
    /// class, tier and side; the music and the ambience follow the mission (the biome, contact, the enemy's turn); the
    /// sabotage's siren wails from where it stands. Main thread.
    /// </summary>
    public sealed class CombatSound
    {
        /// <summary>The sabotage's alarm, as the engine says which it is (the mission's alarm, chosen by the campaign).</summary>
        private static readonly Dictionary<string, string> Sirens = new Dictionary<string, string> { ["police"] = "siren.police", ["ambulance"] = "siren.ambulance", ["factory"] = "siren.alarm" };
        /// <summary>Without that (a skirmish, an older save): by the map, a police car in town, an ambulance out of it, a factory's motor siren.</summary>
        private static readonly Dictionary<string, string> SirensByMap = new Dictionary<string, string> { ["city"] = "siren.police", ["wilds"] = "siren.ambulance", ["facility"] = "siren.alarm" };
        /// <summary>Seconds a siren takes to fall silent: a motor runs down (and its light with it); the electric ones cut out.</summary>
        private static readonly Dictionary<string, double> SirenStop = new Dictionary<string, double> { ["siren.alarm"] = 2.5 };
        /// <summary>The dead walking: at most one growl this often (s), from this far (tiles) from where the camera looks, this likely.</summary>
        private const double GrowlGap = 4;
        private const float GrowlReach = 18;
        private const double GrowlChance = 0.5;
        /// <summary>The siren's level right beside it: steady under the fight, not over it.</summary>
        private const float SirenLevel = 0.45f;
        /// <summary>
        /// The Hinchado (Ana, 20 p1: each kind of the dead with its own sound): its voice and its death, recorded, played
        /// lower than taken (they were brighter than the Zombi's growls: 3–5.5 kHz against 1.7–3.8), as something heavier.
        /// </summary>
        private const float BloaterVoiceRate = 0.75f;
        private const float BloaterDeathRate = 0.85f;
        /// <summary>
        /// Its burst, a little louder than a grenade (Ana, 20 p1): the blast 2 dB up and deeper, the splatter over it. In
        /// Unity's mix (29/10), 2–3 dB more over its first second than the grenade's, by the take, and 1 dB more at the peak.
        /// </summary>
        private const float BurstBlast = 1.26f;
        private const float BurstRate = 0.9f;
        private const float BurstSplatter = 0.45f;
        /// <summary>
        /// The new blunt weapons (Ana, 21 p2): a blow is the body's thud with the weapon's own knock over it, at this
        /// share (the aluminium bat rings: under). The chainsaw revs as it swings and cuts as it hits.
        /// </summary>
        private static readonly Dictionary<string, (string group, float gain)> Blunt = new Dictionary<string, (string, float)>
        {
            ["pan"] = ("melee.pan", 0.6f),
            ["bat"] = ("melee.bat", 0.5f),
            ["crowbar"] = ("melee.crowbar", 0.6f),
        };
        private const string Chainsaw = "chainsaw";

        private Camera? camera;
        private Func<Vector3> focus = () => Vector3.zero;
        private Func<GameState, Pos, Vector3> world = (_, p) => new Vector3(p.X, 0, p.Y);
        /// <summary>Unity's units in a tile (MapSpace.Tile): 0.10.1's distances were in tiles.</summary>
        private float tile = 1;
        /// <summary>Out of town the ground is earth, not asphalt or concrete (where a stone lands).</summary>
        private bool softGround;

        /// <summary>
        /// The camera that hears and where it looks (for distance), Unity's units in a tile, and where a tile is in the world
        /// a tile above the ground (MapSpace.TileToWorld: the siren's place).
        /// </summary>
        public void Attach(Camera camera, Func<Vector3> focus, float unitsPerTile, Func<GameState, Pos, Vector3> world)
        {
            this.camera = camera;
            this.focus = focus;
            tile = unitsPerTile;
            this.world = world;
        }

        private static double Now => Time.realtimeSinceStartupAsDouble;

        private static double Random() => SoundPlayer.Instance?.Random() ?? 0.5;

        /// <summary>Where `at` is on screen, three.js's way: −1 to 1 across and up, off it past the far plane or behind.</summary>
        private (float x, float y, bool behind) Screen(Vector3 at)
        {
            var v = camera!.WorldToViewportPoint(at);
            return (v.x * 2 - 1, v.y * 2 - 1, v.z < 0 || v.z > camera.farClipPlane);
        }

        /// <summary>Stereo position and level of a sound at `at`: panned by screen position, quieter with distance and off screen.</summary>
        private (float pan, float gain) Place(Vector3? at, float gain = 1)
        {
            if (at == null || camera == null) return (0, gain);
            var (x, y, behind) = Screen(at.Value);
            var pan = Mathf.Clamp(x * 0.7f, -0.75f, 0.75f);
            var dist = Vector3.Distance(at.Value, focus()) / tile;
            var g = Mathf.Clamp(1.15f - dist / 30, 0.35f, 1);
            if (Math.Abs(x) > 1 || Math.Abs(y) > 1 || behind) g *= 0.7f;
            return (pan, g * gain);
        }

        private void At(string name, Vector3? at, float gain = 1, double delay = 0, float spread = 0.04f, float rate = 1)
        {
            var (pan, g) = Place(at, gain);
            Sound.Play(name, g, rate, pan, delay, spread: spread);
        }

        private bool Recorded(string group, Vector3? at, float gain = 1, double delay = 0, float spread = 0.04f, float rate = 1, float reverb = 0)
        {
            var (pan, g) = Place(at, gain);
            return Sound.Recorded(group, g, rate, pan, delay, reverb, spread);
        }

        /// <summary>The first of these sounds that exists (newer recipes fall back to what is there).</summary>
        private static string Pick(params string[] names) => names.FirstOrDefault(Sound.Exists) ?? names[names.Length - 1];

        // ------------------------------------------------------------ weapons

        /// <summary>One round (bursts call this per round): the gun's own voice.</summary>
        public void Shot(Unit u, string weaponId, Vector3? at) => At(GunSound(u, weaponId), at, spread: 0.05f);

        /// <summary>Where a round lands: «flesh», «armor», «robot», or «miss» (whizzing past, chipping cover); a critical hit over it.</summary>
        public void Impact(string kind, Vector3? at, bool crit = false)
        {
            At($"impact.{kind}", at);
            if (kind == "miss" && Random() < 0.6) At("impact.cover", at, 0.8f, 0.05);
            if (crit) At(kind == "robot" ? "impact.crit.metal" : "impact.crit", at);
        }

        /// <summary>The kind of flesh a target is, for its impacts: «robot» or «flesh».</summary>
        public static string Body(Unit u) => Machine(u) ? "robot" : "flesh";

        /// <summary>
        /// «swing», «sword», «stun» or «claw», with the `weapon` it is done with: the dead's claw bites (their snarl and the
        /// bite, recorded); a blade cuts; a pan, a bat or a crowbar thuds into the body with its own knock; the chainsaw
        /// revs as it swings and cuts as it hits.
        /// </summary>
        public void Melee(string kind, Vector3? at, string? weapon = null)
        {
            if (kind == "claw" && Sound.HasRecorded("zombie.bite"))
            {
                Recorded("zombie.attack", at, spread: 0.06f);
                Recorded("zombie.bite", at, delay: 0.12);
                return;
            }
            if (weapon == Chainsaw && Recorded(kind == "swing" ? "melee.chainsaw.rev" : "melee.chainsaw.cut", at, kind == "swing" ? 1 : 0.8f, spread: 0.03f)) return;
            if (kind == "sword" && weapon != null && Blunt.TryGetValue(weapon, out var blunt) && Recorded("melee.blunt", at, spread: 0.06f))
            {
                Recorded(blunt.group, at, blunt.gain, spread: 0.06f);
                return;
            }
            if (kind == "sword" && Recorded("melee.blade", at, spread: 0.06f)) return;
            At($"melee.{kind}", at);
        }

        // ------------------------------------------------------------ explosives

        public void Throw(Vector3? at) => At("grenade.throw", at);

        /// <summary>A stone leaving the hand: a short swish.</summary>
        public void StoneThrow(Vector3? at) => At("stone.throw", at, spread: 0.08f);

        /// <summary>Where the stone comes down: a knock on hard ground, a thud on earth, or a blow on whoever it `struck`.</summary>
        public void StoneLand(Vector3? at, bool struck = false) => At(struck ? "stone.hit" : softGround ? "stone.thud" : "stone.land", at);

        /// <summary>One of the dead beating on a «door» or a «barricade»; if it `gaveWay`, the crack and what falls, and the music dips under it.</summary>
        public void Smash(Vector3? at, string kind, bool gaveWay)
        {
            At($"smash.{kind}", at, spread: 0.06f);
            if (!gaveWay) return;
            At($"smash.give.{kind}", at, delay: 0.04);
            Sound.Duck(0.75f, 0.3, 1.2);
        }

        public void Launch(Vector3? at) => At("launch", at);

        public void Land(Vector3? at) => At("grenade.land", at);

        public void Missile(Vector3? at) => At("missile", at);

        /// <summary>«frag», «flash» or «smoke»; the music dips under a blast.</summary>
        public void Explosion(Vector3? at, string kind = "frag")
        {
            if (kind == "smoke")
            {
                At("smoke", at);
                return;
            }
            At(kind == "flash" ? "explosion.flash" : "explosion", at);
            Sound.Duck(0.55f, 0.6, 1.8);
        }

        /// <summary>The Hinchado bursts (as it dies, or on reaching a soldier): a deeper, louder blast, and what it was full of splattering.</summary>
        public void Burst(Vector3? at)
        {
            At("explosion", at, BurstBlast, rate: BurstRate);
            Recorded("bloater.burst", at, BurstSplatter, 0.02, reverb: 0.15f);
            Sound.Duck(0.55f, 0.6, 1.8);
        }

        // ------------------------------------------------------------ moving and acting

        public void Step(Unit u, Vector3? at)
        {
            var t = u.Template;
            // The Hinchado's own, wet and heavy; the rest as in the 0.10.1.
            if (t != "bloater" || !Recorded("bloater.step", at, 0.8f, spread: 0.06f))
                At(t == "mec" ? "step.mec" : t == "zombie" ? "step.drag" : "step", at, t == "sectoid" || t == "xenoid" ? 0.6f : 1, spread: 0.1f);
            if (Template(u)?.Undead == true) DeadWalk(at, t == "bloater");
        }

        // ------------------------------------------------------------ the dead and the hurt

        private double growlAt = double.NegativeInfinity;
        private readonly List<(Vector3 at, bool bloater)> walking = new List<(Vector3, bool)>();
        private bool walkWaiting;

        /// <summary>
        /// One of the dead walking: now and then one growls, the nearest to where the camera looks, and never a choir: one
        /// at a time, a few seconds apart. Recorded growls only, the Hinchado's its own. The steps of a moment are taken
        /// together (a group of the dead moves at once) and the nearest of them picked.
        /// </summary>
        private void DeadWalk(Vector3? at, bool bloater)
        {
            var player = SoundPlayer.Instance;
            if (at == null || player == null || !Sound.HasRecorded("zombie.growl") || Now - growlAt < GrowlGap) return;
            walking.Add((at.Value, bloater));
            if (walkWaiting) return;
            walkWaiting = true;
            player.After(0.15, () =>
            {
                walkWaiting = false;
                var f = focus();
                var nearest = walking.OrderBy((w) => Vector3.Distance(w.at, f)).First();
                walking.Clear();
                if (Vector3.Distance(nearest.at, f) / tile > GrowlReach || Random() > GrowlChance) return;
                growlAt = Now;
                if (!nearest.bloater || !Recorded("bloater.voice", nearest.at, 0.8f, spread: 0.08f, rate: BloaterVoiceRate)) Recorded("zombie.growl", nearest.at, 0.8f, spread: 0.08f);
            });
        }

        /// <summary>A soldier hurt: a scream when the dead bite, a grunt otherwise; by their sex, never the same take twice running.</summary>
        public void Hurt(Unit u, Vector3? at, bool bitten = false)
        {
            var sex = u.Appearance?.Sex == "f" ? "f" : "m";
            // Without a grunt of their own (there are no women's among the recordings), a scream, quieter.
            if (bitten || !Recorded($"soldier.pain.{sex}", at, spread: 0.02f)) Recorded($"soldier.scream.{sex}", at, bitten ? 1 : 0.7f, spread: 0.02f);
        }

        public void Reload(Vector3? at) => At("reload", at);

        /// <summary>Registrar: rummaging through a piece of furniture, a boot or crates.</summary>
        public void Search(Vector3? at) => At("search", at);

        /// <summary>Something turned up while searching.</summary>
        public void Found(Vector3? at) => At("found", at);

        public void Overwatch(Vector3? at) => At("overwatch", at);

        public void Hunker(Vector3? at) => At("hunker", at);

        public void Door(Vector3? at) => At("door", at);

        public void Glass(Vector3? at) => At("impact.glass", at);

        public void Heal(Vector3? at) => At("heal", at);

        public void Shield(Vector3? at) => At("shield", at);

        public void Stun(Vector3? at) => At("melee.stun", at);

        public void Hack(Vector3? at) => At("hack", at);

        public void Hacked(bool success, Vector3? at) => At(success ? "hack.ok" : "alarm", at);

        public void Psi(Vector3? at) => At("psi", at);

        public void Beam(Vector3? at) => At("beam", at);

        public void Pods(Vector3? at) => At("pods", at);

        /// <summary>The dead arriving: a low moan or two, and feet dragging.</summary>
        public void Moan(Vector3? at, int voices = 1)
        {
            var moan = Pick("zombie.moan", "death.zombie");
            for (var i = 0; i < Math.Min(3, voices); i++) At(moan, at, 0.75f, i * 0.35, 0.08f, moan == "death.zombie" ? 0.72f : 1);
            for (var i = 0; i < 3; i++) At("step.drag", at, 0.6f, 0.2 + i * 0.4);
        }

        /// <summary>Raiders arriving: a pickup's engine and brakes at the edge if there is the sound for it, else boots running in.</summary>
        public void Vehicle(Vector3? at)
        {
            if (Sound.Exists("truck.engine"))
            {
                At("truck.engine", at);
                return;
            }
            for (var i = 0; i < 6; i++) At("step", at, 0.7f, i * 0.17);
        }

        public void Flare(Vector3? at) => At("flare", at);

        public void Data() => Sound.Play("data");

        public void Rescue() => Sound.Play("rescue");

        /// <summary>A death; `by`, who killed them: one of ours killed by the dead is eaten.</summary>
        public void Death(Unit u, Vector3? at, Unit? by = null)
        {
            // The siren falls silent the moment it is destroyed, as its lights go out.
            if (u.Objective && wail != null) Silence(SirenStop.TryGetValue(wail.name, out var s) ? s : 0);
            var t = u.Template;
            var name = Machine(u) ? "death.robot" : t == "sectoid" ? "death.sectoid" : t == "xenoid" ? "death.runner" : t == "zombie" || t == "mec" || t == "bloater" ? "death.zombie" : u.Team == Teams.Alien ? "death.advent" : "death.human";
            // The Acorazado falls as the dead do, heavier; the Hinchado gurgles its own as it goes (its burst follows).
            if (t != "bloater" || !Recorded("bloater.death", at, 0.9f, rate: BloaterDeathRate)) At(name, at, rate: t == "mec" ? 0.85f : 1);
            if (u.Team != Teams.Xcom) return;
            Recorded($"soldier.scream.{(u.Appearance?.Sex == "f" ? "f" : "m")}", at, spread: 0.02f);
            if (by != null && Template(by)?.Undead == true) Recorded("zombie.eat", at, delay: 0.5);
        }

        // ------------------------------------------------------------ moments

        /// <summary>¡Contacto!: the stinger, and the music turns tense; the dead who saw the squad (`at`, `dead`) shriek at it.</summary>
        public void Contact(Vector3? at = null, bool dead = false)
        {
            if (!Sound.Recorded("sting", 0.9f, spread: 0)) Sound.Play("contact");
            if (dead && at != null) Recorded("zombie.alert", at, delay: 0.2, spread: 0.06f);
            Sound.Duck(0.6f, 0.8, 2);
            Mood("tense");
        }

        /// <summary>Concealment broken.</summary>
        public void Reveal()
        {
            Sound.Play("reveal");
            Mood("tense");
        }

        /// <summary>A turn begins: our chirp or their swell; the enemy turn has its own music.</summary>
        public void Turn(bool alien, bool contact)
        {
            Sound.Play(alien ? "turn.alien" : "turn.xcom");
            Mood(alien ? "alien" : contact ? "tense" : "calm");
        }

        public void Command() => Sound.Play("command");

        /// <summary>Señalar: a mark put on the map by either player: a pin pushed in, where it is.</summary>
        public void Mark(Vector3? at) => At("mark", at, spread: 0.05f);

        /// <summary>Something to notice (an alert ability, reaction fire, a warning).</summary>
        public void Alert() => Sound.Play("overwatch", 0.8f);

        /// <summary>The mission is over: the music stops for the stinger.</summary>
        public void Outcome(bool victory)
        {
            Silence(0.5);
            Music.Play(null, 0.6);
            Sound.Play(victory ? "victory" : "defeat", delay: 0.3);
        }

        // ------------------------------------------------------------ music and ambience

        /// <summary>The ambience of the map («city», «wilds», «facility») and the music for how things stand.</summary>
        public void Start(string biome, bool contact)
        {
            // A transmission left open at take-off lets the music up (0.10.1 closed the radio as the squad left).
            UiSounds.ReleaseRadio();
            softGround = biome == "wilds";
            Ambience.Play(biome);
            Mood(contact ? "tense" : "calm");
        }

        public void Mood(string piece)
        {
            if (Music.Current != piece) Music.Play(piece, piece == "alien" ? 1.5 : 3);
        }

        // ------------------------------------------------------------ the siren

        private sealed class Wail
        {
            public string name = "";
            public Vector3 at;
            public SoundLoop? loop;
        }

        private Wail? wail;

        /// <summary>
        /// The sabotage's siren: looping from where it stands, heard across the map, louder near and panned by its place on
        /// screen, until it is silenced (its death, the outcome, or null when the mission is left). `view`: the combat's
        /// state as it is shown (each event as it is played), read again ten times a second: the siren also falls silent
        /// when it dies out of sight.
        /// </summary>
        public void Siren(Func<GameState?>? view)
        {
            var s = view?.Invoke();
            var relay = s != null && s.Mission.Kind == MissionKinds.Sabotage && s.Outcome == null ? Relay(s) : null;
            var player = SoundPlayer.Instance;
            if (s == null || relay == null || player == null)
            {
                Silence(0);
                return;
            }
            var name = s.Mission.Alarm != null && Sirens.TryGetValue(s.Mission.Alarm, out var n) ? n : SirensByMap.TryGetValue(s.Biome, out var m) ? m : "siren.police";
            var at = world(s, relay.Pos);
            if (wail != null && wail.name == name && wail.at == at) return;
            Silence(0);
            var w = new Wail { name = name, at = at };
            wail = w;
            void Tick()
            {
                if (wail != w) return;
                var now = view!();
                if (now == null || now.Outcome != null || Relay(now) == null)
                {
                    Silence(SirenStop.TryGetValue(name, out var stop) ? stop : 0);
                    return;
                }
                var (level, pan, muffle) = Far(at);
                w.loop ??= Sound.Loop(name, level * SirenLevel, 0.3f, Bus.Sfx);
                w.loop?.Set(level * SirenLevel, pan, muffle);
                player.After(0.1, Tick);
            }
            Tick();
        }

        private static Unit? Relay(GameState s) => s.Units.FirstOrDefault((u) => u.Id == s.Mission.ObjectiveUnit && u.Alive);

        /// <summary>The siren stops: running down over `windDown` seconds, or at once.</summary>
        private void Silence(double windDown)
        {
            var w = wail;
            if (w == null) return;
            wail = null;
            w.loop?.Stop(windDown);
        }

        /// <summary>Like Place, for a sound meant to carry across the whole map: gentler with distance, losing its highs far away.</summary>
        private (float level, float pan, float muffle) Far(Vector3 at)
        {
            if (camera == null) return (0.5f, 0, 0.5f);
            var (x, y, behind) = Screen(at);
            var pan = Mathf.Clamp(x * 0.7f, -0.75f, 0.75f);
            var dist = Vector3.Distance(at, focus()) / tile;
            var offScreen = Math.Abs(x) > 1 || Math.Abs(y) > 1 || behind;
            var level = Mathf.Clamp(1.1f - dist / 40, 0.18f, 1) * (offScreen ? 0.8f : 1);
            return (level, pan, Mathf.Clamp(dist / 50, 0, 1));
        }

        private static UnitTemplate? Template(Unit u) => Content.Templates.TryGetValue(u.Template, out var t) ? t : null;

        /// <summary>
        /// A machine, which clanks when hit and dies as one (the alarm box). The Acorazado is robotic for its armour but is
        /// one of the dead in riot or army kit: flesh, its own death and steps.
        /// </summary>
        private static bool Machine(Unit u) => Template(u) is UnitTemplate t && t.Robotic && !t.Undead;

        /// <summary>The sound of a unit's gun: soldiers by gun and tier, the enemy by their weapon.</summary>
        private static string GunSound(Unit u, string weaponId)
        {
            switch (weaponId)
            {
                case "rifle":
                case "shotgun":
                case "sniper":
                case "cannon":
                case "pistol":
                    return $"shot.{weaponId}.{Math.Min(3, Math.Max(1, u.WeaponTier ?? 1))}";
                case "mecCannon":
                    return "shot.mec";
                case "sectoidPistol":
                    return "shot.sectoid";
                case "xenoPistol":
                    return "shot.xenoid";
                default:
                    return "shot.advent";
            }
        }
    }
}
