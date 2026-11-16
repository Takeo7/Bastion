using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>A sample writer: run only with BASTION_MUESTRAS=1 (RunsSamples).</summary>
    public sealed class SamplesFactAttribute : FactAttribute
    {
        public const string Variable = "BASTION_MUESTRAS";

        public SamplesFactAttribute()
        {
            if (Environment.GetEnvironmentVariable(Variable) != "1") Skip = $"Solo con {Variable}=1: escribe las muestras de las runs.";
        }
    }

    /// <summary>
    /// The runs' sample states for UI (H0-DISENO §7): campaigns as the room sends them, written with its JSON (Net/Json.cs,
    /// indented) to .dev-snapshots/gameplay/runs/muestras/: `BASTION_MUESTRAS=1 dotnet test engine-cs/tests --filter
    /// RunsSamples`, from the repository's root. Their LEEME.md says what each shows.
    /// </summary>
    public class RunsSamples
    {
        private static string Dir
        {
            get
            {
                for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                {
                    if (Directory.Exists(Path.Combine(d.FullName, ".dev-snapshots"))) return Path.Combine(d.FullName, ".dev-snapshots", "gameplay", "runs", "muestras");
                }
                throw new DirectoryNotFoundException(".dev-snapshots");
            }
        }

        private static void Save(string name, GameState s)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, name), JsonConvert.SerializeObject(s, Formatting.Indented, Json.Settings) + "\n");
            Assert.Equal(Json.Write(s), Json.Write(Json.Read<GameState>(File.ReadAllText(Path.Combine(Dir, name)))));
        }

        /// <summary>
        /// A run's fight under way (H3): the base full, out to a node of row 1, launched as the room does; the first soldier's
        /// pack with a mix (some turned), gear on the floor at its feet, and a partner beside it.
        /// </summary>
        public static GameState Combat()
        {
            var c = Full();
            var run = Start(c);
            var node = RunMap.Choices(run).First((n) => RunMap.IsFight(n.Kind));
            Agree(c, new ChooseNodeCommand { Node = node.Id });
            var s = Runs.Launch(c, new Rng(5))!.Value.State;
            var squad = s.Units.Where((u) => u.CampaignId != null).ToList();
            var a = squad[0];
            var b = squad[1];
            // Beside it.
            foreach (var d in Grid.Dirs4)
            {
                var p = new Pos { X = a.Pos.X + d.X, Y = a.Pos.Y + d.Y, Z = a.Pos.Z };
                if (!Grid.InBounds(s, p.X, p.Y) || !Flights.CanStopAt(s, p) || (Grid.UnitAt(s, p) != null && Grid.UnitAt(s, p) != b)) continue;
                b.Pos = p;
                break;
            }
            Gear Made(string kind, string? type, int count = 1) => new Gear { Id = $"g{++s.Run!.NextGear}", Kind = kind, Type = type, Count = count };
            var pack = a.Pack!;
            pack.Items.Clear();
            Gears.Place(pack, Made(GearKinds.Weapon, FoundWeaponIds.PumpShotgun), 0, 0, false);
            Gears.Place(pack, Made(GearKinds.Weapon, FoundWeaponIds.Pistol), 3, 1, true);
            Gears.Place(pack, Made(GearKinds.Supplies, null, 35), 0, 1, false);
            Gears.Place(pack, Made(GearKinds.Medicine, null, 2), 1, 1, false);
            Gears.Place(pack, Made(GearKinds.Mags, null, 3), 2, 1, false);
            Gears.Place(pack, Made(GearKinds.Item, ItemIds.Molotov, 2), 0, 2, false);
            Gears.Place(pack, Made(GearKinds.Mod, WeaponModIds.Silencer), 1, 2, false);
            RunPack.SyncMags(a);
            s.FloorGear = new List<DroppedGear>
            {
                new DroppedGear { Gear = Made(GearKinds.Weapon, FoundWeaponIds.Machete), Pos = Events.CopyPos(a.Pos) },
                new DroppedGear { Gear = Made(GearKinds.Vest, VestIds.Vest), Pos = Events.CopyPos(a.Pos) },
                new DroppedGear { Gear = Made(GearKinds.Supplies, null, 12), Pos = Events.CopyPos(a.Pos) },
            };
            a.Hp = Math.Max(1, a.MaxHp - 3);
            return s;
        }

        private static void Save(string name, CampaignState c)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, name), JsonConvert.SerializeObject(c, Formatting.Indented, Json.Settings) + "\n");
            // What is written reads back the same.
            Assert.Equal(Json.Write(c), Json.Write(Json.Read<CampaignState>(File.ReadAllText(Path.Combine(Dir, name)))));
        }

        /// <summary>A base of two with a stash and kits of everything (some turned, piles of supplies).</summary>
        public static CampaignState Full()
        {
            var c = Two(3);
            var stash = c.Stash!;
            Lay(c, stash, GearKinds.Supplies, null, 0, 0, 100);
            Lay(c, stash, GearKinds.Supplies, null, 1, 0, 100);
            Lay(c, stash, GearKinds.Supplies, null, 2, 0, 45);
            Lay(c, stash, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 3, 0);
            Lay(c, stash, GearKinds.Weapon, FoundWeaponIds.LightMachineGun, 3, 1).Mods = new List<string> { WeaponModIds.ExtMag };
            Lay(c, stash, GearKinds.Weapon, FoundWeaponIds.PrecisionRifle, 9, 0, turned: true);
            Lay(c, stash, GearKinds.Weapon, FoundWeaponIds.Katana, 8, 0, turned: true);
            Lay(c, stash, GearKinds.Vest, VestIds.Vest, 0, 1);
            Lay(c, stash, GearKinds.Vest, VestIds.HeavyVest, 0, 3);
            Lay(c, stash, GearKinds.Medicine, null, 2, 1, 4);
            Lay(c, stash, GearKinds.Mags, null, 2, 2, 5);
            Lay(c, stash, GearKinds.Mags, null, 2, 3, 2);
            Lay(c, stash, GearKinds.Item, ItemIds.FragGrenade, 7, 0, 3);
            Lay(c, stash, GearKinds.Item, ItemIds.SmokeGrenade, 7, 1);
            Lay(c, stash, GearKinds.Mod, WeaponModIds.Scope, 2, 4, 2);
            Lay(c, stash, GearKinds.Mod, WeaponModIds.Silencer, 3, 4);
            Lay(c, stash, GearKinds.Weapon, FoundWeaponIds.Pistol, 4, 3);
            Lay(c, stash, GearKinds.Weapon, FoundWeaponIds.Crowbar, 4, 5);
            Lay(c, stash, GearKinds.Weapon, FoundWeaponIds.SawnOff, 7, 2, turned: true);
            var a = Soldier(c, 0);
            InSlot(c, a, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.HuntingRifle).Mods = new List<string> { WeaponModIds.Stock, WeaponModIds.Scope };
            InSlot(c, a, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
            InSlot(c, a, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
            InSlot(c, a, KitSlots.Belt0, GearKinds.Item, ItemIds.FirstAid, 2);
            InSlot(c, a, KitSlots.Belt1, GearKinds.Item, ItemIds.Molotov, 3);
            InPack(c, a, GearKinds.Mags, null, 0, 0, 4);
            InPack(c, a, GearKinds.Medicine, null, 1, 0, 2);
            InPack(c, a, GearKinds.Supplies, null, 2, 0, 35);
            InPack(c, a, GearKinds.Weapon, FoundWeaponIds.Bat, 0, 1);
            var b = Soldier(c, 0, 1);
            InSlot(c, b, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.PumpShotgun);
            InSlot(c, b, KitSlots.Belt0, GearKinds.Item, ItemIds.FragGrenade, 2);
            InPack(c, b, GearKinds.Weapon, FoundWeaponIds.Machete, 3, 0, turned: true);
            var d = Soldier(c, 1);
            InSlot(c, d, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.MachineGun);
            InSlot(c, d, KitSlots.Vest, GearKinds.Vest, VestIds.HeavyVest);
            InPack(c, d, GearKinds.Item, ItemIds.Flashbang, 0, 0);
            InPack(c, d, GearKinds.Mags, null, 1, 0, 3);
            c.Upgrades![UpgradeIds.Stash] = 1;
            c.Stash!.H += Runs.StashRows;
            c.Upgrades[UpgradeIds.Sight] = 1;
            return c;
        }

        [SamplesFact]
        public void WriteTheSamples()
        {
            Save("base-vacia.json", Solo(3));
            Save("base-llena.json", Full());
            // A run at row 3, two nodes behind, a soldier hurt.
            var row3 = Full();
            // Without the «sight» upgrade: rows 6 and 7 out of sight.
            row3.Upgrades!.Remove(UpgradeIds.Sight);
            var run = Start(row3);
            for (var row = 1; row <= 3; row++)
            {
                // A fight where there is one (won by hand, as H3 will), else the stop, answered and left.
                var n = RunMap.Choices(run).FirstOrDefault((x) => RunMap.IsFight(x.Kind)) ?? RunMap.Choices(run)[0];
                Agree(row3, new ChooseNodeCommand { Node = n.Id });
                if (run.Phase == RunPhases.Fight)
                {
                    Runs.NodeCleared(row3, new Rng(row));
                    continue;
                }
                if (run.Stop!.Outcome == null && (n.Kind == NodeKinds.Event || n.Kind == NodeKinds.Survivor)) Agree(row3, new AnswerStopCommand { Option = 1 });
                Ok(row3, new LeaveStopCommand(), 0, Both);
                Ok(row3, new LeaveStopCommand(), 1, Both);
            }
            run.Hp[run.Squad[1]] = 2;
            Save("mapa-fila3.json", row3);
            var trade = Full();
            InPack(trade, Soldier(trade, 0, 1), GearKinds.Supplies, null, 0, 1, 60);
            GoTo(trade, NodeKinds.Trade);
            Save("trueque.json", trade);
            var ev = Full();
            GoTo(ev, NodeKinds.Event);
            Save("suceso.json", ev);
            var survivor = Full();
            GoTo(survivor, NodeKinds.Survivor);
            Save("superviviente.json", survivor);
            Save("combate-mochila.json", Combat());
        }
    }
}
