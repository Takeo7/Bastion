using System;
using System.Collections.Generic;
using UnityEngine;

// What the HUD is handed to draw (docs/UNITY-UI.md §1): built by the presenter from the engine's state, as game.ts built
// hud.ts's views. Plain [Serializable] classes with public fields, so a piece compares them by their JSON (Ui.Key) and
// draws again only when they say something new. Field by field they are the TypeScript views of hud.ts.

namespace Bastion.UI.Hud
{
    /// <summary>A tag on a card or a flag («VIGILANCIA», «CUBRIÉNDOSE»): its words, its kind (the colour) and its tooltip.</summary>
    [Serializable]
    public sealed class TagView
    {
        public string Text;
        /// <summary>ow, hk, stun, psi, bite, data, evac, aid, in-play: as the CSS classes were.</summary>
        public string Kind;
        public string Tip;
        /// <summary>Its icon on a narrow bust (SquadList.StateIcons): a HUD icon, «state:…».</summary>
        public string Icon;
    }

    // ----------------------------------------------------------------- turn

    /// <summary>Whose turn (hud.ts setTurn): the frame's colour; at the top, the night and the soldier in play.</summary>
    [Serializable]
    public sealed class TurnView
    {
        /// <summary>mine, partner, enemy, or over (the mission is over).</summary>
        public string Owner;
        /// <summary>The partner's name, when it is theirs.</summary>
        public string OwnerName;
        /// <summary>Whose colour, «#rrggbb»; the enemy's is the stamp's red.</summary>
        public string Color;
        /// <summary>«De noche», and what the dark changes (its tooltip); empty by day.</summary>
        public string NightLabel;
        public string NightRules;
        /// <summary>The soldier in play while turns go one soldier at a time; empty if not.</summary>
        public string InPlay;
        /// <summary>Alone (no partner here: Ana, 28/10): no frame round the screen, whose turn it is needs no telling.</summary>
        public bool Alone;
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
        /// <summary>«A punto», «Con acciones», «Con movimiento», «Sin acciones ni movimiento», «Sin conexión», «Ausente», or empty.</summary>
        public string Status;
    }

    [Serializable]
    public sealed class PlayersView
    {
        public List<PlayerView> Players = new List<PlayerView>();
        /// <summary>With the finger: «Ceder» by the partner (this player holds the command, the partner has something to move).</summary>
        public bool CanPass;
    }

    /// <summary>The finger's buttons (TouchButtons): the floors where there are any, and the faster key in the enemy's turn.</summary>
    [Serializable]
    public sealed class TouchView
    {
        public bool Hurry;
        /// <summary>The rucksack's button on (U11): everything to search shown, as Mayús held.</summary>
        public bool Search;
    }

    /// <summary>The floor looked at (FloorPicker, cuadro 121) and whether it can go up or down; null with a single floor.</summary>
    [Serializable]
    public sealed class FloorView
    {
        public int Level;
        public bool CanUp;
        public bool CanDown;
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

    /// <summary>«Terminar turno», the bar's last key (hud.ts setEndTurn): act, confirm, ready, waiting, enemy or disabled, and its help's detail.</summary>
    [Serializable]
    public sealed class EndTurnView
    {
        public string Mode = "act";
        /// <summary>Who is waited for, or how many soldiers still have actions.</summary>
        public string Detail;
        /// <summary>Ready without the command (Arturo, 7/11): who holds it, the turn over when theirs is, the key off (note 10); null, one holds it.</summary>
        public string Holder;
        /// <summary>Mode «done»: the soldier in play whose turn the key ends first (Arturo, 7/11, Ana's #5), and who the command passes to (null, nobody).</summary>
        public string InPlay;
        public string GoesTo;
    }

    // ---------------------------------------------------------------- squad

