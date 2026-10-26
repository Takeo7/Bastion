using System;
using System.Collections.Generic;
using UnityEngine;

// What the HUD is handed to draw (docs/UNITY-UI.md §1): built by the presenter from the engine's state, as game.ts built
// hud.ts's views. Plain [Serializable] classes with public fields, so a piece compares them by their JSON (Ui.Key) and
// draws again only when they say something new. Field by field they are the TypeScript views of hud.ts.

namespace Bastion.UI.Hud
{
    /// <summary>A tag on a card or a flag («VIGILANCIA», «MORDISCO»): its words, its kind (the colour) and its tooltip.</summary>
    [Serializable]
    public sealed class TagView
    {
        public string Text;
        /// <summary>ow, hk, stun, psi, bite, data, evac, aid, in-play: as the CSS classes were.</summary>
        public string Kind;
        public string Tip;
    }

    // ----------------------------------------------------------------- turn

    /// <summary>The strip at the top (hud.ts setTurn): whose turn, its number, concealment, night, the soldier in play.</summary>
    [Serializable]
    public sealed class TurnView
    {
        public int Turn;
        /// <summary>mine, partner, enemy, or over (the mission is over).</summary>
        public string Owner;
        /// <summary>The partner's name, when it is theirs.</summary>
        public string OwnerName;
        /// <summary>Whose colour, «#rrggbb»; the enemy's is the stamp's red.</summary>
        public string Color;
        public bool Concealed;
        /// <summary>«De noche», and what the dark changes (its tooltip); empty by day.</summary>
        public string NightLabel;
        public string NightRules;
        /// <summary>The soldier in play while turns go one soldier at a time; empty if not.</summary>
        public string InPlay;
    }

    /// <summary>A player on the strip (hud.ts setPlayers): name, colour, connected, ready, holding the command.</summary>
    [Serializable]
    public sealed class PlayerView
    {
        public int Slot;
        public string Name;
        public string Color;
        public bool Me;
        public bool Connected;
        public bool Ready;
        public bool Command;
        /// <summary>«Listo», «Con acciones», «Sin acciones», «Desconectado», «Ausente», or empty.</summary>
        public string Status;
    }

    [Serializable]
    public sealed class PlayersView
    {
        public List<PlayerView> Players = new List<PlayerView>();
    }

    /// <summary>One player's turn clock (the server's turnClock): how much is left, 0–1, and whether it runs now.</summary>
    [Serializable]
    public sealed class ClockView
    {
        public bool Shown;
        public float Left;
        public bool Running;
        public string Color;
    }

    /// <summary>«Terminar turno» (hud.ts setEndTurn): act, confirm, ready, waiting, enemy or disabled, and its line.</summary>
    [Serializable]
    public sealed class EndTurnView
    {
        public string Mode = "act";
        /// <summary>Who is waited for, or how many soldiers still have actions.</summary>
        public string Detail;
    }

    /// <summary>The command (hud.ts setCommand): «Ceder el mando» for its holder, «Ver a Ana» for the other one.</summary>
    [Serializable]
    public sealed class CommandView
    {
        public bool Shared;
        public bool Mine;
        public string Holder;
        public string Color;
        public bool CanPass;
    }

    // ---------------------------------------------------------------- squad

    /// <summary>A soldier's card on the squad list (hud.ts setSquad).</summary>
    [Serializable]
    public sealed class SquadCardView
    {
        public string Id;
        /// <summary>The nickname «in quotes» if it has one, else the name.</summary>
        public string Name;
        /// <summary>The owner's colour.</summary>
        public string Accent;
        /// <summary>This player gives its orders (its own, or the partner's while away): it can be picked.</summary>
        public bool Mine;
        public bool Selected;
        public bool Dead;
        public bool Evacuated;
        public bool InPlay;
        public int Hp;
        public int MaxHp;
        public int Ap;
        public int ApMax;
        /// <summary>A siege's turret: its rounds in place of the actions.</summary>
        public bool Rounds;
        /// <summary>The six experience lines (skills.ts SKILL_IDS order), for its hexagon; empty for one with none.</summary>
        public float[] Skills = Array.Empty<float>();
        public string Title;
        public List<TagView> Tags = new List<TagView>();
    }

