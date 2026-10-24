using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Bastion.Engine;
using Newtonsoft.Json.Linq;

namespace Bastion.Net
{
    /// <summary>
    /// What the room asks of the rules (room.ts imports them from the engine): to make a mission, play a command, and
    /// the turn's changes that come from the host. Behind an interface so the room can be tested on its own, with rules
    /// of its own; the game's are the engine's (rules.ts, setup.ts), once in C#. `connected`: the seats that count, by
    /// slot (Room.ActiveSlots).
    /// </summary>
    public interface IRules
    {
        /// <summary>setup.ts createMission, for a skirmish.</summary>
        GameState CreateMission(SkirmishOptions options, Rng rng);

        /// <summary>rules.ts executeCommand: the command of `slot`, or why not.</summary>
        CommandResult ExecuteCommand(GameState state, Command cmd, int slot, IReadOnlyList<int> connected, Rng rng);

        /// <summary>rules.ts syncTurn: the squad's turn ends if whoever was holding it up is gone.</summary>
        List<GameEvent> SyncTurn(GameState state, IReadOnlyList<int> connected, Rng rng);

        /// <summary>rules.ts callAbandon: `slot` asks to leave the mission (alone, it ends now).</summary>
        List<GameEvent> CallAbandon(GameState state, int slot, IReadOnlyList<int> connected);

        /// <summary>rules.ts answerAbandon.</summary>
        List<GameEvent> AnswerAbandon(GameState state, int slot, bool accept);

        /// <summary>rules.ts runOutOfTime: `slot`'s turn clock is out; their soldiers take cover.</summary>
        List<GameEvent> RunOutOfTime(GameState state, int slot, IReadOnlyList<int> connected, Rng rng);
    }

    /// <summary>
    /// The engine's rules (Rules.cs and Setup.cs, rules.ts and setup.ts in C#), as the room asks for them. `create`, to
    /// make missions otherwise: a recorded one (Recorded), for tests.
    /// </summary>
    public sealed class EngineRules : IRules
    {
        private readonly Func<SkirmishOptions, Rng, GameState> create;

        public EngineRules(Func<SkirmishOptions, Rng, GameState>? create = null)
        {
            this.create = create ?? ((options, rng) => Setup.CreateMission(MissionOptionsOf(options, rng)));
        }

        /// <summary>What room.ts startSkirmish asks createMission for (the difficulty, Setup's own: 2).</summary>
        public static MissionOptions MissionOptionsOf(SkirmishOptions options, Rng rng)
        {
            var map = options.MapKind == MapSourceKinds.Handmade ? MapSource.Handmade(options.Id ?? "") : MapSource.Generated(options.Biome ?? "", options.Seed, options.Site);
            return new MissionOptions { Map = map, Slots = options.Slots, Rng = rng, Kind = options.Kind, Veterans = options.Veterans, Night = options.Night };
        }

        /// <summary>
        /// Every mission the one in `path`, as it was recorded: a state (a save's `state`, a snapshot's) or a recorded game of
        /// golden/ (its starting `state`). Read afresh each time, so each new mission starts over: for tests between two apps
        /// on a known mission (NetLink, BASTION_MISION).
        /// </summary>
        public static EngineRules Recorded(string path)
        {
            return new EngineRules((options, rng) =>
            {
                var file = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                var state = file["state"] is JObject inner ? inner : file;
                return state.ToObject<GameState>(Json.Serializer) ?? throw new InvalidDataException($"{path}: no es una misión.");
            });
        }

        public GameState CreateMission(SkirmishOptions options, Rng rng) => create(options, rng);

        public CommandResult ExecuteCommand(GameState state, Command cmd, int slot, IReadOnlyList<int> connected, Rng rng) => Rules.ExecuteCommand(state, cmd, slot, connected, rng);

        public List<GameEvent> SyncTurn(GameState state, IReadOnlyList<int> connected, Rng rng) => Rules.SyncTurn(state, connected, rng);

        public List<GameEvent> CallAbandon(GameState state, int slot, IReadOnlyList<int> connected) => Rules.CallAbandon(state, slot, connected);

        public List<GameEvent> AnswerAbandon(GameState state, int slot, bool accept) => Rules.AnswerAbandon(state, slot, accept);

        public List<GameEvent> RunOutOfTime(GameState state, int slot, IReadOnlyList<int> connected, Rng rng) => Rules.RunOutOfTime(state, slot, connected, rng);
    }

    /// <summary>A skirmish's MissionOptions (setup.ts): its map (a MapSource) and how it is played.</summary>
    public sealed class SkirmishOptions
    {
        /// <summary>MapSource's `kind`: handmade (by `Id`) or generated (by `Biome`, `Seed` and `Site`).</summary>
        public string MapKind { get; set; } = "generated";
        public string? Id { get; set; }
        public string? Biome { get; set; } // Biomes
        public int Seed { get; set; }
        public string? Site { get; set; } // Sites
        public List<int> Slots { get; set; } = new List<int>();
        public string Kind { get; set; } = MissionKinds.Elimination;
        public bool Veterans { get; set; }
        public bool Night { get; set; }
    }
}