    /// <summary>A soldier's card under the sheet (hud.ts setSquad; UI-COMBATE UI-02).</summary>
    [Serializable]
    public sealed class SquadCardView
    {
        public string Id;
        /// <summary>The nickname «in quotes» if it has one, else the name.</summary>
        public string Name;
        /// <summary>For a narrow card (more than four): the first name, or the nickname.</summary>
        public string Short;
        /// <summary>This player gives its orders (its own, or the partner's while away): it can be picked.</summary>
        public bool Mine;
        /// <summary>Its owner's colour (#rrggbb): the studio's backdrop behind its bust (Ana, 7/11 #13).</summary>
        public string Color;
        public bool Selected;
        public bool Dead;
        /// <summary>A woman's (Appearance.Sex «f»): «CAÍDA» on the bust, not «CAÍDO» (Lore, 23/11).</summary>
        public bool Female;
        public bool Evacuated;
        public bool InPlay;
        /// <summary>A soldier, with a bust by the name (a siege's turret has none).</summary>
        public bool Bust;
        public int Hp;
        public int MaxHp;
        public int Ap;
        public int ApMax;
        /// <summary>The movement left and the turn's, in tiles (Arturo, 5/11 #14: apart from the actions); MovesMax 0, none shown.</summary>
        public int Moves;
        public int MovesMax;
        /// <summary>A siege's turret: its rounds in place of the actions.</summary>
        public bool Rounds;
        public string Title;
        public List<TagView> Tags = new List<TagView>();
        /// <summary>The first of Separados' second group (U27): a wider gap before it, with a line down it.</summary>
        public bool GroupStart;
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
        /// <summary>
        /// Its word before the number, always in sight (Producto's review, 23/11, B24; cuadro: «PUNT. 55»): the two aims. `Spare`,
        /// the one not of the weapon in hand: its word goes when the strip doesn't fit (the Duo with the finger).
        /// </summary>
        public string Word;
        public bool Spare;
    }

    /// <summary>The picked soldier's panel (hud.ts setSelected), without the backpack (after the first milestone).</summary>
    [Serializable]
    public sealed class UnitPanelView
    {
        public string Id;
        /// <summary>Its states in full, beside its name (with SquadList.StateIcons: the busts show only their icons).</summary>
        public List<TagView> States = new List<TagView>();
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
        public List<StatView> Stats = new List<StatView>();
        public string Weapon;
        /// <summary>
        /// For the sheet inside its card (cuadro 179, with the mouse): its health and its actions, as bars, and its gun's
        /// picture side on, as on its key («weapon:rifle»; null without a gun: WeaponIcon, the blade's).
        /// </summary>
        public int Hp, MaxHp, Ap, ApMax;
        public string WeaponPicture;
        /// <summary>The weapon in the hand's picture, an id of the HUD's table (U29); null, the rifle's as before.</summary>
        public string WeaponIcon;
        public string WeaponTip;
        /// <summary>Nothing in the hand nor in the belt (Content.BareHanded): «Sin cuchillo», its knife's picture dimmed (Lore, 24/11).</summary>
        public bool WeaponBare;
        /// <summary>A weapon of the Armería's state and improvements (arms.ts wearMarks): «GASTADA» (worn), «ROTA» (broken), «+2» (mods).</summary>
        public List<TagView> WeaponMarks = new List<TagView>();
        /// <summary>Its backpack, where there is anything to search or it carries something (game.ts packView); null, none.</summary>
        public PackView Pack;
        /// <summary>
        /// Its cover as a shield (Arturo, 7/11: on this panel, not by its health): 2 full, 1 half, 0 flanked; -1, none (no
        /// cover, or cover doesn't count). The worst against whoever sees it, else the best round it.
        /// </summary>
        public int Cover = -1;
        /// <summary>Its help's title and words (Lore).</summary>
        public string CoverTitle;
        public string CoverTip;
    }

    // --------------------------------------------------- over the battle

    /// <summary>A line of the soldier's sheet: its name, its value and, when it helps, the long version.</summary>
    [Serializable]
    public sealed class SheetRowView
    {
        public string Key;
        public string Value;
        public string Tip;
    }

    /// <summary>
    /// A soldier's sheet over the battle (hud.ts SoldierSheetView; playtest 18/10 #23): who, its figures, what it carries
    /// and its skills; in a campaign its board beside, as in the Cuartel, not to be touched. Only to look at.
    /// </summary>
    [Serializable]
    /// <summary>A bar of the soldier's sheet: its icon, how full («hp», «ap», «moves»: the pips' kinds), its name and what it is for its help.</summary>
    public sealed class SheetBarView
    {
        public string Icon;
        public string Kind;
        public int Now, Max;
        public string Name;
        public string Tip;
    }

    /// <summary>A figure of the soldier's sheet under its icon («67 %», «3–5»); its name, and what it does if Lore says, for its help.</summary>
    public sealed class SheetFigureView
    {
        public string Icon;
        public string Value;
        public string Name;
        public string Tip;
    }

