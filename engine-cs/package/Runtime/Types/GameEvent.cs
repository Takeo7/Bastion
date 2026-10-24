#nullable enable
// events.ts: the GameEvent union, one class per variant (its `t` as the class's T). applyEvent is not here yet.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>
    /// Everything that changes the game state is an event. The host produces them
    /// (rolling dice), and every client replays them with the same reducer, so all
    /// copies of the state stay identical without sending full snapshots.
    /// </summary>
    public abstract class GameEvent
    {
        /// <summary>The event's tag (TypeScript `t`).</summary>
        public abstract string T { get; }
    }

    public sealed class TurnBeganEvent : GameEvent
    {
        public override string T => "turnBegan";
        public string Team { get; set; } = ""; // Teams
        public int Turn { get; set; }
        public int? Command { get; set; } // Slot
    }

    /// <summary>The command (el mando) changes hands.</summary>
    public sealed class CommandPassedEvent : GameEvent
    {
        public override string T => "commandPassed";
        public int To { get; set; } // Slot
    }

    /// <summary>The soldier in play changes (playtest 2026-10-15, #9); null, none.</summary>
    public sealed class UnitActiveEvent : GameEvent
    {
        public override string T => "unitActive";
        public string? Unit { get; set; }
    }

    public sealed class MovedEvent : GameEvent
    {
        public override string T => "moved";
        public string Unit { get; set; } = "";
        public List<Pos> Path { get; set; } = new List<Pos>();
        public int Ap { get; set; }
    }

    /// <summary>Real time (docs/TIEMPO-REAL.md): the clock, on every tick of the host's.</summary>
    public sealed class RtClockEvent : GameEvent
    {
        public override string T => "rtClock";
        public int Clock { get; set; }
    }

    /// <summary>Real time: stopped or going again, by a player (null: the host).</summary>
    public sealed class RtPausedEvent : GameEvent
    {
        public override string T => "rtPaused";
        public bool Paused { get; set; }
        public int? Slot { get; set; } // Slot
    }

    /// <summary>Real time: a unit's standing order changes.</summary>
    public sealed class RtOrderEvent : GameEvent
    {
        public override string T => "rtOrder";
        public string Unit { get; set; } = "";
        public RtOrder Order { get; set; } = new IdleRtOrder();
    }

    /// <summary>Real time: what a unit starts doing, until when (clock ms), at whom and with what.</summary>
    public sealed class RtDoingEvent : GameEvent
    {
        public override string T => "rtDoing";
        public string Unit { get; set; } = "";
        public string Doing { get; set; } = ""; // RtDoings
        public int Until { get; set; }
        public string? Target { get; set; }
        public string? Ability { get; set; } // AbilityIds
    }

    /// <summary>Real time: the clock runs at this speed (1, or slow while an ability is being aimed).</summary>
    public sealed class RtSpeedEvent : GameEvent
    {
        public override string T => "rtSpeed";
        public double Speed { get; set; }
    }

    /// <summary>
    /// Real time: a round has gone by (realtime.ts RT_ROUND): for `unit`, its own (what lasts a turn on it wears off:
    /// cover, a stun, cooldowns), each one's from when it was first about; without, the map's (smoke, bengalas).
    /// </summary>
    public sealed class RtRoundEvent : GameEvent
    {
        public override string T => "rtRound";
        public string? Unit { get; set; }
    }

    /// <summary>An ability was activated: pays its actions (negative = gains), cooldown and charge.</summary>
    public sealed class AbilityUsedEvent : GameEvent
    {
        public override string T => "abilityUsed";
        public string Unit { get; set; } = "";
        public string Ability { get; set; } = ""; // AbilityIds
        public int Ap { get; set; }
        public int Cooldown { get; set; }
        public string? Charge { get; set; } // ChargeIds
    }

    public sealed class ShotEvent : GameEvent
    {
        public override string T => "shot";
        public string Unit { get; set; } = "";
        public string Target { get; set; } = "";
        public string Ability { get; set; } = ""; // AttackAbilities
        public Pos From { get; set; } = new Pos();
        public string Result { get; set; } = ""; // ShotResults
        public double Chance { get; set; }
        /// <summary>The d100, 1–100: it hits when `roll ≤ chance`.</summary>
        public int Roll { get; set; }
        public bool Reaction { get; set; }
    }

    /// <summary>`amount` is what got through armour; `mitigated` what the armour stopped.</summary>
    public sealed class DamagedEvent : GameEvent
    {
        public override string T => "damaged";
        public string Unit { get; set; } = "";
        public int Amount { get; set; }
        public int Mitigated { get; set; }
        public int Shred { get; set; }
    }

    /// <summary>`by`: the unit whose attack or explosive dealt the killing blow.</summary>
    public sealed class DiedEvent : GameEvent
    {
        public override string T => "died";
        public string Unit { get; set; } = "";
        public string? By { get; set; }
    }

    public sealed class StunnedEvent : GameEvent
    {
        public override string T => "stunned";
        public string Unit { get; set; } = "";
    }

    /// <summary>`melee`: with the blade (docs/FASE10.md §4), it strikes whoever comes alongside.</summary>
    public sealed class OverwatchEvent : GameEvent
    {
        public override string T => "overwatch";
        public string Unit { get; set; } = "";
        public bool KillZone { get; set; }
        public bool? Melee { get; set; }
    }

    public sealed class HunkerEvent : GameEvent
    {
        public override string T => "hunker";
        public string Unit { get; set; } = "";
    }

    /// <summary>A stone thrown at `target` (docs/FASE10.md §4); `hit`, who was standing there.</summary>
    public sealed class StoneThrownEvent : GameEvent
    {
        public override string T => "stoneThrown";
        public string Unit { get; set; } = "";
        public Pos Target { get; set; } = new Pos();
        public string? Hit { get; set; }
    }

    /// <summary>A bar filled out there (docs/FASE10.md §2): its line's stat goes up `growth` percent at once. `filled`, the bars of that line now.</summary>
    public sealed class BarFilledEvent : GameEvent
    {
        public override string T => "barFilled";
        public string Unit { get; set; } = "";
        public string Skill { get; set; } = ""; // SkillIds
        public int Filled { get; set; }
        public int Growth { get; set; }
    }

    public sealed class ReloadEvent : GameEvent
    {
        public override string T => "reload";
        public string Unit { get; set; } = "";
    }

    public sealed class ExplosiveEvent : GameEvent
    {
        public override string T => "explosive";
        public string Unit { get; set; } = "";
        public string Ability { get; set; } = ""; // BlastAbilities
        public Pos Target { get; set; } = new Pos();
    }

    public sealed class HealedEvent : GameEvent
    {
        public override string T => "healed";
        public string Unit { get; set; } = "";
        public string Target { get; set; } = "";
        public int Amount { get; set; }
    }

    public sealed class AidedEvent : GameEvent
    {
        public override string T => "aided";
        public string Unit { get; set; } = "";
        public string Target { get; set; } = "";
        public int Amount { get; set; }
    }

    /// <summary>Revival / Restoration: stun, disorientation and panic are gone.</summary>
    public sealed class CleansedEvent : GameEvent
    {
        public override string T => "cleansed";
        public string Unit { get; set; } = "";
        public string Target { get; set; } = "";
    }

    public sealed class RefundEvent : GameEvent
    {
        public override string T => "refund";
        public string Unit { get; set; } = "";
        public string Reason { get; set; } = ""; // RefundReasons
    }

    /// <summary>RefundEvent.reason: `'implacable' | 'deathFromAbove'`.</summary>
    public static class RefundReasons
    {
        public const string Implacable = "implacable";
        public const string DeathFromAbove = "deathFromAbove";

        public static readonly IReadOnlyList<string> All = new[] { Implacable, DeathFromAbove };
    }

    public sealed class SuppressedEvent : GameEvent
    {
        public override string T => "suppressed";
        public string Unit { get; set; } = "";
        public string Target { get; set; } = "";
    }

    /// <summary>The suppressor lets go (it acted, moved, died or its turn came).</summary>
    public sealed class SuppressionEndedEvent : GameEvent
    {
        public override string T => "suppressionEnded";
        public string Unit { get; set; } = "";
    }

    public sealed class MarkedEvent : GameEvent
    {
        public override string T => "marked";
        public string Unit { get; set; } = "";
        public int Amount { get; set; }
    }

    public sealed class RupturedEvent : GameEvent
    {
        public override string T => "ruptured";
        public string Unit { get; set; } = "";
        public int Amount { get; set; }
    }

    public sealed class PsiEvent : GameEvent
    {
        public override string T => "psi";
        public string Unit { get; set; } = "";
        public string Target { get; set; } = "";
        public bool Success { get; set; }
        public string? Effect { get; set; } // PsiEffects
        public double Chance { get; set; }
    }

    /// <summary>Mind control ends: the soldier fights for XCOM again. Nothing takes control any more: kept for missions saved before.</summary>
    public sealed class ReleasedEvent : GameEvent
    {
        public override string T => "released";
        public string Unit { get; set; } = "";
    }

    public sealed class ControlTickEvent : GameEvent
    {
        public override string T => "controlTick";
        public string Unit { get; set; } = "";
    }

    /// <summary>
    /// A corpse rises as a zombie: raised by a Chillón (`unit`), or by itself, an
    /// infected soldier fallen in the fight (docs/FASE3.md): then `pod` is its own new group.
    /// </summary>
    public sealed class ReanimatedEvent : GameEvent
    {
        public override string T => "reanimated";
        public string? Unit { get; set; }
        public string Corpse { get; set; } = "";
        public Unit Zombie { get; set; } = new Unit();
        public Pod? Pod { get; set; }
    }

    /// <summary>Bitten and infected (`by`: the zombie, or the Hinchado that burst).</summary>
    public sealed class InfectedEvent : GameEvent
    {
        public override string T => "infected";
        public string Unit { get; set; } = "";
        public string By { get; set; } = "";
    }

    public sealed class SmokeDeployedEvent : GameEvent
    {
        public override string T => "smokeDeployed";
        public string Unit { get; set; } = "";
        public Pos Pos { get; set; } = new Pos();
        public double Radius { get; set; }
        public int Turns { get; set; }
    }

    /// <summary>Flashbang: disoriented until the end of its own next turn.</summary>
    public sealed class DisorientedEvent : GameEvent
    {
        public override string T => "disoriented";
        public string Unit { get; set; } = "";
    }

    public sealed class EvacuatedEvent : GameEvent
    {
        public override string T => "evacuated";
        public string Unit { get; set; } = "";
    }

    public sealed class ItemPickedEvent : GameEvent
    {
        public override string T => "itemPicked";
        public string Unit { get; set; } = "";
    }

    public sealed class ItemDroppedEvent : GameEvent
    {
        public override string T => "itemDropped";
        public Pos Pos { get; set; } = new Pos();
    }

    /// <summary>A player asks to evacuate, from `unit` in the bus; the partner answers (docs/PRUEBA-2026-10-14.md P1).</summary>
    public sealed class EvacCalledEvent : GameEvent
    {
        public override string T => "evacCalled";
        public int Slot { get; set; } // Slot
        public string Unit { get; set; } = "";
    }

    /// <summary>The partner said no: everyone stays.</summary>
    public sealed class EvacRefusedEvent : GameEvent
    {
        public override string T => "evacRefused";
        public int Slot { get; set; } // Slot
    }

    /// <summary>A player asks to abandon the mission; the partner answers (playtest 16/10), as with the bus.</summary>
    public sealed class AbandonCalledEvent : GameEvent
    {
        public override string T => "abandonCalled";
        public int Slot { get; set; } // Slot
    }

    public sealed class AbandonRefusedEvent : GameEvent
    {
        public override string T => "abandonRefused";
        public int Slot { get; set; } // Slot
    }

    /// <summary>The bus leaves: `evacuated` for whoever is in it and `leftBehind` for the rest follow.</summary>
    public sealed class EvacDepartedEvent : GameEvent
    {
        public override string T => "evacDeparted";
    }

    /// <summary>Not in the bus when it left: lost (docs/PRUEBA-2026-10-14.md P1).</summary>
    public sealed class LeftBehindEvent : GameEvent
    {
        public override string T => "leftBehind";
        public string Unit { get; set; } = "";
    }

    /// <summary>`decor`: what it is to look like from now on (a breached wall), turned `facing` (DIRS4; north if none); without it, the decor stays.</summary>
    public sealed class TileChangedEvent : GameEvent
    {
        public override string T => "tileChanged";
        public Pos Pos { get; set; } = new Pos();
        public string Kind { get; set; } = ""; // TileKinds
        public string? Decor { get; set; } // DecorKinds
        public int? Facing { get; set; }
    }

    /// <summary>A trap of the base goes off under one of the dead (docs/FASE12.md §11.4): one use less. Its harm follows (`damaged`, `stunned`).</summary>
    public sealed class TrapSprungEvent : GameEvent
    {
        public override string T => "trapSprung";
        public string Trap { get; set; } = "";
        public string Unit { get; set; } = "";
    }

    /// <summary>One of the dead beats on a door or a barricade (docs/FASE12.md §4): `left`, what it still holds; at 0 a `tileChanged` follows, it gave way.</summary>
    public sealed class SmashedEvent : GameEvent
    {
        public override string T => "smashed";
        public string Unit { get; set; } = "";
        public Pos Tile { get; set; } = new Pos();
        public int Amount { get; set; }
        public int Left { get; set; }
    }

    /// <summary>A storey's slab gives way under a grenade (docs/FASE7.md §5): the place is open air.</summary>
    public sealed class FloorBrokenEvent : GameEvent
    {
        public override string T => "floorBroken";
        public Pos Pos { get; set; } = new Pos();
    }

    /// <summary>Down through a broken floor onto `to` (docs/FASE7.md §5); the damage comes after, as `damaged`.</summary>
    public sealed class FellEvent : GameEvent
    {
        public override string T => "fell";
        public string Unit { get; set; } = "";
        public Pos From { get; set; } = new Pos();
        public Pos To { get; set; } = new Pos();
    }

    /// <summary>`unit`: the soldier they saw (or who attacked them); `by`: the alien that saw it.</summary>
    public sealed class PodActivatedEvent : GameEvent
    {
        public override string T => "podActivated";
        public string Pod { get; set; } = "";
        public string Cause { get; set; } = ""; // PodCauses
        public string? Unit { get; set; }
        public string? By { get; set; }
    }

    /// <summary>
    /// Who gave the squad away and how: `unit` is the soldier seen (or who fired,
    /// sang or hacked), `by` the alien that saw it, `range` its detection in tiles.
    /// </summary>
    public sealed class ConcealmentBrokenEvent : GameEvent
    {
        public override string T => "concealmentBroken";
        public string Reason { get; set; } = ""; // ConcealReasons
        public string? Unit { get; set; }
        public string? By { get; set; }
        public double? Range { get; set; } // Fractional at night (los.ts detectionTo).
    }

    public sealed class ReadyEvent : GameEvent
    {
        public override string T => "ready";
        public int Slot { get; set; } // Slot
        public bool Ready { get; set; }
    }

    public sealed class MissionEndedEvent : GameEvent
    {
        public override string T => "missionEnded";
        public string Outcome { get; set; } = ""; // Outcomes
        public string Reason { get; set; } = ""; // EndReasons
    }

    /// <summary>Hack attempt on the objective terminal.</summary>
    public sealed class HackedEvent : GameEvent
    {
        public override string T => "hacked";
        public string Unit { get; set; } = "";
        public bool Success { get; set; }
        public double Chance { get; set; }
    }

    /// <summary>A soldier reached the VIP, who now follows that player's orders.</summary>
    public sealed class VipRescuedEvent : GameEvent
    {
        public override string T => "vipRescued";
        public string Unit { get; set; } = "";
        public string By { get; set; } = "";
        public int? Owner { get; set; } // Slot
    }

    /// <summary>Flare: reinforcements will land here at the end of the next alien turn.</summary>
    public sealed class ReinforcementsIncomingEvent : GameEvent
    {
        public override string T => "reinforcementsIncoming";
        public Reinforcement R { get; set; } = new Reinforcement();
    }

    /// <summary>New units on the battlefield (reinforcements), already formed.</summary>
    public sealed class SpawnedEvent : GameEvent
    {
        public override string T => "spawned";
        public List<Unit> Units { get; set; } = new List<Unit>();
        public Pod Pod { get; set; } = new Pod();
        public string? Reinforcement { get; set; }
    }

    /// <summary>An inactive pod heads for its next patrol waypoint.</summary>
    public sealed class PatrolEvent : GameEvent
    {
        public override string T => "patrol";
        public string Pod { get; set; } = "";
        public int Index { get; set; }
    }

    // Noise and searching (docs/FASE2.md).

    /// <summary>
    /// Something `by` did was heard `radius` tiles around `tile`. `heard`: the
    /// enemies that now go to look (Unit.heard). The squad's noise also winds up
    /// the Saqueo's horde.
    /// </summary>
    public sealed class NoiseEvent : GameEvent
    {
        public override string T => "noise";
        public Pos Tile { get; set; } = new Pos();
        public double Radius { get; set; } // Fractional: the squad's is louder (rules.ts makeNoise).
        public string? By { get; set; }
        public List<string> Heard { get; set; } = new List<string>();
    }

    /// <summary>It reached what it heard and found nobody: it stops looking for it.</summary>
    public sealed class ForgotEvent : GameEvent
    {
        public override string T => "forgot";
        public string Unit { get; set; } = "";
    }

    /// <summary>
    /// One of the dead goes after `target`, a soldier it saw (Unit.hunting), `missed` of its turns since it last saw
    /// any of the living; null: it has forgotten them.
    /// </summary>
    public sealed class HuntingEvent : GameEvent
    {
        public override string T => "hunting";
        public string Unit { get; set; } = "";
        public string? Target { get; set; }
        public int Missed { get; set; }
    }

    /// <summary>An enemy turns where it stands to face `facing` (radians, Unit.facing): a pod woken by a soldier, the dead looking round.</summary>
    public sealed class TurnedEvent : GameEvent
    {
        public override string T => "turned";
        public string Unit { get; set; } = "";
        public double Facing { get; set; }
    }

    /// <summary>A soldier searched the furniture on `tile`; `loot` went into the backpack (null: nothing).</summary>
    public sealed class SearchedEvent : GameEvent
    {
        public override string T => "searched";
        public string Unit { get; set; } = "";
        public Pos Tile { get; set; } = new Pos();
        public Loot? Loot { get; set; }
    }

    /// <summary>A room searched, by its id (loot.ts roomAt; docs/FASE8.md §B); what it turned up, as `searched`.</summary>
    public sealed class RoomSearchedEvent : GameEvent
    {
        public override string T => "roomSearched";
        public string Unit { get; set; } = "";
        public int Room { get; set; }
        public Loot? Loot { get; set; }
    }

    /// <summary>The backpack (docs/FASE8.md §C): a thing left on the soldier's tile, free; it becomes `dropped` l&lt;n&gt;.</summary>
    public sealed class LootDroppedEvent : GameEvent
    {
        public override string T => "lootDropped";
        public string Unit { get; set; } = "";
        public int Index { get; set; }
    }

    /// <summary>A thing picked up from the soldier's tile (GameState.dropped), free.</summary>
    public sealed class LootTakenEvent : GameEvent
    {
        public override string T => "lootTaken";
        public string Unit { get; set; } = "";
        public string Id { get; set; } = "";
    }

    /// <summary>A thing handed to a partner next to the soldier, for an action.</summary>
    public sealed class LootGivenEvent : GameEvent
    {
        public override string T => "lootGiven";
        public string Unit { get; set; } = "";
        public string To { get; set; } = "";
        public int Index { get; set; }
    }

    /// <summary>A medicine used from the backpack, for an action; the cure itself comes as `healed` and `cured`.</summary>
    public sealed class MedicineUsedEvent : GameEvent
    {
        public override string T => "medicineUsed";
        public string Unit { get; set; } = "";
        public string Target { get; set; } = "";
        public int Index { get; set; }
    }

    /// <summary>A weapon from the backpack taken in hand (or at the belt), for an action: the one it replaces goes into the backpack, or to the floor.</summary>
    public sealed class WeaponEquippedEvent : GameEvent
    {
        public override string T => "weaponEquipped";
        public string Unit { get; set; } = "";
        public int Index { get; set; }
    }

    /// <summary>A vest from the backpack put on (playtest 16/10 #18): its health at once, the new points full.</summary>
    public sealed class VestWornEvent : GameEvent
    {
        public override string T => "vestWorn";
        public string Unit { get; set; } = "";
        public int Index { get; set; }
    }

    /// <summary>A belt thing from the backpack put on the belt (playtest 18/10 #5), for an action: its uses, its ability and what it gives, at once.</summary>
    public sealed class BeltEquippedEvent : GameEvent
    {
        public override string T => "beltEquipped";
        public string Unit { get; set; } = "";
        public int Index { get; set; }
    }

    /// <summary>A bite's contagion stopped (docs/FASE8.md §C).</summary>
    public sealed class CuredEvent : GameEvent
    {
        public override string T => "cured";
        public string Unit { get; set; } = "";
        public string By { get; set; } = "";
    }

    /// <summary>Apaños (docs/FASE9.md): `amount` of condition back to the weapon in `target`'s hand.</summary>
    public sealed class PatchedEvent : GameEvent
    {
        public override string T => "patched";
        public string Unit { get; set; } = "";
        public string Target { get; set; } = "";
        public double Amount { get; set; } // Fractional when the condition is (Arms.Condition).
    }

    // Night (docs/ALTURA-Y-NOCHE.md).

    /// <summary>A light goes on at night (a Bengala, a fire): whoever stands in it is seen from as far as by day.</summary>
    public sealed class LightOnEvent : GameEvent
    {
        public override string T => "lightOn";
        public Light Light { get; set; } = new Light();
    }

    /// <summary>A Bengala or a fire burns out.</summary>
    public sealed class LightOffEvent : GameEvent
    {
        public override string T => "lightOff";
        public string Id { get; set; } = "";
    }
}
