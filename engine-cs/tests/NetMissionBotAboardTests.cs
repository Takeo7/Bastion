using System.Linq;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// R9 (Red, 18/11; Producto, by Gameplay): the bench's chapter of seed 28 stuck in a fight with G22's test map, «Ana:
    /// Primero termina el turno de Arjun Mehta, o cede el mando», for ever. One aboard La Lata gets its actions at the turn's
    /// start, to come down (T24): the rules count it as the partner's go (Sim.HasActions, Alternates) and refuse another
    /// of Ana's while hers in play has its turn left (InPlay), but MissionBot only counted the partner's on the field
    /// (PartnerPlays): Ana's bot didn't hand over the command, tried her other one, was refused, and Pepe, without the
    /// command, never ended his turn. The bot now asks the rules (Sim.Alternates).
    /// </summary>
    public class NetMissionBotAboardTests
    {
        /// <summary>
        /// A run's fight of four (4 seats): Ana's two on the field, the first in play with its actions spent and `moves` of
        /// its walk left (null: all); Pepe's two in La Lata with this turn's actions. Ana has the command.
        /// </summary>
        private static (GameState S, Unit InPlay, Unit Other) Aboard(int? moves)
        {
            var (_, s) = LaunchAt(NodeKinds.Normal, 1, before: (c) =>
            {
                c.Upgrades![UpgradeIds.Squad] = 1;
                c.Squad = Runs.DefaultSquad(c, c.Players);
            }, node: (n) => { while (RunMap.LooseSurvivor(n)) n.Seed++; });
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ToList();
            Assert.Equal(4, squad.Count);
            for (var i = 0; i < 4; i++) squad[i].Owner = i < 2 ? 0 : 1;
            foreach (var p in squad.Skip(2))
            {
                Events.ApplyEvent(s, new BoardedEvent { Unit = p.Id });
                p.Ap = Content.ApPerTurnOf(p);
            }
            Assert.True(Rules.LaLataHere(s));
            var inPlay = squad[0];
            inPlay.Ap = 0;
            inPlay.Moves = moves;
            Assert.True(Paths.HasTurnLeft(inPlay));
            s.ActiveUnit = inPlay.Id;
            s.Command = 0;
            s.Ready = new bool[2];
            return (s, inPlay, squad[1]);
        }

        /// <summary>Whatever walk the one in play has left, Ana's bot is never refused there (the stuck chapter's refusal).</summary>
        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(null)]
        public void ItsOrderIsTaken(int? moves)
        {
            var (s, _, _) = Aboard(moves);
            var cmd = MissionBot.Next(s, 0, Both);
            Assert.NotNull(cmd);
            var r = Exec(s, cmd!, connected: Both);
            Assert.True(r.Ok, $"{Bastion.Net.Json.Write(cmd)}: {r.Error}");
        }

        [Fact]
        public void ItHandsOverTheCommandWhenThePartnersGoIsAboard()
        {
            var (s, inPlay, mate) = Aboard(null);
            var squad = new[] { inPlay, mate };

            // The rules: Ana's other one waits while hers in play has its turn and Pepe has his go.
            var other = Exec(s, new AbilityCommand { Unit = squad[1].Id, Ability = AbilityIds.Hunker }, connected: Both);
            Assert.False(other.Ok);
            Assert.Equal($"Primero termina el turno de {inPlay.Name}, o cede el mando.", other.Error);

            // The bot: the command to Pepe; his, with nobody on the field, ends his turn; the command back to Ana, and
            // her next order taken.
            var hand = MissionBot.Next(s, 0, Both);
            Assert.IsType<PassCommand>(hand);
            Assert.True(Exec(s, hand!, connected: Both).Ok);
            Assert.Equal(1, s.Command);
            var done = MissionBot.Next(s, 1, Both);
            Assert.True(done is EndTurnCommand { Ready: true });
            Assert.True(Exec(s, done!, slot: 1, connected: Both).Ok);
            Assert.Equal(0, s.Command);
            var next = MissionBot.Next(s, 0, Both);
            Assert.NotNull(next);
            Assert.True(Exec(s, next!, connected: Both).Ok);
        }
    }
}