    /// <summary>
    /// A weapon the soldier carries, on its sheet: its picture (the stash's, by kind and type) and its name; the odds with
    /// it («%»: its aim, its critical) apart from what it does (its damage, the critical's) and from the rest (how far, its
    /// rounds, its noise…); its whole help, as in the stash.
    /// </summary>
    public sealed class SheetWeaponView
    {
        public string Kind, Type;
        public string Name;
        public string Tip;
        public List<SheetFigureView> Odds = new List<SheetFigureView>();
        public List<SheetFigureView> Damage = new List<SheetFigureView>();
        public List<SheetFigureView> Rest = new List<SheetFigureView>();
    }

    /// <summary>A thing carried that is no weapon (the vest, the belt's, the pack's): its picture, how many, its name and help. `Packed`: in the pack.</summary>
    public sealed class SheetThingView
    {
        public string Kind, Type;
        public string Icon;
        public int Count = 1;
        public string Tip;
        public bool Packed;
    }

    public sealed class SoldierSheetView
    {
        /// <summary>
        /// The sheet to read with the eyes (Arturo, cuadros 179 and 180; with the mouse): what rises and falls as bars;
        /// the will, the dodge, the defence and the armour, a figure each under its icon; each weapon carried with its
        /// odds («%») apart from its damage and its other figures; and the rest it carries, as the stash draws it. No
        /// word says what is what: each one's name is its help. Empty, the lines of before (Stats, Carries).
        /// </summary>
        public List<SheetBarView> Bars = new List<SheetBarView>();
        public List<SheetFigureView> Guards = new List<SheetFigureView>();
        public List<SheetWeaponView> Weapons = new List<SheetWeaponView>();
        public List<SheetThingView> Things = new List<SheetThingView>();
        public string Name;
        /// <summary>Its veterancy, or what it is if not a soldier.</summary>
        public string Rank;
        /// <summary>A partner's: whose (their name) and their colour; empty, one's own.</summary>
        public string Whose;
        public string WhoseColor;
        public List<SheetRowView> Stats = new List<SheetRowView>();
        public List<SheetRowView> Carries = new List<SheetRowView>();
        /// <summary>The board's cells it has, by name (a skirmish's, where there is no board to show).</summary>
        public List<string> Skills = new List<string>();
        /// <summary>A campaign soldier's board (the Cuartel's window, not editable); null, none.</summary>
        public Bastion.UI.Strategy.SkillBoardView Board;
    }

    /// <summary>The Bastion's file on an enemy (hud.ts enemyFile; right click): what it is, what it does, how to beat it.</summary>
    [Serializable]
    public sealed class EnemyFileView
    {
        public string Name;
        /// <summary>«Salud 3/3 · Puntería 50».</summary>
        public string Stats;
        /// <summary>Under the pointer on them: what its defence and dodge do (M6); null, it has none.</summary>
        public string StatsTip;
        public string What;
        public string Does;
        public string Tip;
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
        /// <summary>The runs (H3): the pack in a grid, the kit's places, the floor and the partners beside; null, the slots of before.</summary>
        public RunPackView Run;
    }

    /// <summary>A chapter's boss on the bar at the top (H6): name, health, armour, its charge ready or in how many turns.</summary>
    [Serializable]
    public sealed class BossView
    {
        /// <summary>Its unit, for its file on a click (OnSheet).</summary>
        public string Id;
        public string Name;
        public int Hp, Max, Armor;
        /// <summary>«Embestida lista», «Embestida en 2 turnos».</summary>
        public string Charge;
        public bool ChargeReady;
        public string Tip;
    }

