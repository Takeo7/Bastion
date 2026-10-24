using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// What the step-2 tests (rules, ai, aim, abilities, skills, loot, events) share, as the TypeScript tests write it
    /// file by file: a pod of units, the events of one kind, a Sim, a command run. Step1Helpers has the rest (stateFrom,
    /// withLevels, addUnit, P).
    /// </summary>
    public static class Step2Helpers
    {
        /// <summary>`pod(s, id, units)`: a pod of these units, awake (or not), with no round to walk.</summary>
        public static Pod Pod(GameState s, string id, IEnumerable<Unit> units, bool active = true)
        {
            var list = units.ToList();
            var p = new Pod { Id = id, Active = active, UnitIds = list.Select((u) => u.Id).ToList(), Patrol = new List<Pos>(), PatrolIndex = 0 };
            foreach (var u in list) u.PodId = id;
            s.Pods.Add(p);
            return p;
        }

        /// <summary>`of(events, t)`: the events of one kind, in order.</summary>
        public static List<T> Of<T>(IEnumerable<GameEvent> events) where T : GameEvent
        {
            return events.OfType<T>().ToList();
        }

        /// <summary>The tags of the events, in order (`events.map((e) => e.t)`).</summary>
        public static List<string> Tags(IEnumerable<GameEvent> events)
        {
            return events.Select((e) => e.T).ToList();
        }

        /// <summary>`new Sim(s, new Rng(seed), connected)` (connected: [0] when not given).</summary>
        public static Sim NewSim(GameState s, int seed = 1, params int[] connected)
        {
            return new Sim(s, new Rng(seed), connected.Length > 0 ? connected : new[] { 0 });
        }

        /// <summary>`executeCommand(s, cmd, { slot, connected, rng: new Rng(seed) })` (connected: [slot] when not given).</summary>
        public static CommandResult Exec(GameState s, Command cmd, int slot = 0, int seed = 1, int[]? connected = null)
        {
            return Rules.ExecuteCommand(s, cmd, slot, connected ?? new[] { slot }, new Rng(seed));
        }

        /// <summary>The same with a Rng of the caller's (kept from one command to the next).</summary>
        public static CommandResult Exec(GameState s, Command cmd, Rng rng, int slot = 0, int[]? connected = null)
        {
            return Rules.ExecuteCommand(s, cmd, slot, connected ?? new[] { slot }, rng);
        }

        public static MoveCommand Move(string unit, int x, int y, int z = 0)
        {
            return new MoveCommand { Unit = unit, To = new Pos { X = x, Y = y, Z = z } };
        }

        public static AbilityCommand Ability(string unit, string ability, string? target = null, Pos? tile = null)
        {
            return new AbilityCommand { Unit = unit, Ability = ability, Target = target, Tile = tile };
        }
    }
}
