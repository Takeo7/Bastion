#nullable enable
// campaign.ts: the regions and the Marea, what can be read of them (REGIONS and ROADS are in Campaign.cs). syncMarea and
// tide, which change the state, come with the rules.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ------------------------------------------------------------------ regions

        /// <summary>The shortest way by road from `a` to `b`, both included ([a] when they are the same); La Lata drives it. Ties go by the order of each zone's roads.</summary>
        public static List<string> RoadRoute(string a, string b)
        {
            var prev = new JsMap<string, string?>();
            prev.Set(a, null);
            var queue = new List<string> { a };
            for (var k = 0; k < queue.Count && !prev.Has(b); k++)
            {
                foreach (var n in Regions[queue[k]].Roads)
                {
                    if (prev.Has(n)) continue;
                    prev.Set(n, queue[k]);
                    queue.Add(n);
                }
            }
            var route = new List<string>();
            for (string? at = b; at != null; at = prev.Get(at, null)) route.Insert(0, at);
            return route;
        }

        /// <summary>Road stretches on the shortest way from `a` to `b`.</summary>
        public static int RoadHops(string a, string b)
        {
            return RoadRoute(a, b).Count - 1;
        }

        // ----------------------------------------------------------------- the Marea

        /// <summary>Nidos gather under the Marea or beside it this many times as often as elsewhere.</summary>
        private const int NidoTideWeight = 3;

        /// <summary>The activity at which `id` falls under the Marea; null for Bielsa.</summary>
        public static int? MareaThreshold(string id)
        {
            if (id == HomeRegion) return null;
            return MareaThresholds[Math.Min(RoadHops(MareaSource, id), MareaThresholds.Count - 1)];
        }

        /// <summary>Under the Marea: its enclave sends nothing, its missions bring one more group of the dead and it can't be newly contacted. It recedes when the activity drops.</summary>
        /// <remarks>`c: Pick&lt;CampaignState, 'doom'&gt;`: the other overload takes the activity alone.</remarks>
        public static bool UnderMarea(CampaignState c, string id)
        {
            return UnderMarea(c.Doom, id);
        }

        public static bool UnderMarea(int doom, string id)
        {
            var t = MareaThreshold(id);
            return t != null && doom >= t.Value;
        }

        public static List<string> FloodedZones(CampaignState c)
        {
            return FloodedZones(c.Doom);
        }

        public static List<string> FloodedZones(int doom)
        {
            return RegionIds.All.Where((id) => UnderMarea(doom, id)).ToList();
        }

        /// <summary>Zones a road joins to Bielsa or to a zone already contacted: the radio reaches as far as the people do.</summary>
        private static bool RoadReachable(CampaignState c, string id)
        {
            return Regions[id].Roads.Any((n) => n == HomeRegion || c.Regions.FirstOrDefault((r) => r.Id == n)?.Contacted == true);
        }

        /// <summary>Why `id` can't be contacted now, or null (docs/FASE5.md: only by road, and never under the Marea).</summary>
        public static string? ContactBlocker(CampaignState c, string id)
        {
            var region = c.Regions.FirstOrDefault((r) => r.Id == id);
            if (region == null || region.Contacted) return "Ya tenemos contacto con esa zona.";
            if (UnderMarea(c, id)) return Lore.FloodedContactReason(Progression.BastionLevel(c.Level), Regions[id].Name);
            if (!RoadReachable(c, id))
            {
                // The zones on the way that can be contacted now; failing those, the first one on the road there.
                bool Contacted(string n) => c.Regions.FirstOrDefault((r) => r.Id == n)?.Contacted == true;
                var next = Regions[id].Roads.Where((n) => !Contacted(n) && RoadReachable(c, n) && !UnderMarea(c, n)).ToList();
                var route = RoadRoute(HomeRegion, id).FirstOrDefault((n) => !Contacted(n));
                return Lore.NoRoadReason((next.Count > 0 ? next : route != null ? new List<string> { route } : new List<string>()).Select((n) => Regions[n].Name).ToList());
            }
            if (c.Regions.Count((r) => r.Contacted) >= ContactCapacity(c)) return "No caben más contactos: construid o mejorad la Sala de radio.";
            if (!CanAfford(c, ContactCost(c))) return "No hay suministros suficientes.";
            return null;
        }

        public static int ContactCapacity(CampaignState c)
        {
            var quests = QuestOutcomes(c).Sum((q) => q.Outcome.Contacts ?? 0);
            var relays = FacilityPieces(c, FacilityIds.Relay).Sum((f) => 1 + f.Level);
            return 2 + relays + (Progression.HasTech(c.Level, c.Techs, TechIds.Encryption) ? 1 : 0) + quests;
        }

        public static int ContactCost(CampaignState c)
        {
            return 30 + 20 * c.Regions.Count((r) => r.Contacted);
        }
    }
}
