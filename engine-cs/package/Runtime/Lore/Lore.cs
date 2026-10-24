using System;

namespace Bastion.Engine
{
    /// <summary>
    /// The story (docs/LORE.md): mission briefings, the Diario pages, the story
    /// chapters and the epilogues. Apart from the chapters' mission setup (`STORY`),
    /// which the campaign reads, it is text only.
    /// </summary>
    /// <remarks>
    /// lore.ts in C#, a section a file (engine-cs/README.md). Grouped texts are
    /// nested static classes, so none of them depends on the order the partial
    /// files are initialised in.
    /// </remarks>
    public static partial class Lore
    {
        /// <summary>Bastion level at which the doom track gets its name: until the radio is fixed nobody knows the Marea is coming.</summary>
        public const int PartituraLevel = 4;

        /// <summary>
        /// The squad's bus (docs/LORE.md «El Bastión»), named once for every text that
        /// names it: `to` and `of` contract like a zone's, should it ever start with «El».
        /// </summary>
        public static class Bus
        {
            public const string Name = "La Lata";
            public static readonly string To = ToZone(Name);
            public static readonly string Of = OfZone(Name);
            public const string In = "en " + Name;
        }

        /// <summary>The doom track as a label: "La Marea" once known.</summary>
        public static string DoomName(int level)
        {
            return level >= PartituraLevel ? "La Marea" : "Infestación";
        }

        /// <summary>The doom track inside a sentence: "la Marea".</summary>
        public static string DoomSubject(int level)
        {
            return level >= PartituraLevel ? "la Marea" : "la infestación";
        }

        /// <summary>The doom track at a glance: "La Marea, a 9 km" once known, "Infestación 3/12" before.</summary>
        public static string DoomStatus(int level, int doom)
        {
            return level >= PartituraLevel ? $"La Marea, a {Math.Max(0, DoomTrack - doom)} km" : $"Infestación {doom}/{DoomTrack}";
        }

        /// <summary>Length of the doom track (campaign.ts DOOM_MAX): the Marea starts this many km away.</summary>
        private const int DoomTrack = 12;

        /// <summary>"La Marea se acerca 1 km." / "La infestación baja (−2)."</summary>
        public static string DoomChangeText(int level, int delta)
        {
            if (level >= PartituraLevel) return delta > 0 ? $"La Marea se acerca {delta} km." : $"La Marea retrocede {-delta} km.";
            return delta > 0 ? $"La infestación crece (+{delta})." : $"La infestación baja (−{-delta}).";
        }

        /// <summary>
        /// A soldier's sight by day and in the dark, and how close a zombie notices a
        /// hidden one by day and in the dark (los.ts nightSight, detectionTo). A
        /// nested class, so Content is read when a text needs it.
        /// </summary>
        private static class Sight
        {
            public static readonly int Day = Content.Templates[TemplateIds.Rookie].Sight;
            public static readonly int Night = Math.Max(Content.Night.MinSight, Day - Content.Night.SightCut);
            public static readonly int ZombieNotice = Content.Templates[TemplateIds.Zombie].Detection;
            public static readonly int ZombieNoticeDark = (int)Js.Round(ZombieNotice * Content.Night.Detection);
        }

        /// <summary>«puntería con armas de fuego»: JavaScript's `s.charAt(0).toLowerCase() + s.slice(1)`.</summary>
        private static string LowerFirst(string s)
        {
            return s.Length == 0 ? s : s.Substring(0, 1).ToLowerInvariant() + s.Substring(1);
        }

        /// <summary>«Ana»: JavaScript's `s.charAt(0).toUpperCase() + s.slice(1)`.</summary>
        private static string Capital(string s)
        {
            return s.Length == 0 ? s : s.Substring(0, 1).ToUpperInvariant() + s.Substring(1);
        }
    }
}
