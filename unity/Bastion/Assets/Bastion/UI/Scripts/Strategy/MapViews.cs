using System;
using System.Collections.Generic;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The Mapa (screen.ts renderGeoscape, geoscape.ts): the valley with its zones' markers, the Marea at the campaign's
    /// activity and La Lata at its zone; on the left the missions, waiting a day, the Nidos, the errands, the rumours and
    /// the network of enclaves; on the right the mission picked or the zone.
    /// </summary>
    [Serializable]
    public sealed class MapView
    {
        /// <summary>The Marea's activity (the campaign's doom).</summary>
        public float Level;
        public List<ZoneMarkView> Zones = new List<ZoneMarkView>();
        /// <summary>La Lata's zone (the active mission's, else the Bastion's).</summary>
        public string Lata;
        /// <summary>What La Lata is now (Campaign.VehicleOf: todoterreno, furgoneta, camión, autobús).</summary>
        public int Vehicle = 1;
        /// <summary>The zone picked: kept clear of the sheet on the right.</summary>
        public string Selected;
        public List<MissionRowView> Missions = new List<MissionRowView>();
        public string MissionsEmpty;
        public WaitDayView Wait;
        public string NestsTip;
        public List<MapRowView> Nests = new List<MapRowView>();
        public string QuestsTip;
        public List<MapRowView> Quests = new List<MapRowView>();
        public string RumorsTip;
        public List<RumorRowView> Rumors = new List<RumorRowView>();
        public int Contacts;
        public int ContactsMax;
        public string ContactsTip;
        public MapDetailView Detail;
    }

    /// <summary>A zone on the valley: its pin, its name, what there is in it (a Nido, the operations, a rumour).</summary>
    [Serializable]
    public sealed class ZoneMarkView
    {
        public string Id;
        public string Name;
        public bool Contacted;
        public bool Flooded;
        public bool Selected;
        public bool Nest;
        /// <summary>The operations' tones and kinds, in order.</summary>
        public List<string> Missions = new List<string>();
        public List<string> Kinds = new List<string>();
        public bool Rumor;
        public bool Listening;
    }

    /// <summary>A mission in the list: its pin (tone and kind), name, where, its days chip.</summary>
    [Serializable]
    public sealed class MissionRowView
    {
        public string Id;
        public string Tone;
        public string Kind;
        public string Name;
        public string Where;
        public string Chip;
        public string ChipTone;
        public string ChipTip;
        public bool Selected;
        public string Tip;
    }

    /// <summary>A row of the Nidos or the errands: a name, a line under it, a chip.</summary>
    [Serializable]
    public sealed class MapRowView
    {
        public string Id;
        public string Name;
        public string Line;
        public string Chip;
        public string ChipTone;
        public string ChipTip;
        public bool Selected;
    }

    [Serializable]
    public sealed class RumorRowView
    {
        public string Id;
        public string Region;
        public string Text;
        /// <summary>«Escuchando · 2 misiones»; null, not listened to.</summary>
        public string Listening;
        /// <summary>«Proponer escuchar · 2 misiones»; null with Listening.</summary>
        public string Button;
        public bool Enabled = true;
        public string Why;
        public string Chip;
        public string ChipTone;
        public bool Selected;
    }

    /// <summary>«Esperar un día»: a key of both players (advance), what is missing, why not.</summary>
    [Serializable]
    public sealed class WaitDayView
    {
        public string Label;
        public bool Ready;
        /// <summary>The partner has asked: the button in the accent.</summary>
        public bool Asked;
        public bool Enabled = true;
        public string Why;
        public string Note;
        public string Tip;
    }

    /// <summary>The sheet on the right: a mission's briefing or a zone's info.</summary>
    [Serializable]
    public sealed class MapDetailView
    {
        public string Kicker;
        /// <summary>The kicker's tone: final, nest, story, quest or null.</summary>
        public string KickerTone;
        public string Title;
        public string Subtitle;
        /// <summary>LIVING_TIP, the flooded zone's note… in amber.</summary>
        public string Warning;
        public List<string> Text = new List<string>();
        /// <summary>The offer's signature, handwritten.</summary>
        public string Note;
        /// <summary>A zone's small lines under its text: who is there and what they send, its errand, its Nido.</summary>
        public List<MapLineView> Lines = new List<MapLineView>();
        /// <summary>A zone's missions, each to its briefing (Arturo, 26/10: a zone opens its sheet with all of them).</summary>
        public List<MissionRowView> Missions = new List<MissionRowView>();
        public List<RowView> Rows = new List<RowView>();
        /// <summary>The network's meter under a contact (filled, of).</summary>
        public int Meter;
        public int MeterMax;
        public List<MapButtonView> Buttons = new List<MapButtonView>();
        public string Why;
    }

    [Serializable]
    public sealed class MapLineView
    {
        public string Text;
        /// <summary>bad (a Nido, a zone under the Marea) or null, muted.</summary>
        public string Tone;
        public string Tip;
    }

    [Serializable]
    public sealed class MapButtonView
    {
        /// <summary>deploy, squad, cancel, contact.</summary>
        public string Action;
        public string Label;
        public bool Primary;
        public bool Enabled = true;
    }
}
