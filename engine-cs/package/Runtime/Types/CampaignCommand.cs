#nullable enable
// campaign.ts: the CampaignCommand union (protocol.ts campaignCommandSchema, by `type`), one class per variant; and
// protocol.ts BaseView (baseViewSchema), the base's screens, which campaign.ts NextStep names.
// What campaignCommandSchema checks, for whoever reads them off the wire:
// - an id (`piece` of mend, raise, rearm and dismantle; `soldier`; `weapon`; `id`; assignWeapon's `soldier` when not
//   null): 1–16 characters;
// - `piece` of place and placeLine: a PieceIds value (the keys of base.ts PIECES);
// - a tile of the base (`x` and `y`, each of `tiles` and `cells`, post's `at` when not null): whole numbers 0–255;
//   `rot`: a whole number 0–3; fell: 1–400 tiles; placeLine: 1–60 cells;
// - `region`: a RegionIds value; `item`: an ItemIds value (the keys of content.ts ITEMS); `cell`: the id of a cell of
//   board.ts BOARD; `mod`: a WeaponModIds value; `project`: a CraftIds value; equip's `slot`: an ItemSlots value;
// - answerEvent's `option`: 0 or 1;
// - customize: `name` at most NAME_MAX * 2 characters and `nickname` NICKNAME_MAX * 2 (Appearances.NameMax and
//   NicknameMax); `appearance`: `primary`, `secondary` and `visor`, and `helmet`, `torso`, `arms` and `legs` when
//   there, colours `#rrggbb` (either case); `head` a HeadIds value, `build` a BuildIds value, `sex` missing or a Sexes
//   value, `character` missing or /^[a-z][a-zA-Z0-9]{0,23}$/.
// In TypeScript post's `at` and assignWeapon's `soldier` must be there, null or not; here a null leaves the JSON, as
// every null does (engine-cs README), and reads back the same.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>What a player asks of the host on the base (campaign.ts CampaignCommand, protocol.ts campaignCommandSchema).</summary>
    public abstract class CampaignCommand
    {
        /// <summary>The command's tag (TypeScript `type`).</summary>
        public abstract string Type { get; }
    }

    /// <summary>The base (docs/FASE12.md §11): put a piece somewhere, turned.</summary>
    public sealed class PlaceCommand : CampaignCommand
    {
        public override string Type => "place";
        public string Piece { get; set; } = ""; // PieceIds
        public int X { get; set; }
        public int Y { get; set; }
        public int Rot { get; set; }
    }

    /// <summary>Fell trees.</summary>
    public sealed class FellCommand : CampaignCommand
    {
        public override string Type => "fell";
        public List<Vec2> Tiles { get; set; } = new List<Vec2>();
    }

    /// <summary>Mend a piece.</summary>
    public sealed class MendCommand : CampaignCommand
    {
        public override string Type => "mend";
        /// <summary>The piece on the base (BasePiece.id).</summary>
        public string Piece { get; set; } = "";
    }

    /// <summary>Raise a piece a level.</summary>
    public sealed class RaiseCommand : CampaignCommand
    {
        public override string Type => "raise";
        /// <summary>The piece on the base (BasePiece.id).</summary>
        public string Piece { get; set; } = "";
    }

    /// <summary>Pieces in a row, dragged (Arturo, 18/10 #35): all or none, to a vote when together they are dear.</summary>
    public sealed class PlaceLineCommand : CampaignCommand
    {
        public override string Type => "placeLine";
        public string Piece { get; set; } = ""; // PieceIds
        public List<Vec2> Cells { get; set; } = new List<Vec2>();
        public int Rot { get; set; }
    }

    /// <summary>The Bastion one level up, bought (#40).</summary>
    public sealed class LevelUpCommand : CampaignCommand
    {
        public override string Type => "levelUp";
    }

    /// <summary>A defender put where it will stand when the siege begins (#58); null takes it back to the Bastion.</summary>
    public sealed class PostCommand : CampaignCommand
    {
        public override string Type => "post";
        public string Soldier { get; set; } = "";
        public Vec2? At { get; set; }
    }

    /// <summary>Set again (or reload) a trap or a turret.</summary>
    public sealed class RearmCommand : CampaignCommand
    {
        public override string Type => "rearm";
        /// <summary>The piece on the base (BasePiece.id).</summary>
        public string Piece { get; set; } = "";
    }

    /// <summary>Take down a piece.</summary>
    public sealed class DismantleCommand : CampaignCommand
    {
        public override string Type => "dismantle";
        /// <summary>The piece on the base (BasePiece.id).</summary>
        public string Piece { get; set; } = "";
    }

    public sealed class ChooseMissionCommand : CampaignCommand
    {
        public override string Type => "chooseMission";
        /// <summary>The operation (MissionOffer.id).</summary>
        public string Id { get; set; } = "";
    }

    public sealed class ContactCommand : CampaignCommand
    {
        public override string Type => "contact";
        public string Region { get; set; } = ""; // RegionIds
    }

    public sealed class AnswerEventCommand : CampaignCommand
    {
        public override string Type => "answerEvent";
        public int Option { get; set; } // 0 | 1
    }

    public sealed class InvestigateCommand : CampaignCommand
    {
        public override string Type => "investigate";
        /// <summary>The rumour (Rumor.id).</summary>
        public string Id { get; set; } = "";
    }

    public sealed class CancelMissionCommand : CampaignCommand
    {
        public override string Type => "cancelMission";
    }

    public sealed class SquadCommand : CampaignCommand
    {
        public override string Type => "squad";
        public string Soldier { get; set; } = "";
        public bool On { get; set; }
    }

    /// <summary>
    /// Recruits come without a class: they specialise with their first talent points. `squad`: straight into the squad
    /// of the chosen mission (a free place in the hangar).
    /// </summary>
    public sealed class RecruitCommand : CampaignCommand
    {
        public override string Type => "recruit";
        public bool? Squad { get; set; }
    }

    /// <summary>Fase 10: a skill of the hex board bought with a point (skills.ts unlockBlocker).</summary>
    public sealed class UnlockCommand : CampaignCommand
    {
        public override string Type => "unlock";
        public string Soldier { get; set; } = "";
        public string Cell { get; set; } = ""; // CellId
    }

    /// <summary>Fase 9 (docs/FASE9.md §5): mend a weapon with parts, as far as they go.</summary>
    public sealed class RepairCommand : CampaignCommand
    {
        public override string Type => "repair";
        /// <summary>The weapon of the Armería (FoundWeapon.id).</summary>
        public string Weapon { get; set; } = "";
    }

    /// <summary>Fase 9 (docs/FASE9.md §5): mount or take off an improvement.</summary>
    public sealed class ModCommand : CampaignCommand
    {
        public override string Type => "mod";
        /// <summary>The weapon of the Armería (FoundWeapon.id).</summary>
        public string Weapon { get; set; } = "";
        public string Mod { get; set; } = ""; // WeaponModIds
        public bool On { get; set; }
    }

    public sealed class BuyCommand : CampaignCommand
    {
        public override string Type => "buy";
        public string Item { get; set; } = ""; // ItemIds
    }

    public sealed class EquipCommand : CampaignCommand
    {
        public override string Type => "equip";
        public string Soldier { get; set; } = "";
        public string Slot { get; set; } = ""; // ItemSlots
        public string? Item { get; set; } // ItemIds
    }

    public sealed class CustomizeCommand : CampaignCommand
    {
        public override string Type => "customize";
        public string Soldier { get; set; } = "";
        public string Name { get; set; } = "";
        public string Nickname { get; set; } = "";
        public Appearance Appearance { get; set; } = new Appearance();
    }

    public sealed class AdvanceCommand : CampaignCommand
    {
        public override string Type => "advance";
        public bool Ready { get; set; }
    }

    public sealed class LaunchCommand : CampaignCommand
    {
        public override string Type => "launch";
        public bool Ready { get; set; }
    }

    /// <summary>Treat a bitten soldier: two keys, like launching (docs/FASE3.md).</summary>
    public sealed class TreatCommand : CampaignCommand
    {
        public override string Type => "treat";
        public string Soldier { get; set; } = "";
        public bool Ready { get; set; }
    }

    /// <summary>1 ✚ for 3 days fewer of a wound.</summary>
    public sealed class MedicateCommand : CampaignCommand
    {
        public override string Type => "medicate";
        public string Soldier { get; set; } = "";
    }

    /// <summary>The Taller makes a tier of weapons or armour (docs/FASE4.md): a shared decision, like building.</summary>
    public sealed class CraftCommand : CampaignCommand
    {
        public override string Type => "craft";
        public string Project { get; set; } = ""; // CraftIds
    }

    /// <summary>A found weapon from the Armería to one of your soldiers (null: back on the rack).</summary>
    public sealed class AssignWeaponCommand : CampaignCommand
    {
        public override string Type => "assignWeapon";
        /// <summary>The weapon of the Armería (FoundWeapon.id).</summary>
        public string Weapon { get; set; } = "";
        public string? Soldier { get; set; }
    }

    /// <summary>The answer to the partner's proposal.</summary>
    public sealed class VoteCommand : CampaignCommand
    {
        public override string Type => "vote";
        public bool Accept { get; set; }
    }

    public sealed class WithdrawCommand : CampaignCommand
    {
        public override string Type => "withdraw";
    }

    /// <summary>protocol.ts BaseView (baseViewSchema): screens of the strategy layer, XCOM 2 style (shown to the partner).</summary>
    public static class BaseViews
    {
        public const string Hub = "hub";
        public const string Geoscape = "geoscape";
        public const string Research = "research";
        public const string Engineering = "engineering";
        public const string Barracks = "barracks";
        public const string Facilities = "facilities";
        public const string Squad = "squad";
        public const string Debrief = "debrief";

        public static readonly IReadOnlyList<string> All = new[] { Hub, Geoscape, Research, Engineering, Barracks, Facilities, Squad, Debrief };
    }
}