    /// <summary>
    /// A run's soldier's pack in combat (H3; RunPack): its grid, its kit's places, what lies at its feet laid out in a grid,
    /// and the partners beside it as places to give to. Things go from one to another on the grids' board (GearBoard).
    /// </summary>
    [Serializable]
    public sealed class RunPackView
    {
        public Bastion.UI.Gear.GearGridView Pack;
        public List<Bastion.UI.Gear.GearSlotView> Kit = new List<Bastion.UI.Gear.GearSlotView>();
        /// <summary>What lies at its feet (GameState.FloorGear), packed into a grid four wide; null, nothing.</summary>
        public Bastion.UI.Gear.GearGridView Floor;
        /// <summary>The partners beside it (Slot «give», the partner's name): a thing let go on one is given.</summary>
        public List<Bastion.UI.Gear.GearSlotView> Give = new List<Bastion.UI.Gear.GearSlotView>();
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
        /// <summary>What to do first, on the key itself (#24: «Cargador vacío: recarga»).</summary>
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
        /// <summary>
        /// The picked soldier's movement left, in path cost halves of a tile (Arturo, 5/11 #14): its tiles before the keys;
        /// MovesMax 0, none shown. `MovesTip`, what it is.
        /// </summary>
        public int Moves;
        public int MovesMax;
        public string MovesTip;
        /// <summary>Why the keys rest (not this player's orders): «Turno de Ana: puedes planear los movimientos»; empty if they don't.</summary>
        public string Lock;
    }

    // ----------------------------------------------------------------- shot

    /// <summary>A modifier of the odds (aim.ts Modifier): its label and value.</summary>
    [Serializable]
    public sealed class ModView
    {
        public string Label;
        public int Value;
        /// <summary>Its icon (LineIcon's «aim:…», U1 of 25/11) and a short note by its figure: how far for the reach, what covers.</summary>
        public string Icon;
        public string Note;
        /// <summary>The note in full words for its help, where the figure's is short («a 2 casillas» for «2 c.»; Lore, 25/11).</summary>
        public string About;
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
        /// <summary>The crit at 0, why (cuadro 158): «Sin crítico: está a cubierto»; null otherwise.</summary>
        public string CritNote;
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
        /// <summary>A Limpieza's (O8, Arturo 25/11): the hostiles left on the whole map, seen or not; -1, another fight.</summary>
        public int Left = -1;
        /// <summary>A siege's next wave, short and in full, and whether it comes this turn.</summary>
        public string WaveText;
        public string WaveTip;
        public bool WaveUrgent;
        /// <summary>How near the horde's next wave is, 0–1 (Rules.HordeFill); -1, no horde.</summary>
        public float HordeFill = -1;
        /// <summary>The same as noise (Ana's notes, Arturo 29/10): 40 dB quiet, 90 as the wave comes.</summary>
        public int HordeDb;
        public string HordeTitle;
        /// <summary>The turn the horde can come from, while it can't yet (Motor silencioso, cuadro 165); 0, it can.</summary>
        public int HordeFrom;
        /// <summary>«VIENEN MÁS».</summary>
        public string Alert;
        /// <summary>The weather (Weathers' ids; Arturo, 01/11): its name and what it changes on the card; null, clear.</summary>
        public string Weather;
        /// <summary>
        /// The objective is the stage's plain sentence, as its first card said it, with nothing that changes in it (a Saqueo's
        /// before anything is carried, a Limpieza's, a Travesía's): with the mouse the card leaves it to its help (cuadro 177).
        /// </summary>
        public bool ObjectivePlain;
        /// <summary>
        /// The objective in steps, under the kind (La llave, U22): each a line, the done ones dimmed with «✓»; null, the
        /// one line of `Objective`.
        /// </summary>
        public List<GoalStepView> Steps;
    }

    /// <summary>A step of the objective (U22): its words and whether it is done.</summary>
    [Serializable]
    public sealed class GoalStepView
    {
        public string Text;
        public bool Done;
        /// <summary>A line under it (Mordido's count, U23), red when `NoteUrgent`; null, none.</summary>
        public string Note;
        public bool NoteUrgent;
        /// <summary>The mission lost its point (the bitten turned, U23): in red, no number.</summary>
        public bool Lost;
    }