    [Serializable]
    public sealed class SquadView
    {
        public List<SquadCardView> Cards = new List<SquadCardView>();
    }

    /// <summary>A figure on the soldier's panel: its icon (Kind), tooltip, pips and/or a number.</summary>
    [Serializable]
    public sealed class StatView
    {
        /// <summary>hp, ap, ammo, sidearm, aim, melee, grenade, armor, worn.</summary>
        public string Kind;
        public string Tip;
        /// <summary>Pips, when Total is above 0.</summary>
        public int Filled;
        public int Total;
        /// <summary>A number instead of (or after) the pips: «68», «×2», «+3».</summary>
        public string Value;
        /// <summary>The spare magazines are none: their number in red.</summary>
        public bool ValueBad;
    }

    /// <summary>The picked soldier's panel (hud.ts setSelected), without the backpack (after the first milestone).</summary>
    [Serializable]
    public sealed class UnitPanelView
    {
        public string Id;
        /// <summary>A soldier opens its sheet with a click (H); a VIP or an enemy doesn't.</summary>
        public bool Soldier;
        /// <summary>Veterancy chevrons.</summary>
        public int Rank;
        /// <summary>«Novata», «Veterano»… or what it is, if not a soldier.</summary>
        public string RankName;
        public float[] Skills = Array.Empty<float>();
        public string Name;
        /// <summary>A partner's soldier: whose (their name: the panel says «DE ANA», HudText.Squad.Whose) and their colour; empty, one's own.</summary>
        public string Whose;
        public string WhoseColor;
        /// <summary>Bitten: its tag (Lore.BiteTag, «MORDISCO») and what a bite does in this mission; empty if not.</summary>
        public string Bite;
        public string BiteTip;
        public List<StatView> Stats = new List<StatView>();
        public string Weapon;
        public string WeaponTip;
        /// <summary>A weapon of the Armería's state and improvements (arms.ts wearMarks): «GASTADA» (worn), «ROTA» (broken), «+2» (mods).</summary>
        public List<TagView> WeaponMarks = new List<TagView>();
        /// <summary>Its backpack, where there is anything to search or it carries something (game.ts packView); null, none.</summary>
        public PackView Pack;
    }

    // ------------------------------------------------------------- backpack

    /// <summary>An order on a thing carried or on the floor (hud.ts ItemOrder): the engine's `item` command for the soldier shown.</summary>
    [Serializable]
    public sealed class PackOrder
    {
        /// <summary>drop, take, give, use, equip (ItemActions).</summary>
        public string Action;
        /// <summary>The slot ordered on; -1, none (take).</summary>
        public int Index = -1;
        /// <summary>The thing on the floor taken.</summary>
        public string Id;
        /// <summary>The partner it is handed to, or treated.</summary>
        public string To;
    }

    /// <summary>A line of a slot's menu (hud.ts packMenuEl): «Curar a Kofi», what it costs, or why not instead.</summary>
    [Serializable]
    public sealed class PackActView
    {
        public string Label;
        /// <summary>«1 acción», «gratis».</summary>
        public string Cost;
        /// <summary>Why it can't be given now (in place of the cost); empty, it can.</summary>
        public string Off;
        /// <summary>What the slot is for (Usar, Equipar, Ponerse, Al cinturón): lit.</summary>
        public bool Main;
        public PackOrder Order;
    }

    /// <summary>A slot of the backpack (hud.ts PackSlot): one thing carried.</summary>
    [Serializable]
    public sealed class PackSlotView
    {
        /// <summary>supplies, medicines, food, parts, weapon, item: its colour.</summary>
        public string Kind;
        /// <summary>Its number («12§», «3»); empty, only its icon.</summary>
        public string Text;
        /// <summary>Its icon (HudIcons: «store:food», «stat:grenade»…); empty, none.</summary>
        public string Icon;
        public string Tip;
        /// <summary>What it is (lore.ts findLabel: «Medicinas»), its menu's title.</summary>
        public string Label;
        /// <summary>The same as a phrase (lootName: «una medicina»), for «Dejar …».</summary>
        public string Name;
        /// <summary>Its menu: what can be done with it.</summary>
        public List<PackActView> Acts = new List<PackActView>();
    }

