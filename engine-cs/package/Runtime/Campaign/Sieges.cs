#nullable enable
// campaign.ts: the base and the sieges (docs/FASE12.md §11), what can be read of them: what raising a building costs,
// what the Sala de radio hears, where a defender may stand, the map and plan of a siege. Starting one, taking the base
// back after it and its fall come with the rules.
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ------------------------------------------------------------------ the base

        /// <summary>The answer to choosing another mission, or picking the squad, while the siege waits.</summary>
        private const string SiegeNow = "Viene el asedio: no hay otra cosa que hacer.";

        /// <summary>Why the base can't pay `supplies` and `parts`, or null.</summary>
        private static string? CostBlocker(CampaignState c, int supplies, int parts)
        {
            if (!CanAfford(c, supplies)) return "No hay suministros suficientes.";
            if ((c.Parts ?? 0) < parts) return "No hay piezas suficientes.";
            return null;
        }

        /// <summary>
        /// What raising a building of the base a level costs, and the Bastion's level
        /// it needs: its facility's (upgradeCost). The Sala de radio rises into the
        /// Centro de comunicaciones and then the Estación de escucha (§11.3).
        /// </summary>
        public static PieceRaise RaiseCost(BasePiece p)
        {
            var def = Base.Pieces[p.Piece];
            if (def.Kind != PieceKinds.Building || p.Piece == PieceIds.Caravan) return new PieceRaise { Error = "Eso no se mejora." };
            var level = p.Level ?? 1;
            if (level >= 3) return new PieceRaise { Error = "Ya está al máximo." };
            if (p.Piece == PieceIds.Radio)
            {
                var step = Facilities[level == 1 ? FacilityIds.Relay : FacilityIds.Listening];
                return new PieceRaise { Cost = step.Cost, Level = step.Level };
            }
            var next = UpgradeCost(def.Facility!, level + 1);
            return new PieceRaise { Cost = next.Cost, Level = next.Level };
        }

        // ---------------------------------------------------------------- the sieges

        /// <summary>What the base knows of the next siege (§11.5): when, always; how many and by where, only with the Sala de radio.</summary>
        public static SiegeNews SiegeHeard(CampaignState c)
        {
            return HasRadio(c) ? new SiegeNews { In = c.Siege.In, Count = c.Siege.Count, Sides = new List<int>(c.Siege.Sides) } : new SiegeNews { In = c.Siege.In };
        }

        /// <summary>
        /// Why `soldier` can't stand at `at` when the siege begins (Arturo, 18/10 #58:
        /// each places their own, anywhere free on the clearing but its edge), or null.
        /// </summary>
        public static string? PostBlocker(CampaignState c, int slot, string soldier, Vec2? at)
        {
            if (ActiveOffer(c)?.Siege != true) return "Solo se coloca antes de que empiece el asedio.";
            var s = c.Soldiers.FirstOrDefault((x) => x.Id == soldier);
            if (s == null || s.Owner != slot) return "Solo puedes colocar a tus soldados.";
            if (!c.Squad.Contains(s.Id)) return $"{s.Name} no defiende: no está en condiciones.";
            if (at == null) return null;
            var @base = c.Bastion;
            if (at.X < Base.BaseEdge || at.Y < Base.BaseEdge || at.X >= @base.Width - Base.BaseEdge || at.Y >= @base.Height - Base.BaseEdge) return "Ahí no: es el borde del claro.";
            if (Base.TreeAt(@base, at.X, at.Y)) return "Ahí hay árboles.";
            var piece = Base.PieceAt(@base, at.X, at.Y);
            if (piece != null && Base.Pieces[piece.Piece].Kind == PieceKinds.Trap) return "Ahí hay una trampa.";
            if (piece?.Piece == PieceIds.Wire) return "Ahí hay alambre.";
            if (Base.BaseLayout(@base).Tiles[at.Y * @base.Width + at.X] != TileKinds.Floor) return "Ahí no se puede estar.";
            if ((c.SiegePosts ?? new Dictionary<string, Vec2>()).Any((e) => e.Key != soldier && e.Value.X == at.X && e.Value.Y == at.Y)) return "Ahí ya hay alguien.";
            return null;
        }

        /// <summary>The map a mission is fought on: a siege, on the base as it is now (§11.5); any other, its own.</summary>
        public static MapSource OfferMap(CampaignState c, MissionOffer offer)
        {
            return offer.Siege == true ? MapSource.OfBase(c.Bastion) : offer.Map;
        }

        /// <summary>The siege's own plan for the mission: how many come, and by which sides.</summary>
        public static SiegePlan? SiegePlan(CampaignState c, MissionOffer offer)
        {
            return offer.Siege == true ? new SiegePlan { Count = c.Siege.Count, Sides = new List<int>(c.Siege.Sides) } : null;
        }
    }
}