    /// <summary>A siege's compass (#34): shown, and the sides (DIRS4: 0 N, 1 E, 2 S, 3 O) the next wave comes in by.</summary>
    [Serializable]
    public sealed class CompassView
    {
        public bool Shown;
        public List<int> Hot = new List<int>();
        /// <summary>Its letters at the screen's edges too (a siege's); not the finger's compass of every mission.</summary>
        public bool Edges = true;
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
        /// <summary>
        /// A state on a line of its own over the health (Saqueadores' guards, U28: «DESPREVENIDO», «ALERTA»), not in the row:
        /// the flag stays as wide as its gauge, so two guards two tiles apart don't cover each other's. Null, none.
        /// </summary>
        public TagView State;
        /// <summary>Over an enemy, from the tile under the pointer: aim, sight, flank or none, and its words («74 %»).</summary>
        public string HoverKind;
        public string HoverText;
        /// <summary>By another target's odds (U1; cuadro 158): the icon of what takes most off them; null, nothing does.</summary>
        public string HoverIcon;
        /// <summary>
        /// An enemy's eye, in the circle of its gauge (Ana, 7/11 #1): «open», it sees the picked soldier, or, a move under
        /// the pointer, would see it there (Arturo, 5/11 #5, #6); «closed», it doesn't; null, no gauge (one of ours).
        /// </summary>
        public string Eye;
        /// <summary>The target being picked: the only flag kept over the shoulder.</summary>
        public bool Target;
        /// <summary>
        /// Another target of what is aimed: over the shoulder its hover line stays, its odds, and a click on it aims at it
        /// instead (Arturo, 5/11 #11).
        /// </summary>
        public bool Other;
        /// <summary>
        /// Beside the one aiming and not a target of it, with why not on its hover line (the Golpe silencioso's «te ha visto»:
        /// Gameplay, 16/11): over the shoulder that line stays, as another target's odds, without the click.
        /// </summary>
        public bool Why;
        /// <summary>Its card in the list under the pointer: an arrow over it points it out (Arturo, 31/10 #11).</summary>
        public bool Pointed;
        /// <summary>
        /// Mordido's count over the bitten survivor (U23): the turns left before they turn, from their finding; -1, none.
        /// Its words on hover (Lore's BiteText.Countdown); `CountdownUrgent` at 2 and 1, red and beating.
        /// </summary>
        public int Countdown = -1;
        public string CountdownTip;
        public bool CountdownUrgent;
    }

    [Serializable]
    public sealed class FlagsView
    {
        public List<FlagView> Flags = new List<FlagView>();
    }

    /// <summary>
    /// The enemies' sight (Arturo, 5/11 #6 and #9): its button bottom right, which stays on, and the legend over it while
    /// an enemy's sight is on the ground (one under the pointer, or all of them with the button on).
    /// </summary>
    [Serializable]
    public sealed class SightView
    {
        /// <summary>The button there (in a battle, not from above).</summary>
        public bool Button;
        /// <summary>
        /// There but resting, dimmed and not pressed (over a shoulder, the camera away: the review of 23/11, C08: it went
        /// while a blow was armed, as if gone for good).
        /// </summary>
        public bool Resting;
        /// <summary>The button on: every enemy in sight's at once.</summary>
        public bool On;
    }