    /// <summary>A thing on the soldier's tile (GameState.dropped), to pick up.</summary>
    [Serializable]
    public sealed class PackFloorView
    {
        public string Id;
        public string Kind;
        /// <summary>findLabel: «Medicinas», «+2 piezas».</summary>
        public string Label;
        /// <summary>lootName: «2 medicinas», for «Coger …: ¿qué dejas?».</summary>
        public string Name;
    }

    /// <summary>A fallen soldier's rucksack on the tile (playtest 18/10 #49): whose, and one order that takes what fits.</summary>
    [Serializable]
    public sealed class PackFallenView
    {
        /// <summary>lore.ts fallenPackMark: «MOCHILA DE CHIARA».</summary>
        public string Mark;
        /// <summary>fallenPackAction: its button.</summary>
        public string Action;
        public string Tip;
        /// <summary>The backpack is full.</summary>
        public bool Off;
        public List<string> Ids = new List<string>();
    }

    /// <summary>One of the squad's totals: «12§», «×2» with the medicines' icon.</summary>
    [Serializable]
    public sealed class PackTotalView
    {
        public string Kind;
        public string Text;
        public string Icon;
        /// <summary>Its icon after the number (rations and parts), not before.</summary>
        public bool IconAfter;
    }

    /// <summary>The soldier's backpack and the squad's haul (hud.ts PackView): it only counts if it gets home.</summary>
    [Serializable]
    public sealed class PackView
    {
        public List<PackSlotView> Slots = new List<PackSlotView>();
        public int Size;
        /// <summary>One of the player's own, with orders to give (docs/FASE8.md §C): the slots and the floor take a click.</summary>
        public bool Live;
        /// <summary>The partner holds the command: why no order goes yet («Tiene el mando Luis»); empty, they go.</summary>
        public string Locked;
        public string Tip;
        public List<PackFloorView> Floor = new List<PackFloorView>();
        public List<PackFallenView> Fallen = new List<PackFallenView>();
        public List<PackTotalView> Totals = new List<PackTotalView>();
        /// <summary>What the squad carries, in words (lootSummary), on its totals.</summary>
        public string SquadTip;
    }

    // -------------------------------------------------------------- actions

    /// <summary>A name and its value on an ability's card («Alcance», «8 casillas»).</summary>
    [Serializable]
    public sealed class StatPair
    {
        public string Key;
        public string Value;
    }

    /// <summary>
    /// A key of the action bar (hud.ts AbilityView). A menu (Lanzar, Vigilancia, Investigar) has its choices as entries
    /// of the same list with `Menu` set to its id: no view inside a view, which Unity's serializer won't nest.
    /// </summary>
    [Serializable]
    public sealed class AbilityView
    {
        public string Id;
        /// <summary>The menu it is a choice of; empty for a key of the bar.</summary>
        public string Menu;
        public string Hotkey;
        public bool Enabled;
        /// <summary>Why it can't be used now (the card's last line).</summary>
        public string Reason;
        /// <summary>What to do first, on the key itself (#24: «Sin balas: recarga»).</summary>
        public string Note;
        public string Name;
        public string Cost;
        public string Limits;
        public string Damage;
        public List<StatPair> Stats = new List<StatPair>();
        public string Noise;
        public string Description;
        public bool Active;
        /// <summary>Uses left («×2»); -1, none counted.</summary>
        public int Count = -1;
        /// <summary>Turns before it can be used again; 0, ready.</summary>
        public int Cooldown;
        public bool Free;
        public bool Depleted;
        /// <summary>Its icon's id when it isn't its own (a menu shows its family's).</summary>
        public string Icon;
        /// <summary>Disparar's gun, for its picture on the key (#7 of 23/10).</summary>
        public string Weapon;
    }

    [Serializable]
    public sealed class ActionBarView
    {
        public List<AbilityView> Abilities = new List<AbilityView>();
        /// <summary>Why the keys rest (not this player's orders): «Turno de Ana: puedes apuntar y planear»; empty if they don't.</summary>
        public string Lock;
    }

