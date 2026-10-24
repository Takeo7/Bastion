// transmissions.ts: its types and ids (the data and the functions are in Transmissions.cs).
using System;
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>SpeakerId. `coro` is the nameless voice on the Congregación's frequency (the id predates the zombie lore).</summary>
    public static class SpeakerIds
    {
        public const string Morse = "morse";
        public const string Teo = "teo";
        public const string Nwosu = "nwosu";
        public const string Simon = "simon";
        public const string Voz = "voz";
        public const string Halvorsen = "halvorsen";
        public const string Coro = "coro";

        public static readonly IReadOnlyList<string> All = new[] { Morse, Teo, Nwosu, Simon, Voz, Halvorsen, Coro };
    }

    public sealed class RadioSpeaker
    {
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public sealed class SceneLine
    {
        public string Who { get; set; } = ""; // SpeakerIds
        public string Text { get; set; } = "";
        /// <summary>The rule behind it, in small print under the line (the radio's warnings).</summary>
        public string? Rule { get; set; }
        /// <summary>Said only when this holds (Simão speaks only if he was let in).</summary>
        public Func<CampaignState, bool>? If { get; set; }
    }

    /// <summary>SceneId, in story order; `recap` is built on the spot, for whoever opens a campaign already under way.</summary>
    public static class SceneIds
    {
        public const string Council = "council";
        public const string Ch1After = "ch1After";
        public const string Ch2Before = "ch2Before";
        public const string Ch2After = "ch2After";
        public const string Ch3Before = "ch3Before";
        public const string Ch3After = "ch3After";
        public const string FirstFlood = "firstFlood";
        public const string Partitura = "partitura";
        public const string Simon = "simon";
        public const string Ch5After = "ch5After";
        public const string Velmar = "velmar";
        public const string FirstBite = "firstBite";
        public const string ShortMedicines = "shortMedicines";
        public const string FirstTurned = "firstTurned";
        public const string Hunger = "hunger";
        public const string FoundWeapon = "foundWeapon";
        public const string Confession = "confession";
        public const string Ch6After = "ch6After";
        public const string Contranota = "contranota";
        public const string Voz = "voz";
        public const string Eve = "eve";
        public const string EndingBreak = "endingBreak";
        public const string EndingRecord = "endingRecord";
        public const string GranCoral = "granCoral";
        public const string Recap = "recap";

        public static readonly IReadOnlyList<string> All = new[] { Council, Ch1After, Ch2Before, Ch2After, Ch3Before, Ch3After, FirstFlood, Partitura, Simon, Ch5After, Velmar, FirstBite, ShortMedicines, FirstTurned, Hunger, FoundWeapon, Confession, Ch6After, Contranota, Voz, Eve, EndingBreak, EndingRecord, GranCoral, Recap };
    }

    public sealed class RadioScene
    {
        public string Id { get; set; } = ""; // SceneIds
        public string Title { get; set; } = "";
        /// <summary>Where it is heard: frequency, line or room.</summary>
        public string Channel { get; set; } = "";
        /// <summary>Unlocked once this holds; it never locks again.</summary>
        public Func<CampaignState, bool> When { get; set; } = c => false;
        public IReadOnlyList<SceneLine> Lines { get; set; } = new SceneLine[0];
    }
}
