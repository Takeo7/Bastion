using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// N2 (17/11): a Rescate alone among the dead against one with Cuervos, played by MissionBot for both players as the
    /// test apps do, by difficulty (rows 3–4: 2, row 5: 3) and arms (the knife, as the squad mostly goes; rifles): won,
    /// survivor brought out, soldiers fallen. For how many Rescates have Cuervos and from which row (RunMap.GuardedFrom,
    /// GuardedOdds). Only with BASTION_BANCO_RESCATE, the folder it writes to.
    /// </summary>
    public class RescueBench
    {
        private const int Seeds = 12;

        [Fact]
        public void AloneAgainstGuarded()
        {
            var dir = Environment.GetEnvironmentVariable("BASTION_BANCO_RESCATE");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            var rows = new List<string>();
            var cases = (from difficulty in new[] { 2, 3 } from rifles in new[] { false, true } from guarded in new[] { false, true } select (difficulty, rifles, guarded)).ToList();
            var results = new (int Won, int Out, int Fallen, int Turns)[cases.Count];
            Parallel.For(0, cases.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (k) =>
            {
                var (difficulty, rifles, guarded) = cases[k];
                int won = 0, out_ = 0, fallen = 0, turns = 0;
                for (var seed = 1; seed <= Seeds; seed++)
                {
                    var c = Two(seed);
                    c.Upgrades![UpgradeIds.Squad] = 2;
                    c.Squad = Runs.DefaultSquad(c, c.Players);
                    if (rifles)
                        foreach (var x in c.Soldiers)
                        {
                            InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                            InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                            InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
                        }
                    var run = Start(c, seed);
                    var n = RunMap.Choices(run)[0];
                    n.Kind = NodeKinds.Rescue;
                    n.Modifier = null;
                    n.Difficulty = difficulty;
                    n.Guarded = guarded ? true : (bool?)null;
                    Agree(c, new ChooseNodeCommand { Node = n.Id }, 0, seed);
                    var s = new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
                    var rng = new Rng(seed * 7919 + 13);
                    int orders = 0, idle = 0;
                    while (s.Outcome == null && orders < 3000 && idle < 3)
                    {
                        var any = false;
                        foreach (var slot in Both)
                        {
                            var cmd = MissionBot.Next(s, slot, Both);
                            if (cmd == null) continue;
                            orders++;
                            if (Rules.ExecuteCommand(s, cmd, slot, Both, rng).Ok) any = true;
                        }
                        idle = any ? 0 : idle + 1;
                    }
                    if (s.Outcome == Outcomes.Victory) won++;
                    var vip = s.Units.Single((u) => u.Id == s.Mission.Survivor);
                    if (vip.Evacuated) out_++;
                    fallen += s.Units.Count((u) => u.CampaignId != null && !u.Evacuated);
                    turns += s.Turn;
                }
                results[k] = (won, out_, fallen, turns);
            });
            var sb = new StringBuilder();
            sb.AppendLine($"Rescate, {Seeds} semillas cada caso, dos jugadores con 2 soldados cada uno y 6 plazas (MissionBot):");
            sb.AppendLine("| Dificultad | Armas | Cuervos | Ganados | Superviviente sacado | Caídos por combate | Turnos |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            for (var k = 0; k < cases.Count; k++)
            {
                var (difficulty, rifles, guarded) = cases[k];
                var r = results[k];
                sb.AppendLine($"| {difficulty} | {(rifles ? "fusiles" : "cuchillos")} | {(guarded ? "sí" : "no")} | {r.Won} de {Seeds} | {r.Out} de {Seeds} | {(double)r.Fallen / Seeds:0.0} | {(double)r.Turns / Seeds:0} |");
            }
            File.WriteAllText(Path.Combine(dir, "rescate.md"), sb.ToString());
        }
    }
}