    // ----------------------------------------------------------------- shot

    /// <summary>A modifier of the odds (aim.ts Modifier): its label and value.</summary>
    [Serializable]
    public sealed class ModView
    {
        public string Label;
        public int Value;
    }

    /// <summary>What the aimed ability takes off (hud.ts DamageView).</summary>
    [Serializable]
    public sealed class DamageView
    {
        public string Hit;
        public string Crit;
        public string Graze;
        public List<string> Notes = new List<string>();
        /// <summary>«Lo mata», «Un crítico lo mata».</summary>
        public string Kill;
    }

    /// <summary>The aim's panel (hud.ts TargetView): an attack, a support ability, or a self one armed by its first press.</summary>
    [Serializable]
    public sealed class ShotView
    {
        /// <summary>attack, support or self.</summary>
        public string Kind;
        public string Title;
        public string Action;
        /// <summary>Without the command: who holds it («MANDO: ANA»), the button off.</summary>
        public string Locked;
        public string TargetName;
        public int Index;
        public int Total;
        public int Hit;
        public int Crit;
        public int Graze;
        public List<ModView> Mods = new List<ModView>();
        public List<ModView> CritMods = new List<ModView>();
        public bool HasDamage;
        public DamageView Damage = new DamageView();
        public string Note;
        public string Noise;
        /// <summary>Support and self: what it does.</summary>
        public string Effect;
        /// <summary>Self: cost · limits, and the line under its effect.</summary>
        public string Cost;
        public string Limits;
        public string Detail;
        /// <summary>Self: how to use it («Otra vez, Espacio o Intro: usar · Esc: cancelar»), or how to pick another spot.</summary>
        public string Hint;
    }

    // -------------------------------------------------------------- mission

    /// <summary>The objective's card (hud.ts MissionView).</summary>
    [Serializable]
    public sealed class MissionView
    {
        /// <summary>The kind's name, or «Asedio».</summary>
        public string Label;
        public string Area;
        public string Objective;
        public string ObjectiveTip;
        /// <summary>Turns left; -1, no timer.</summary>
        public int TurnsLeft = -1;
        /// <summary>A siege's next wave, short and in full, and whether it comes this turn.</summary>
        public string WaveText;
        public string WaveTip;
        public bool WaveUrgent;
        /// <summary>Saqueo: how near the next wave of the horde is, 0–1; -1, no horde.</summary>
        public float HordeFill = -1;
        public string HordeTitle;
        /// <summary>«VIENEN MÁS».</summary>
        public string Alert;
    }

    /// <summary>A siege's compass (#34): shown, and the sides (DIRS4: 0 N, 1 E, 2 S, 3 O) the next wave comes in by.</summary>
    [Serializable]
    public sealed class CompassView
    {
        public bool Shown;
        public List<int> Hot = new List<int>();
    }

    /// <summary>Which floor is shown, on maps with storeys (hud.ts setFloor); Max 0 hides it.</summary>
    [Serializable]
    public sealed class FloorView
    {
        public int Level;
        public int Max;
    }

    // ---------------------------------------------------------------- flags

    /// <summary>The flag over a unit in sight (unitViews.ts renderFlag) and the hover line over an enemy (setTags).</summary>
    [Serializable]
    public sealed class FlagView
    {
        public string Id;
        public string Name;
        public bool Enemy;
        public int Hp;
        public int MaxHp;
        public int Armor;
        /// <summary>-1 none shown, 0 flanked, 1 low, 2 high.</summary>
        public int Cover = -1;
        public List<TagView> Tags = new List<TagView>();
        /// <summary>Over an enemy, from the tile under the pointer: aim, sight, flank or none, and its words («74 %»).</summary>
        public string HoverKind;
        public string HoverText;
        /// <summary>The target being picked: the only flag kept over the shoulder.</summary>
        public bool Target;

        /// <summary>An enemy that sees one of the squad: an eye on its flag (playtest 21/10 #11).</summary>
        public bool Watching;
    }

