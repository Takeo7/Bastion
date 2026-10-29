#nullable enable
// campaign.ts: a new campaign (createCampaign, its opening mission), a partner who joins late (addPlayer), and the
// squad's places in the hangar as they change (joinSquad, syncPads).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ------------------------------------------------------------------- create

        private static List<RegionState> FreshRegions()
        {
            return RegionIds.All.Select((id) => new RegionState { Id = id, Contacted = id == HomeRegion, Facility = null }).ToList();
        }

        /// <summary>The base's first line, and the same once the opening operation is chosen (placeholders for Lore).</summary>
        /// <remarks>TypeScript's: «El Bastión entra en servicio. Elegid la primera misión.» (text-fixes.ts, Gameplay 24/10).</remarks>
        private const string Welcome = "Lo que se levante en este claro será el Bastión. Elegid la primera misión.";
        private const string WelcomeOpening = "Primera salida: la insulina del consultorio de Metsovo. La escuadra espera junto a La Lata.";

        /// <summary>
        /// A new campaign opens with a mission (playtest 2026-10-13): the first story
        /// operation is chosen and its squad picked, waiting in the hangar; the base
        /// and its needs come after. The server calls it on a campaign it has just
        /// created (createCampaign itself leaves the choice open).
        /// </summary>
        public static void OpeningMission(CampaignState c)
        {
            var first = c.Offers.FirstOrDefault((o) => o.Story == 1) ?? c.Offers.FirstOrDefault();
            if (first == null || c.ActiveMission != null) return;
            c.ActiveMission = first.Id;
            c.Squad = DefaultSquad(c, c.Players);
            c.LaunchReady = new bool[2];
            SyncPads(c);
            var welcome = c.Log.FirstOrDefault((l) => l.Text == Welcome);
            if (welcome != null) welcome.Text = WelcomeOpening;
        }

        /// <summary>Soldiers each player starts with.</summary>
        private const int StartKit = 4;
        /// <summary>Ready soldiers a commander keeps when a partner arrives: a squad share.</summary>
        private const int KeepReady = 3;

        public static CampaignState CreateCampaign(IReadOnlyList<int> players, Rng rng)
        {
            var bastion = Base.FreshBase(rng);
            var c = new CampaignState
            {
                Version = CampaignVersion,
                Day = 1,
                Players = new List<int>(players),
                Credits = 200,
                Xp = 0,
                Level = 1,
                Vehicle = 0,
                Soldiers = new List<CampaignSoldier>(),
                Inventory = new Dictionary<string, int> { [ItemIds.FragGrenade] = 2, [ItemIds.FirstAid] = 1 },
                Medicines = StartMedicines,
                NextMedicineDay = 1 + EnclaveDays,
                Treatment = null,
                Food = StartFood,
                Hungry = false,
                NextFoodDay = 1 + LaVegaFoodDays,
                Parts = 0,
                Crafted = new List<string>(),
                FoundWeapons = new List<FoundWeapon>(),
                Flooded = new List<string>(),
                Base = new BaseWorks(),
                Bastion = bastion,
                Siege = new SiegeClock { In = SiegeFirstIn, Count = SiegeOpening, Sides = new List<int>(), Held = 0 },
                Regions = FreshRegions(),
                Offers = new List<MissionOffer>(),
                ActiveMission = null,
                Squad = new List<string>(),
                Doom = 2,
                NextMissionDay = 2,
                NextDoomDay = 2 + DoomInterval,
                NextFacilityDay = 3,
                NextSupplyDay = 1 + Month,
                NextEventDay = 2,
                Event = null,
                AdvanceReady = new bool[2],
                LaunchReady = new bool[2],
                Proposal = null,
                Log = new List<LogEntry>(),
                LastReport = null,
                Outcome = null,
                Ending = null,
                Story = new StoryState { Done = 0, Simon = null },
                Quests = new List<QuestState>(),
                QuestLog = new JsMap<string, QuestLogEntry>(),
                QuestEvent = null,
                Rumors = new List<Rumor>(),
                Listening = null,
                NextRumorDay = 2,
                Techs = new List<string>(),
                Seen = new Dictionary<string, int>(),
                NextId = 0,
            };
            // Everyone starts as a recruit: classes come at Capitán.
            for (var i = 0; i < 2 * StartKit; i++) AddSoldier(c, players.Count > 1 ? players[i % 2] : players[0], rng);
            var a = MakeOffer(c, rng);
            var b = MakeOffer(c, rng);
            c.Offers.Add(a);
            c.Offers.Add(b);
            c.Siege.Sides = SiegeSides(rng);
            Log(c, Welcome, LogTones.Good);
            RefreshNarrative(c, rng);
            SyncMarea(c);
            c.Uid = Base36(Seed31(rng));
            return c;
        }

        // ----------------------------------------------------------------- the squad

        private static void JoinSquad(CampaignState c, CampaignSoldier soldier, SquadPlaceResult place)
        {
            // Dead code of the 0.10.1 (engine-cs README): since «each their own part» (Arturo, 18/10 #43) squadPlace never gives `evict`. Kept, as TypeScript has it.
            if (place.Evict != null)
            {
                var gone = c.Soldiers.First((s) => s.Id == place.Evict);
                c.Squad = c.Squad.Where((id) => id != place.Evict).ToList();
                Log(c, $"{gone.Name} deja su hueco a {soldier.Name}.");
            }
            c.Squad.Add(soldier.Id);
        }

        /// <summary>Writes down the pads the squad stands on now, so they stay put.</summary>
        public static void SyncPads(CampaignState c)
        {
            var pads = new Dictionary<string, int>();
            var all = SquadPads(c);
            for (var i = 0; i < all.Count; i++) if (all[i] != null) pads[all[i]!] = i;
            c.Pads = pads;
        }

        /// <summary>Two looks the same, field by field (TypeScript compares their JSON).</summary>
        private static bool SameLook(Appearance a, Appearance b)
        {
            return a.Primary == b.Primary && a.Secondary == b.Secondary && a.Visor == b.Visor && a.Head == b.Head && a.Build == b.Build && a.Sex == b.Sex
                && a.Helmet == b.Helmet && a.Torso == b.Torso && a.Arms == b.Arms && a.Legs == b.Legs && a.Character == b.Character;
        }

        /// <summary>
        /// Brings a player into a campaign started without them. They take over the
        /// other commander's untouched rookies (never deployed, so nothing is lost), and
        /// fresh recruits make up the rest of a kit. Returns how many soldiers they got.
        /// </summary>
        public static int AddPlayer(CampaignState c, int slot, Rng rng)
        {
            if (c.Players.Contains(slot)) return 0;
            c.Players = c.Players.Append(slot).OrderBy((p) => p).ToList();
            // Untouched rookies change hands, but never the reserves the commander already there
            // needs: they keep at least a squad share of ready soldiers (UX-REVISION-2 #37).
            // Whatever is missing comes as fresh recruits.
            int Ready(int owner) => c.Soldiers.Count((s) => s.Owner == owner && SoldierReady(s));
            var rookies = new List<CampaignSoldier>();
            foreach (var s in Enumerable.Reverse(c.Soldiers.Where((x) => x.Owner != slot && x.Alive && x.Missions == 0).ToList()))
            {
                if (rookies.Count >= LateKit) break;
                if (SoldierReady(s) && Ready(s.Owner) - rookies.Count((r) => r.Owner == s.Owner && SoldierReady(r)) <= KeepReady) continue;
                rookies.Add(s);
            }
            // A squad being picked is redone for both by whoever calls this (see the server's bringPlayersIn).
            c.Squad = c.Squad.Where((id) => !rookies.Any((s) => s.Id == id)).ToList();
            foreach (var s in rookies)
            {
                var sex = s.Appearance.Sex;
                var wasDefault = SameLook(s.Appearance, Appearances.DefaultAppearance(s.Owner, null, sex, s.Id));
                s.Owner = slot;
                if (wasDefault) s.Appearance = Appearances.DefaultAppearance(slot, null, sex, s.Id);
                s.Loadout = new Loadout();
            }
            var recruits = new List<CampaignSoldier>();
            for (var i = rookies.Count; i < LateKit; i++) recruits.Add(AddSoldier(c, slot, rng));
            // Whoever was already there learns who changed hands, and who joined.
            var moved = rookies.Count > 0 ? $"Pasan a su mando: {ListOf(rookies.Select((s) => s.Name).ToList())}." : "";
            var joined = recruits.Count > 0 ? $"{(rookies.Count > 0 ? "Además, se alistan" : "Se alistan a sus órdenes")}: {ListOf(recruits.Select((s) => s.Name).ToList())}." : "";
            // «Llega el segundo comandante» (text-fixes.ts, Lore 24/10): the partner may be Ana.
            Log(c, string.Join(" ", new[] { "Se une el otro jugador.", moved, joined }.Where((x) => x != "")), LogTones.Good, Alert("RELEVO", "Se une el otro jugador"));
            return LateKit;
        }
    }
}