    /// <summary>
    /// A tag that stays over a point of the map (overlays.ts setBreakables, setFallenPacks): a siege's door or barricade
    /// that has taken blows, its bar and what it still holds; a fallen soldier's rucksack, «MOCHILA DE CHIARA».
    /// </summary>
    [Serializable]
    public sealed class WorldTagView
    {
        /// <summary>bar, pack or loot.</summary>
        public string Kind;
        public string Text;
        public Vector3 World;
        /// <summary>A bar's: how full (0 to 1), and whether it is down to a third (red).</summary>
        public float Fill;
        public bool Low;
        /// <summary>A loot box's mark (lootMarks.ts): its icon's id, its ring's colour, and how wide it is in the world (it grows and shrinks with the camera).</summary>
        public string Icon;
        public string Ring;
        public float Size;
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
        /// <summary>An icon of the table before the words, or none (El herido's crutch, U26).</summary>
        public string Icon;
        /// <summary>Its colour («#rrggbb») over its kind's, for the frame, the arrow and the words; null, its kind's (Separados' other group in its player's, U27).</summary>
        public string Color;
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
        /// <summary>An expedition's stage (L5, 17/11): its title, its button and the button's words while it goes; null, the mission's.</summary>
        public string Title;
        public string Back;
        public string Going;
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
        /// <summary>Its help on hover (the base's: «La campaña queda guardada»); null, none.</summary>
        public string LeaveTip;
        /// <summary>Under Ajustes' title (the base's: whose they are); null, none.</summary>
        public string SettingsNote;
        /// <summary>«Abandonar la misión» (Lore's words): its tooltip; with `AbandonWaiting`, off and saying so (one's own call waits).</summary>
        public bool Abandon = true;
        public string AbandonLabel;
        public string AbandonTip;
        public string AbandonWaiting;
        /// <summary>Why it is there at all, under it (an expedition's fight with no way out: Rules.AbandonAllowed); null, nothing.</summary>
        public string AbandonWhy;
        /// <summary>«Salir de Bastión» (the desktop app; Lore's words).</summary>
        public bool Quit = true;
        /// <summary>Under «MENÚ»: the mission waits while it is open (the battle's); null, nothing.</summary>
        public string PauseNote;
        /// <summary>Over the whole screen, the battle hidden under it (the battle's, paused: Ana, 28/10).</summary>
        public bool Cover;
        /// <summary>
        /// The battle's camera and command, under Continuar (UI-COMBATE UI-07; they were over «Terminar turno»): «Ir a Mateo»
        /// while this player gives the orders and someone has actions, «Ver a Ana» while the partner does; null, none.
        /// </summary>
        public string GoTo;
        public string Watch;
        /// <summary>«Ceder el mando», for its holder with a partner; off while it can't be passed.</summary>
        public bool Pass;
        public bool PassOn;
        /// <summary>The «Cámara al soldado» switch, as it is now, and its help.</summary>
        public bool Follow;
        public bool FollowOn;
        public string FollowLabel;
        public string FollowAbout;
        public string QuitLabel;
        public List<SettingView> Settings = new List<SettingView>();
        /// <summary>Controles: each key and what it does; then the night's rules under their heading.</summary>
        public List<ControlView> Controls = new List<ControlView>();
        public string NightLabel;
        public List<string> NightRules = new List<string>();
        /// <summary>How cover hides from the dead (Lore.CoverText; Arturo, 5/11 #4) and how moving goes (Lore.MoveText, #14), after the night's.</summary>
        public string CoverLabel;
        public List<string> CoverRules = new List<string>();
        public string MoveLabel;
        public List<string> MoveRules = new List<string>();
        /// <summary>Under «MENÚ», hosting while the partner isn't in: how they join (InviteView); null, nothing.</summary>
        public InviteView Invite;
    }

    /// <summary>
    /// How the partner joins the game this app hosts, on its menu (Esc) in the base and in the mission while they aren't
    /// in (Gameplay, 14/11: «Continuar partida» and «Nueva partida» go straight to the base, past the room's menu, which
    /// showed it): a line, each address with «Copiar», and the key's and the router's lines (RoomText.InviteOf).
    /// </summary>
    public sealed class InviteView
    {
        public string Line;
        /// <summary>What each one is («En tu misma red», «Por internet, con la clave») and the address, copied whole.</summary>
        public List<(string Kind, string Address)> Addresses = new List<(string, string)>();
        /// <summary>The room's key and when it is asked (RoomText.KeyLine); null, none.</summary>
        public string Key;
        /// <summary>The router's port while it opens, or why it won't (RoomText.RouterLine); null, none.</summary>
        public string Router;
        public string Copy;
        public string Copied;
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
        /// <summary>The slider's value as a share («80 %»: a volume), not as a rate («×1,4»).</summary>
        public bool Percent;
        /// <summary>A row of the sound (its switch, its volumes): in Ajustes' first column, the rest in the second.</summary>
        public bool Sound;
        /// <summary>Each choice's own help, besides the row's (settingsPanel.ts autoQualityRow: «Sí», «No»).</summary>
        public List<string> ChoiceTips;
        /// <summary>
        /// What it does in a line, under its name, always in sight (Producto's review, 23/11, B7: a few names didn't say it
        /// without the pointer); null, the name says it. The whole help stays on the pointer (About).
        /// </summary>
        public string Hint;
        /// <summary>
        /// The hint only while the row `HintWith` has `HintWithOn` chosen and this one isn't on `HintNotOn` (−1, any), following
        /// them as they are changed (the FPS cap's, with the sync on and a cap chosen); null `HintWith`, always.
        /// </summary>
        public string HintWith;
        public int HintWithOn;
        public int HintNotOn = -1;
        /// <summary>A line under the row (Ajuste auto's: what it has lowered); gone as soon as one of `NoteGoesWith` changes.</summary>
        public string Note;
        public List<string> NoteGoesWith;
        /// <summary>One of the last rows (the clothes, «Interfaz», «Ajuste auto»): under the sound's when the other column would be the longer.</summary>
        public bool Tail;
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