    [Serializable]
    public sealed class FlagsView
    {
        public List<FlagView> Flags = new List<FlagView>();
    }

    /// <summary>A mark to point at from the screen's edge (edgeMarks.ts EdgeMark): objective, evac or alert, its words, where.</summary>
    [Serializable]
    public sealed class EdgeMarkView
    {
        public string Kind;
        /// <summary>What it is («Objetivo», «Evacuación», the partner's name).</summary>
        public string Name;
        /// <summary>What the arrow says: its name and how far it is from the picked soldier («Objetivo · 12 casillas»).</summary>
        public string Label;
        public Vector3 World;
    }

    [Serializable]
    public sealed class EdgeMarksView
    {
        public List<EdgeMarkView> Marks = new List<EdgeMarkView>();
    }

    // ---------------------------------------------------------------- cards

    /// <summary>The partner's call waiting for this player's answer (hud.ts CallView): to evacuate, or to abandon.</summary>
    [Serializable]
    public sealed class CallView
    {
        /// <summary>evac or abandon.</summary>
        public string Kind;
        public string Kicker;
        public string Title;
        public string Text;
        /// <summary>What happens when the time runs out.</summary>
        public string ClockNote;
        /// <summary>Milliseconds left when it was drawn (the server's deadline); -1, none.</summary>
        public int Ms = -1;
        public string Accept;
        public string Refuse;
    }

    /// <summary>The arrival card (hud.ts intro).</summary>
    [Serializable]
    public sealed class IntroView
    {
        public string Kicker;
        public string Title;
        public string Objective;
        public string Turns;
        public string Briefing;
    }

    /// <summary>The mission's end (hud.ts showOutcome).</summary>
    [Serializable]
    public sealed class OutcomeView
    {
        public bool Victory;
        public string Line;
        public bool Campaign;
        public bool Siege;
    }

    // ----------------------------------------------------------------- menu

    /// <summary>
    /// The game's menu (Esc; hud.ts renderMenu): which entries it has and how, its settings and its controls. Read when it
    /// opens or turns a page; a setting changed is marked by the menu itself.
    /// </summary>
    [Serializable]
    public sealed class GameMenuView
    {
        /// <summary>«Volver al menú de inicio»: not once the mission is over (the report has its own way out).</summary>
        public bool Leave = true;
        /// <summary>«Abandonar la misión» (Lore's words): its tooltip; with `AbandonWaiting`, off and saying so (one's own call waits).</summary>
        public bool Abandon = true;
        public string AbandonLabel;
        public string AbandonTip;
        public string AbandonWaiting;
        /// <summary>«Salir de Bastión» (the desktop app; Lore's words).</summary>
        public bool Quit = true;
        public string QuitLabel;
        public List<SettingView> Settings = new List<SettingView>();
        /// <summary>Controles: each key and what it does; then the night's rules under their heading.</summary>
        public List<ControlView> Controls = new List<ControlView>();
        public string NightLabel;
        public List<string> NightRules = new List<string>();
    }

    /// <summary>
    /// One row of Ajustes (settingsPanel.ts row, speedRow): its name, its help (the tooltip), and either its choices (the
    /// one on marked) or a slider from `Min` to `Max`, with the words at its ends and its value beside it.
    /// </summary>
    [Serializable]
    public sealed class SettingView
    {
        public string Id;
        public string Label;
        public string About;
        public List<string> Choices = new List<string>();
        public int On;
        public bool Slider;
        public float Min;
        public float Max;
        public float Step;
        public float Value;
        public string Low;
        public string High;
    }

    [Serializable]
    public sealed class ControlView
    {
        public string Keys;
        public string What;
    }

    /// <summary>A question before going (hud.ts confirmLeave, confirmAbandon, confirmQuit): its title, its lines, a warning, and its two answers.</summary>
    [Serializable]
    public sealed class ConfirmView
    {
        public string Title;
        public List<string> Lines = new List<string>();
        public string Warning;
        public string Yes;
        public string No;
        /// <summary>«Yes» is the dangerous one (abandoning, quitting): red, and «No» is the one to press.</summary>
        public bool Danger;
    }
}
