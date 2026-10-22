import type { BasePiece } from './base';
import type { Deeds, MissionLines } from './skills';
export interface Vec2 {
  x: number;
  y: number;
}

/**
 * A place a unit can be (docs/FASE6-PLAN.md): a tile and its level, 0 the
 * ground (`tiles` and `elev`), 1–3 a building's storeys (`GameState.levels`).
 * On the wire and in older saves `z` may be missing: it means 0.
 */
export interface Pos extends Vec2 {
  z: number;
}

/**
 * A place as the client or the wire may give it (a hovered tile, a command):
 * without `z`, on the ground. The state always stores a Pos.
 */
export type Place = Vec2 & { z?: number };

/**
 * One storey above the ground (docs/FASE6-PLAN.md): `GameState.levels[k - 1]`
 * is storey k, k storeys up. Only buildings have them, and only on ground at
 * elevation 0.
 */
export interface Level {
  /**
   * What stands on the storey's slab on each tile (index = y * width + x), as
   * on the ground; null where there is no slab (open air, a stairwell).
   */
  tiles: (TileKind | null)[];
  /** What the storey's objects look like (decor.ts), as GameState.decor. */
  decor?: Decor[];
}

export type Team = 'xcom' | 'alien';

/** Player seat in a co-op session. */
export type Slot = 0 | 1;

/**
 * Every tile also has an elevation (`GameState.elev`, in storeys). A walkable
 * tile at elevation 1 is a rooftop: the block under it is solid.
 *
 * floor:        walkable.
 * wall:         indestructible structure, one storey high — full cover, blocks movement and sight.
 * high:         destructible full-cover object (container, pillar, tree) — degrades to `low`.
 * low:          destructible half-cover object (crate, car, low wall) — degrades to `floor`.
 * door:         closed door in a wall: walkable (opens when crossed), blocks sight, full cover.
 * doorOpen:     open doorway: walkable, no cover.
 * window:       window in a wall: crossed by vaulting (extra cost, can't stop on it),
 *               transparent, full cover for whoever stands beside it. Breaks when crossed.
 * windowBroken: same, without the glass.
 * ladder:       walkable floor that also lets units climb to an orthogonal neighbour one storey up
 *               (a ladder, or a building's stairs: decor 'stairs').
 * console:      objective terminal — indestructible full-cover object.
 * roof:         a storey's slab nobody stands on (a pitched roof, a roof with no way up):
 *               only on GameState.levels.
 */
export type TileKind =
  | 'floor'
  | 'wall'
  | 'high'
  | 'low'
  | 'door'
  | 'doorOpen'
  | 'window'
  | 'windowBroken'
  | 'ladder'
  | 'console'
  | 'roof';

/** Visual and generation theme of a map. */
export type Biome = 'city' | 'wilds' | 'facility';

/**
 * What a tile's floor is, on procedural maps: 0 open ground, 1 street or dirt track, 2 inside a building, 3 a yard,
 * 4 out of play (docs/MAPAS.md §5.10: the outline, a 'wall' the client draws as its piece of GameState.frame).
 */
export type GroundKind = 0 | 1 | 2 | 3 | 4;

/**
 * What closes a map round what is played (docs/MAPAS.md §5.10): blocks shut up, fenced lots, ruins, works, a street
 * that goes on past a barricade; a river or canal, the railway, the motorway or a military barrier along one side;
 * in the country, containers, wood, a ravine, rocks or water.
 */
export type FrameKind = 'block' | 'lot' | 'ruin' | 'works' | 'street' | 'river' | 'rail' | 'highway' | 'barrier' | 'containers' | 'forest' | 'ravine' | 'rocks' | 'water';

/**
 * A piece of a map's outline (cosmetic): a rectangle of tiles out of play, every one of them 'wall' with ground 4,
 * each in one piece only. `storeys` on a block, a ruin or works (and GameState.storeys of its tiles, for sight), maybe
 * on the motorway and the railway; `side`, the map's side (a DIRS4 index) a river, the railway, the motorway or a
 * barrier runs along.
 */
export interface FramePiece {
  kind: FrameKind;
  x: number;
  y: number;
  w: number;
  h: number;
  side?: number;
  storeys?: number;
}

/**
 * What a piece of cover on a procedural map is meant to be, so the client can
 * draw it as such: a parked car (two tiles, front and rear), a fence, a shelf…
 * Purely cosmetic: cover, sight and movement only ever look at the tile kind.
 */
export type DecorKind =
  // Streets and car parks.
  | 'carFront'
  | 'carRear'
  | 'planter'
  | 'bin'
  | 'streetTree'
  | 'kiosk'
  | 'fountain'
  | 'fountainCorner'
  | 'statue'
  // Farms and yards.
  | 'fence'
  | 'gatePost'
  | 'hay'
  // Indoors.
  | 'shelf'
  | 'counter'
  | 'table'
  | 'sofa'
  | 'bed'
  | 'hospitalBed'
  | 'medicalShelf'
  | 'gurney'
  | 'medicalCabinet'
  | 'medicalCart'
  | 'wheelchair'
  | 'receptionDesk'
  | 'waitingSeats'
  | 'examTable'
  | 'medicalMonitor'
  | 'xray'
  | 'bedsideTable'
  | 'visitorChair'
  | 'clinicalSink'
  | 'sterilizer'
  | 'ultrasound'
  | 'waitingTable'
  | 'waterDispenser'
  | 'toilet'
  | 'clinicalBin'
  | 'consultDesk'
  | 'desk'
  | 'wardrobe'
  | 'cabinet'
  | 'rack'
  // Yards and facilities.
  | 'crates'
  | 'barricade'
  | 'pipe'
  | 'tank'
  | 'pylon'
  // Country.
  | 'tree'
  | 'bush'
  | 'rock'
  | 'log'
  | 'rubble'
  // A building's stairs (docs/FASE6-PLAN.md): a `ladder` tile, facing the way up.
  | 'stairs'
  // A wall a grenade broke (docs/FASE7.md §4): its stump and the rubble, on a `low` tile.
  | 'breach'
  // The bus parked by the evacuation zone (docs/PRUEBA-2026-10-14.md P1): `high` tiles the client draws it over.
  | 'bus'
  // The special loot boxes (docs/FASE8.md §B), on a `low` tile: weapons, medicines, ammunition, food.
  | 'lootWeapons'
  | 'lootMedical'
  | 'lootAmmo'
  | 'lootFood'
  // The new scenarios (playtest 2026-10-14, #25). Blocks of one kind, apart from each other: one object per block.
  /** A wrecked lorry, a 2 × 4 block of `high` tiles; facing: its nose. */
  | 'truck'
  /** A stack of logs, a 1 × 2 or 2 × 2 block of `low` tiles. */
  | 'logPile'
  /** A water tower on its legs, a 2 × 2 block of `high` tiles. */
  | 'waterTower'
  /** A shipping container, a 2 × 5 block of `high` tiles; facing: its doors. */
  | 'container'
  /** A big roadside sign on its post, one `high` tile; facing: towards the road. */
  | 'sign'
  /** Wire (a fallen fence or barbed wire), a line of `low` tiles like 'fence'. */
  | 'wire'
  // The factory (playtest 2026-10-15, #14 and #17).
  /** The nose of the patrol car or ambulance whose siren a sabotage silences, a `high` tile beside the objective; facing: away from it. */
  | 'sirenCar'
  /** Stacked pallets, a `low` tile; in rows between a factory's zones and by its loading docks. */
  | 'pallets'
  /** Oil drums, a `low` tile. */
  | 'drums'
  /** A forklift, a `low` tile; facing: its forks. Not placed for now: no pack has one (playtest 15/10, #17). */
  | 'forklift'
  /** Factory machinery, a `high` tile inside a hall. */
  | 'machine'
  /** A silo, a 3 × 3 block of `high` tiles (docs/MAPAS.md §5: 2 × 2 before phase 5). */
  | 'silo'
  /** A park bench on a square (playtest 2026-10-18, #12), two `low` tiles side by side; facing: the way one sits. */
  | 'bench'
  // A house's rooms by what they are (playtest 2026-10-18, #15): the kitchen's, the bathroom's, the living room's.
  /** A fridge, a `high` tile against a kitchen's wall; facing: its door. */
  | 'fridge'
  /** A cooker, a `low` tile in a kitchen's run of counters; facing: its front. */
  | 'stove'
  /** A bath, a `low` tile in a bathroom's corner; facing: away from the wall. */
  | 'bathtub'
  /** A washbasin, a `low` tile against a bathroom's wall; facing: away from it. */
  | 'washbasin'
  /** The television on its stand, a `low` tile against a living room's wall, facing the sofa. */
  | 'tvStand'
  /** A house's toilet (not the clinics' 'toilet'), a `low` tile in a bathroom's corner. */
  | 'wc'
  // What used to be drawn on free floor, now on tiles of its own (Arturo, 19/10: «que ocupen»; furnishing.ts). All `low`
  // but the drinks fridge; the client draws each as before (homeFurnishing.ts, dressing.ts).
  /** An armchair across the table from a sofa, or at its side; facing: the way one sits. */
  | 'armchair'
  /** A coffee table before a sofa; facing: the sofa's. */
  | 'coffeeTable'
  /** A standard lamp in a corner. */
  | 'floorLamp'
  /** A potted plant in a corner (or a dining chair's place in a bedroom's). */
  | 'pottedPlant'
  /** A house's bedside table by its bed (its lamp on it); facing: the bed's. */
  | 'nightstand'
  /** A chair on its own in a bedroom's corner; facing: into the room. */
  | 'diningChair'
  /** A towel rail against a bathroom's wall; facing: away from it. */
  | 'towelRack'
  /** A coat stand in a hall's corner; facing: away from the wall. */
  | 'coatRack'
  /** A café's table, its two chairs tucked in on its tile. */
  | 'cafeTable'
  /** A bookshop's or a library's reading table, its chair tucked in on its tile. */
  | 'readingTable'
  /** A grocer's drinks fridge, a `high` tile against a wall; facing: its door. */
  | 'drinksFridge'
  /** A barn's wheelbarrow. */
  | 'wheelbarrow'
  /** A barn's tool leant against its wall (a pitchfork, a rake, a spade); facing: the wall. */
  | 'tools'
  /** A fire hydrant by the kerb; facing: the street. */
  | 'hydrant'
  /** A mailbox by the kerb; facing: the street. */
  | 'mailbox'
  /** What lies about and stands up (homeDressing, medicalAbandonment: a chair over, bags, a fallen drip): its MedicalDetail draws it. */
  | 'strewn'
  /** A garden shed at the back of a plot (docs/MAPAS.md §5.4), 2 × 3; facing: its door. */
  | 'shed'
  /** A washing line between two posts across a back garden, 1 × 3; facing: along it. */
  | 'washingLine'
  /** A vegetable patch in a back garden, 2 × 3, low cover; facing: along its rows. */
  | 'vegPatch'
  /**
   * Furniture piled against a window from inside so the dead don't get in (playtest 21/10, #20: a wardrobe on its
   * side, a table, a mattress, planks), a `high` tile; every window of its room has one. Facing: the window.
   */
  | 'windowBarricade'
  /**
   * Another tile of an object that stands over several (docs/MAPAS.md, phase 1: a car 1 × 3, a bed 1 × 2): what it is
   * is its anchor's, the tile that says the object's kind; facing: the step towards the anchor (decor.ts objectTiles).
   */
  | 'part';

/** What makes the noise a sabotage has to silence (playtest 2026-10-15, #14): a patrol car, an ambulance, a factory's alarm. */
export type Alarm = 'police' | 'ambulance' | 'factory';

/** A scenario a generated map can be (playtest 2026-10-14, #25): a forest's service area, a city's industrial estate. */
export type Site = 'roadside' | 'industrial';

/** A tile's decor hint packed in one number (0: none); see decor.ts. */
export type Decor = number;

/** 0 = none, 1 = half (low), 2 = full (high). */
export type CoverLevel = 0 | 1 | 2;

export type ClassId = 'assault' | 'grenadier' | 'sharpshooter' | 'specialist';
/** The humans (trooper, officer, lancer) and the zombies (zombie, xenoid, mec, sectoid, bloater: `undead` in TEMPLATES). */
export type AlienId = 'trooper' | 'officer' | 'xenoid' | 'lancer' | 'mec' | 'sectoid' | 'zombie' | 'bloater';
/** Mission objectives that are units: the alien relay to destroy and the VIP to extract. */
export type ObjectiveId = 'relay' | 'vip';
/** The base's defences that are units (docs/FASE12.md §11.4): the turret, which shoots on its own. */
export type DefenceId = 'turret';
/** 'rookie': a recruit with no class yet (classes.ts), a rifle and the basic actions. */
export type TemplateId = ClassId | 'rookie' | AlienId | ObjectiveId | DefenceId;

/** Head pieces a soldier can wear (barracks customization). */
export type HeadId = 'helmet' | 'hood' | 'beret' | 'cap' | 'mask' | 'bare';
/** Body builds. */
export type BuildId = 'slim' | 'normal' | 'heavy';
/** Armour pieces the barracks paints one by one (appearance.ts). */
export type ArmorPart = 'helmet' | 'torso' | 'arms' | 'legs';

/**
 * How a soldier looks; colours are `#rrggbb`. Purely cosmetic. The pieces
 * are optional: without one, the piece wears what it wore before pieces
 * could be painted (`partColor`), so older looks stay as they were. The
 * owner's mark is never part of the look: it always glows in their colour.
 */
export interface Appearance {
  /** Main armour colour: every piece without its own. */
  primary: string;
  /** Plates, trim and cloth. */
  secondary: string;
  /** Visor and lens glow. */
  visor: string;
  head: HeadId;
  build: BuildId;
  /** The soldier's sex (from their name): with `character` unset, the client picks a model of that sex. */
  sex?: 'f' | 'm';
  /** The headgear: a helmet's shell, or a beret, cap or hood (those follow `secondary` when unset). */
  helmet?: string;
  /** Chest, back, belt and packs. */
  torso?: string;
  /** Shoulder pads and bracers. */
  arms?: string;
  /** Thigh and shin plates. */
  legs?: string;
  /** Which survivor model the soldier is (the client's Synty list, e.g. 'hunter'); unset = picked from the soldier's id. */
  character?: string;
}

export type AbilityId =
  | 'shoot'
  | 'pistol'
  | 'slash'
  | 'stunLance'
  | 'overwatch'
  | 'hunker'
  | 'reload'
  | 'grenade'
  | 'launch'
  | 'missiles'
  | 'evac'
  | 'hack'
  /** Opening a special loot box beside the soldier (docs/FASE8.md §B; before, any furniture or car). */
  | 'search'
  /** Searching the room the soldier is in, once for the squad (docs/FASE8.md §B). */
  | 'searchRoom'
  /** Opening or closing a door beside the soldier without moving (playtest 2026-10-13): free. */
  | 'openDoor'
  | 'closeDoor'
  // Granted by perks.
  | 'runAndGun'
  | 'rapidFire'
  | 'suppression'
  | 'chainShot'
  | 'rupture'
  | 'aimedShot'
  | 'lightningHands'
  | 'faceoff'
  | 'killZone'
  | 'revival'
  | 'restoration'
  // Fase 9's talents (skills.ts).
  /** Hacerse sitio: a Slash at every enemy alongside. */
  | 'elbowRoom'
  /** Apaños: 40 of condition back to one's own weapon or an adjacent ally's. */
  | 'patchUp'
  // Fase 10 (docs/FASE10.md §4).
  /** A stone, always at hand: heard where it lands, 1 of damage to whoever it hits. Doesn't end the turn. */
  | 'stone'
  /** Overwatch with the blade: strikes the first enemy that comes alongside. */
  | 'overwatchMelee'
  // Alien powers.
  | 'claw'
  | 'mindspin'
  | 'reanimate'
  /** The Hinchado bursts. */
  | 'burst'
  /** In a siege (docs/FASE12.md §4), the dead beat on a door or a barricade beside them. */
  | 'smash'
  // Granted by equipment.
  | 'smoke'
  | 'flashbang'
  | 'firstAid';

/** Abilities that roll to hit against a single unit. */
export type AttackAbility =
  | 'shoot'
  | 'pistol'
  | 'slash'
  | 'stunLance'
  | 'claw'
  | 'rapidFire'
  | 'chainShot'
  | 'aimedShot'
  | 'rupture'
  | 'lightningHands'
  | 'faceoff';

/** Area abilities thrown or fired at a tile. */
export type BlastAbility = 'grenade' | 'launch' | 'missiles' | 'smoke' | 'flashbang' | 'burst';

/** Limited-use items. */
export type ChargeId =
  | 'grenade'
  | 'missiles'
  | 'smoke'
  | 'flashbang'
  | 'firstAid'
  // Fase 9's talents with uses per mission (skills.ts).
  | 'restoration'
  | 'patchUp';

/** Equipment fabricated in the base: one utility item and one ammo type per soldier. */
export type ItemId = 'fragGrenade' | 'smokeGrenade' | 'flashbang' | 'firstAid' | 'nanoVest' | 'mindShield' | 'apRounds' | 'tracerRounds';
export type ItemSlot = 'utility' | 'ammo';

/**
 * One thing found searching (docs/FASE2.md, FASE4.md): exactly one is set.
 * Each takes a slot of the backpack. The first three are lore.ts SearchFind's.
 */
export interface Loot {
  /** Suministros (credits in the campaign). */
  supplies?: number;
  /** An accessory, as the armoury sells it. */
  item?: ItemId;
  /** Medicines (a counter in the campaign; phase 3 uses them). */
  medicines?: number;
  /** Rations (docs/FASE4.md): the Bastion eats every day. */
  food?: number;
  /** Parts, for the Taller to make weapons and armour. */
  parts?: number;
  /** A weapon of the Armería (content.ts FOUND_WEAPONS): to the Armería, for one soldier; equipped in the mission (docs/FASE8.md §C). */
  weapon?: FoundWeaponId;
  /** That weapon is already the Armería's (FoundWeapon.id): a soldier's own, swapped for a find. It goes back, not in as new. */
  weaponId?: string;
  /** That weapon's condition and what is mounted on it (docs/FASE9.md §5); absent, 100 and nothing. */
  condition?: number;
  mods?: WeaponModId[];
  /** A loose improvement for a weapon, to mount in the Armería (docs/FASE9.md §5). */
  mod?: WeaponModId;
  /** Magazines (docs/FASE7.md): straight to the searcher's spares, not into the backpack. */
  mags?: number;
}

/** The Armería's weapons (docs/FASE8.md §A, content.ts FOUND_WEAPONS): the plain ones, and the good ones found searching (docs/FASE4.md). */
export type FoundWeaponId =
  | 'assaultRifle'
  | 'pumpShotgun'
  | 'precisionRifle'
  | 'machineGun'
  | 'machete'
  | 'huntingRifle'
  | 'sawnOff'
  | 'scopedRifle'
  | 'lightMachineGun';

/**
 * What a campaign soldier carries (docs/FASE8.md §A), besides the pistol everyone has: the class no longer
 * brings a weapon. Absent, the template's (skirmish, the enemies, missions saved before).
 */
/** What can be mounted on a weapon (docs/FASE9.md §5), two at most: Mira, Culata, Cargador ampliado, Silenciador. */
export type WeaponModId = 'scope' | 'stock' | 'extMag' | 'silencer';

export interface Arms {
  /** In the hand; null, none: the pistol is the main gun. */
  primary: FoundWeaponId | null;
  /** The Armería's weapon it is (FoundWeapon.id); absent, picked up in this mission. */
  primaryId?: string;
  /** That weapon's condition, 0 to 100 (docs/FASE9.md §5); absent, 100. Every shot wears it. */
  condition?: number;
  /** What is mounted on it. */
  mods?: WeaponModId[];
  /** At the belt; null, the knife. */
  melee: FoundWeaponId | null;
  meleeId?: string;
}

/** What a successful Mindspin does to its victim ('controlled' only in missions saved before the zombies: the Chillón's scream never takes control). */
export type PsiEffect = 'disoriented' | 'panicked' | 'controlled';

export type ShotResult = 'miss' | 'graze' | 'hit' | 'crit';

/**
 * Why the squad lost its concealment (the client explains it): an alien saw a
 * soldier walk into its detection range (`spotted`) or a patrol walked up to
 * one (`patrol`); a soldier attacked or set off an explosive; a failed hack
 * raised the alarm; reinforcements landed.
 */
export type ConcealReason = 'spotted' | 'patrol' | 'attack' | 'explosive' | 'alarm' | 'reinforcements';

/**
 * Why a pod came alive: a soldier came into its sight (`sighted`), it ran into
 * the squad on patrol or it was attacked. `noise` only in older saves: noise
 * no longer wakes a pod, it sends it to look (`Unit.heard`).
 */
export type PodCause = 'sighted' | 'patrol' | 'attacked' | 'noise';

export type Outcome = 'victory' | 'defeat';
export type EndReason = 'objective' | 'wiped' | 'timer' | 'abandoned' | 'objectiveLost';

/**
 * elimination: kill every hostile.
 * recovery:    pick up the data and extract it.
 * sabotage:    destroy the alien relay before the timer runs out.
 * hack:        hack the terminal before the timer runs out (failures raise the alarm).
 * rescue:      reach the VIP and extract them alive.
 * scavenge:    «Saqueo»: search what you can and get out by the evac; no timer, but the
 *              horde comes in by the edges, sooner the more noise the squad makes.
 */
export type MissionKind = 'elimination' | 'recovery' | 'sabotage' | 'hack' | 'rescue' | 'scavenge';

/**
 * Behaviours the rules know by name: a soldier gets them from the cells of its
 * hex board (board.ts `perk`), and the active ones bring their ability
 * (board.ts PERK_ABILITY).
 */
export type PerkId =
  // Shooting.
  | 'aimedShot'
  | 'rapidFire'
  | 'chainShot'
  | 'guardCrit'
  | 'deathFromAbove'
  | 'lightningHands'
  | 'serenity'
  | 'coveringFire'
  | 'killZone'
  | 'faceoff'
  | 'rupture'
  | 'suppression'
  | 'holoTargeting'
  // Melee.
  | 'elbowRoom'
  | 'implacable'
  | 'bladeMaster'
  /** Muro de acero: melee overwatch strikes every enemy that comes alongside, once each. */
  | 'bladeWall'
  /** Segundo aliento: a melee kill heals 1. */
  | 'secondWind'
  // Throwing.
  | 'salvo'
  | 'goodArm'
  | 'safeBlast'
  // Medicine.
  | 'revivalProtocol'
  | 'bridge'
  | 'lightHands'
  | 'restoration'
  // Searching.
  | 'patchUp'
  // Fitness.
  | 'runAndGun'
  | 'shadowstep';

/**
 * Bonuses baked into a unit when a mission starts (rank, perks and the
 * squad's engineering upgrades). Aliens have all zeros.
 */
export interface UnitMods {
  aim: number;
  crit: number;
  defense: number;
  /** Extra tiles per action point. */
  mobility: number;
  /** Primary weapon damage. */
  damage: number;
  meleeDamage: number;
  pistolAim: number;
  /** Bonus within 4 tiles. */
  closeRangeAim: number;
  closeRangeCrit: number;
  grenadeDamage: number;
  grenadeRadius: number;
  heal: number;
  aid: number;
  meleeAim: number;
  /** Extra damage against flanked targets. */
  flankDamage: number;
  /** Extra armour shredded by primary weapon hits. */
  shred: number;
  will: number;
  /** Armour ignored by primary weapon hits (armour-piercing rounds). */
  pierce: number;
  /** Graze chance against this unit (talents add to the template's). */
  dodge: number;
  /** Extra damage on critical hits. */
  critDamage: number;
  meleeCrit: number;
  pistolDamage: number;
  pistolCrit: number;
  /** Extra damage within 4 tiles. */
  closeDamage: number;
  /** Aim when shooting down from a higher level. */
  heightAim: number;
  /** Aim lost to the reaction-shot penalty is cut by this many percentage points. */
  reactionKeep: number;
  /** Penalty taken off reaction shots aimed at this unit. */
  reactionDefense: number;
  /** Defence while on overwatch. */
  watchDefense: number;
  /** Squadsight costs this much less aim per tile. */
  squadsightKeep: number;
  /** "Llave doble": aim with a partner's soldier within 4 tiles of the shooter. */
  pairAim: number;
  /** "Llave doble": defence with a partner's soldier within 4 tiles. */
  pairDefense: number;
  /** "Llave doble": aim against targets within 4 tiles of a partner's soldier. */
  spotterAim: number;
  grenadeShred: number;
  grenadeRange: number;
  /** Extra aim allies get from this unit's holo-targeting. */
  holoAim: number;
  /** Extra aim this unit's Suppression takes away. */
  suppressAim: number;
  hack: number;
  /** Against the Chillón's scream: 1 = never panics («Nervios de acero»), 2 = immune («Serenidad»). */
  psiGuard: number;
  /** Golpe al oído (board.ts): the Slash disorients on a hit. */
  slashDisorient: number;
  /** Ojos del viento: extra tiles of sight. */
  sight: number;
  /** Ojos del viento: smoke doesn't shield targets from this unit (1/0). */
  smokeSight: number;
  /** Voz tranquila and the like (board.ts): +10 will per level to allies within 4 tiles. */
  calmAura: number;
  // Fase 9 (skills.ts).
  /** Tiro: aim with firearms (the pistol too), not with the blade. */
  gunAim: number;
  /** Tiro 3: crit with firearms. */
  gunCrit: number;
  /** Sin hacer ruido (1/0): searching, opening boxes and forcing make no noise. */
  quiet: number;
  // Fase 10 (board.ts).
  /** The stone's extra damage, and how many tiles farther it is heard. */
  stoneDamage: number;
  stoneNoise: number;
  /** Mantenimiento (1/0): its weapons wear half. */
  care: number;
}

export interface Unit {
  id: string;
  team: Team;
  template: TemplateId;
  name: string;
  /** Owning player for XCOM soldiers; null for aliens and shared (pool) units. */
  owner: Slot | null;
  pos: Pos;
  hp: number;
  maxHp: number;
  /** Damage absorbed per hit; explosives shred it. */
  armor: number;
  ap: number;
  ammo: number;
  /** Spare magazines for the primary weapon (docs/FASE7.md): only the squad counts them; without it, reloading never runs out. */
  mags?: number;
  /** A campaign soldier's own weapons (docs/FASE8.md §A); absent, the template's. */
  arms?: Arms;
  /** Rounds in the pistol carried beside a primary weapon (content.ts PISTOL_CLIP); `ammo` is the main gun's. */
  pistolAmmo?: number;
  charges: Partial<Record<ChargeId, number>>;
  overwatch: boolean;
  hunkered: boolean;
  /** Aid Protocol: defense bonus until the start of the squad's next turn (0 = none). */
  aided: number;
  /** Stunned units start their next turn with a single action. */
  stunned: boolean;
  /** False once dead or evacuated: the unit is no longer on the battlefield. */
  alive: boolean;
  evacuated: boolean;
  podId: string | null;
  /** Left behind when the bus went (docs/PRUEBA-2026-10-14.md P1): off the field, and lost to the campaign. */
  lost?: boolean;
  rank: number;
  perks: PerkId[];
  mods: UnitMods;
  kills: number;
  /** Soldier id in the campaign, when the mission belongs to one. */
  campaignId: string | null;
  /** Campaign soldiers: chosen look and nickname (absent for aliens and skirmish). */
  appearance?: Appearance;
  nickname?: string;
  /** Campaign soldiers: weapon and armour tier they carry (1 when absent). */
  weaponTier?: number;
  armorTier?: number;
  /** Campaign soldiers carrying a found weapon (docs/FASE4.md): its name, for the card; absent, the class weapon of its tier. */
  weaponName?: string;
  /** Mission objective (the relay): never acts, doesn't count as a hostile. */
  objective: boolean;
  /** VIP waiting to be reached: can't act and the aliens ignore them. */
  captive: boolean;
  /** Resistance to psionic attacks. */
  will: number;
  /** Turns left before each ability can be used again. */
  cooldowns: Partial<Record<AbilityId, number>>;
  /** Kill Zone: overwatch that keeps firing, once per enemy (ids already shot). */
  killZone: boolean;
  reactedTo: string[];
  /** Overwatch with the blade (docs/FASE10.md §4): strikes whoever comes alongside instead of shooting. */
  watchMelee?: boolean;
  /** A soldier's experience lines in the mission (skills.ts MissionLines): its bars fill out there. */
  lines?: MissionLines;
  /** A vest found and put on out there (playtest 16/10 #18): its health counts at once, and it comes home. */
  worn?: ItemId[];
  /**
   * A defence of the base that acts on its own (a turret, docs/FASE12.md §11.4):
   * nobody gives it orders, it has no turn, it keeps watch in the enemy's and
   * it doesn't hold the base (only soldiers do). `piece`: its piece on the base.
   */
  automaton?: { piece: string };
  /** Belt things found and put on the belt out there (playtest 18/10 #5): usable at once; home if not dipped into (loot.ts beltedBack). */
  belted?: ItemId[];
  /**
   * Where the unit looks, in radians round the map (0 along +x, π/2 along +y):
   * the way it last walked, struck or heard something. The dead see only in
   * a cone that way (los.ts inViewCone, Arturo 17/10).
   */
  facing?: number;
  /** Unit this one keeps pinned down with Suppression. */
  suppressing: string | null;
  /** −aim while pinned down; moving draws fire. */
  suppressedBy: string | null;
  /** Holo-targeting: aim bonus allies get against this unit. */
  marked: number;
  /** Rupture: extra damage taken from every hit. */
  ruptured: number;
  /** Turns of disorientation left (−aim, no explosives). */
  disoriented: number;
  /** Loses its next turn cowering. */
  panicked: boolean;
  /** Alien that mind-controls this soldier, and the turns it lasts. */
  controlledBy: string | null;
  controlTurns: number;
  /** A corpse raised by a sectoid can't be raised again. */
  reanimated: boolean;
  /** Implacable / Death From Above refund an action once per turn. */
  refunded: boolean;
  /** Equipment carried (grants abilities such as smoke grenades). */
  gear: ItemId[];
  /** Enemies: where the last noise it heard came from (docs/FASE2.md); it goes to look while it sees nobody. */
  heard?: Pos | null;
  /**
   * The dead: the soldier it last saw, whom it follows wherever they go while it sees none of the living, and how
   * many of its turns have gone by since; after HUNT_TURNS of them it forgets them (Arturo, 22/10). Each its own:
   * the dead don't tell each other.
   */
  hunting?: { id: string; missed: number } | null;
  /** Soldiers: what they found searching (BACKPACK_SLOTS at most). It counts if they get out or stand at a victory. */
  backpack?: Loot[];
  /** Backpack slots when not BACKPACK_SLOTS (the Chapistería's one more, docs/FASE8.md §C). */
  packSlots?: number;
  /** Soldiers: what they did this mission that teaches (Fase 9, skills.ts), counted by the events. */
  deeds?: Deeds;
  /** A zombie risen from a soldier of the squad (its id): it keeps that soldier's `appearance` (playtest 2026-10-16). */
  risenFrom?: string;
  /** Bitten and infected (docs/FASE3.md): no effect in combat, but if it falls it rises, and back at base it turns. */
  infected?: boolean;
}

/**
 * Light at night (docs/ALTURA-Y-NOCHE.md): whoever stands on a tile it lights
 * is seen from as far as by day. The map's street lights never go out; a
 * Bengala's light and fire last `turns` of the squad's turns.
 */
/** A trap of the base on a tile (docs/FASE12.md §11.4): it goes off when one of the dead steps on it. `piece`: its piece on the base. */
export interface Trap {
  piece: string;
  kind: 'mine' | 'spikes' | 'snare';
  node: number;
  charges: number;
}

export interface Light {
  id: string;
  tile: Pos;
  radius: number;
  /** Squad turns it still lasts; absent for the map's own lights. */
  turns?: number;
  /** The tiles it lights, by node (grid.ts nodeOf; on the ground, y * width + x): within `radius` and in its line of sight (los.ts lightTiles). */
  tiles?: number[];
}

/** A smoke cloud: units inside are harder to hit. */
export interface SmokeCloud {
  pos: Pos;
  radius: number;
  /** XCOM turns it still lasts. */
  turns: number;
}

export interface Pod {
  id: string;
  active: boolean;
  unitIds: string[];
  /** Waypoints an inactive pod walks between during the alien turn (empty = it stays put). */
  patrol: Pos[];
  patrolIndex: number;
}

/**
 * Enemies arriving mid-mission. Since the playtest of 2026-10-13, only the
 * horde (`horde`): zombies that come in by a map edge, where they land, with
 * no flare. Missions saved before may still hold a drop announced by a flare
 * one turn before.
 */
export interface Reinforcement {
  id: string;
  /** Alien turn at the end of which they land. */
  turn: number;
  /** A drop's landing tile, chosen when its flare goes up; always null for the horde (no exact spot is ever announced). */
  pos: Pos | null;
  units: TemplateId[];
  landed: boolean;
  /** Saqueo: zombies coming in by the edges, drawn by the noise; they land on the prowl, not alert. */
  horde?: boolean;
  /** The few that come in every turn (playtest 2026-10-15, #11), apart from the waves. */
  trickle?: boolean;
  /** A siege's wave (docs/FASE12.md §4): the map edge it comes in by, a DIRS4 index (0 north, 1 east, 2 south, 3 west). */
  side?: number;
}

/**
 * A siege of the base (docs/FASE12.md): the dead come in waves
 * (MissionState.reinforcements, each by its `side`, any of the four) and go
 * for the soldiers, breaking down the doors and barricades in their way
 * (GameState.breakables). The base falls when nobody is left to defend it;
 * it holds once enemy turn `hold` is over, or when every wave is in and down
 * (Arturo, 18/10).
 */
export interface Siege {
  hold: number;
}

export interface MissionItem {
  /** Where it lies, or null while someone carries it. */
  pos: Pos | null;
  carrier: string | null;
  evacuated: boolean;
}

export interface MissionState {
  kind: MissionKind;
  /** XCOM turns left to secure the objective; null when there is no timer. */
  turnsLeft: number | null;
  item: MissionItem | null;
  evacZone: Vec2[];
  /** The bus parked beside the evac zone (docs/PRUEBA-2026-10-14.md P1): its tiles, and the way it drives off (DIRS4 index). */
  evacBus?: { tiles: Vec2[]; heading: number };
  /** Hack missions: the terminal (a `console` tile). */
  terminal: Pos | null;
  /** Sabotage: the relay unit; rescue: the VIP unit. */
  objectiveUnit: string | null;
  /** Relay destroyed, terminal hacked, data or VIP extracted. */
  objectiveDone: boolean;
  /** The bus has left (docs/PRUEBA-2026-10-14.md P1): whoever was in it got out, the rest were lost. */
  evacDone?: boolean;
  /** Hack bonus from the campaign (research), added to every hack attempt. */
  hackBonus: number;
  /** A sabotage's alarm (playtest 2026-10-15, #14); missing in older saves and outside the campaign: a siren. */
  alarm?: Alarm;
  /** Medicines also stop a bite's contagion, for everyone (docs/FASE8.md §C: the Enfermería from level 2). */
  medicineCures?: boolean;
  /** The campaign's Bastion has a Cuarentena (playtest 2026-10-15, #13); false, the bitten can't board La Lata. Missing (a skirmish, older saves): no rule. */
  quarantine?: boolean;
  reinforcements: Reinforcement[];
  /** 1–4, for the loot tables (missing in missions saved before searching: 2). */
  difficulty?: number;
  /** Noise the squad has made, in horde points (content.ts hordePoints): it brings the Saqueo's horde sooner. */
  noise?: number;
  /** Saqueo: who comes in each wave of the horde; absent on missions without one. */
  horde?: TemplateId[];
  /** A night mission (docs/ALTURA-Y-NOCHE.md): the living see less, light matters. Missing in older saves: day. */
  night?: boolean;
  /** A siege of the base (docs/FASE12.md); an elimination mission otherwise. */
  siege?: Siege;
}

export type MedicalUse = 'reception' | 'consultation' | 'treatment' | 'ward' | 'equipment' | 'nurses' | 'waiting' | 'supplies' | 'hygiene' | 'stairs';
export interface MedicalRoom {
  x: number;
  y: number;
  w: number;
  h: number;
  z: number;
  use: MedicalUse;
}

export interface MedicalDetail {
  kind: 'corpseCivilian' | 'corpseStaff' | 'coveredCorpse' | 'coveredBed' | 'bareBed' | 'privacyScreen' | 'medicalSupplies' | 'luggage' | 'crutches' | 'books' | 'wallCross' | 'cautionSign' | 'safeSign' | 'bloodPool' | 'bloodDrops' | 'wallBlood' | 'floorGrime' | 'scatteredPapers' | 'medicalWaste' | 'overturnedChair' | 'fallenIV' | 'spilledSupplies' | 'rumpledLinen';
  x: number;
  y: number;
  z: number;
  facing: number;
  height: number;
  variant?: number;
}

/** A medical building's bounds, including walls; wear is cosmetic, from 0 to 1. */
/** What a room of a house is (playtest 2026-10-18, #15). */
/** What a house's room is for; a lavatory and a pantry two by three at least (docs/MAPAS.md §4.3, decided by Arturo 19/10). */
export type HomeUse = 'living' | 'kitchen' | 'bedroom' | 'bath' | 'study' | 'hall' | 'toilet' | 'pantry';
export interface HomeRoom {
  x: number;
  y: number;
  w: number;
  h: number;
  z: number;
  use: HomeUse;
}

/**
 * A house and its rooms by use, as the client finishes and dresses them: how
 * worn it is (0 kept, about 0.5 lived in rough, near 1 wrecked) and, seeded,
 * what lies about in it (the medical centres' details: bodies, blood, mess).
 */
/** A supermarket (playtest 20/10, #18): its bounds, walls included, and the side its glass front and its sign look onto (DIRS4). */
export interface MarketSite {
  x: number;
  y: number;
  w: number;
  h: number;
  front: number;
  /**
   * How much is left on its shelves (playtest 21/10, #19): 'full', the shelves still stocked, if a little picked
   * over, and searching it always turns up food; 'picked', stripped bare, maybe something, maybe nothing (loot.ts
   * rollRoom). Missing on maps made before: 'picked'.
   */
  stock?: 'full' | 'picked';
}

export interface HomeSite {
  x: number;
  y: number;
  w: number;
  h: number;
  wear: number;
  rooms: HomeRoom[];
  /** Seeded scenery, stored with the mission. Never affects movement, cover or AI. */
  dressing?: MedicalDetail[];
}

export interface MedicalSite {
  x: number;
  y: number;
  w: number;
  h: number;
  wear: number;
  kind?: 'clinic' | 'ward' | 'hospital';
  rooms?: MedicalRoom[];
  /** Seeded scenery, stored with the mission. Never affects movement, cover or AI. */
  dressing?: MedicalDetail[];
}

/**
 * The real-time mode (docs/TIEMPO-REAL.md, step 1): the mission's clock and what each one is doing. The host's clock
 * (realtime.ts realtimeStep) moves it on a tick at a time; a unit's order stands until it is done or changed.
 */
export interface Realtime {
  /** Mission time, in ms. */
  clock: number;
  paused: boolean;
  /** How fast the clock runs (1, or RT_SLOW while someone chooses what an ability is aimed at: realtime.ts). */
  speed?: number;
  /** By unit id (made as each one first does something). */
  units: Record<string, RtUnit>;
}

/**
 * A standing order: stay; go to a tile; attack someone with an attack (the gun's, kept up until they are down; a
 * special one, once); keep watch (shoot, or strike, whatever comes in reach); or use an ability once, on oneself, an
 * ally or a tile (getting there first if it must).
 */
export type RtOrder =
  | { kind: 'idle' }
  | { kind: 'move'; to: Pos }
  | { kind: 'attack'; target: string; ability?: AbilityId }
  | { kind: 'watch'; melee?: boolean }
  | { kind: 'act'; ability: AbilityId; target?: string; tile?: Pos };

/**
 * What a unit is doing: nothing, walking a step (its `moved` came when it set off), aiming (the shot comes when it
 * ends), reloading, winding up a blow (the bite comes when it ends), or waiting (someone in the way, no line of fire,
 * getting its breath back after a blow).
 */
export type RtDoing = 'idle' | 'walk' | 'aim' | 'reload' | 'strike' | 'wait' | 'act';

export interface RtUnit {
  order: RtOrder;
  doing: RtDoing;
  /** The clock (ms) at which what it is doing ends. */
  until: number;
  /** Whom it is aiming at or striking. */
  target?: string;
  /** With what: the attack it aims or strikes with, the ability it is getting ready (`act`). */
  ability?: AbilityId;
  /** The host's own bookkeeping, never sent: the steps it means to take, how often it has waited for one, and when its own round ends (realtime.ts RT_ROUND). */
  path?: Pos[];
  waited?: number;
  round?: number;
}

export interface GameState {
  /** Number of events applied so far. Clients use it to detect gaps. */
  seq: number;
  /** Played in real time instead of by turns (docs/TIEMPO-REAL.md); absent, by turns. */
  realtime?: Realtime;
  mapId: string;
  /** Display name of the area (generated maps get a name per biome). */
  mapName: string;
  biome: Biome;
  /** The scenario a generated map is, if not the biome's usual one (playtest 2026-10-14, #25). */
  site?: Site;
  width: number;
  height: number;
  /** Row-major: index = y * width + x. */
  tiles: TileKind[];
  /** Elevation of each tile in storeys (same indexing as `tiles`). */
  elev: number[];
  /** Floor of each tile (procedural maps; missing on hand-made maps and older saves). */
  ground?: GroundKind[];
  /** Clinical finishes stay attached to the building after furniture is destroyed. */
  medical?: MedicalSite[];
  /** The houses, their rooms by use and how worn (playtest 2026-10-18, #15). */
  homes?: HomeSite[];
  /** The supermarkets (playtest 2026-10-20, #18): one on a town's map at most. */
  markets?: MarketSite[];
  /**
   * What the cover of each tile is meant to look like (procedural maps; see
   * decor.ts). Cosmetic and never updated: a tile whose kind has changed since
   * (blown up) is drawn from its kind.
   */
  decor?: Decor[];
  /**
   * Storeys of the building each tile belongs to (its walls and its inside),
   * 0 outside buildings (procedural maps; missing on hand-made maps and older
   * saves, which see as if every building were one storey). In a building of
   * two or more, walls and windows stop sight up to its roof (los.ts).
   */
  storeys?: number[];
  /**
   * The storeys above the ground, inside buildings (docs/FASE6-PLAN.md):
   * `levels[k - 1]` is storey k. Missing on maps without them (older saves,
   * hand-made maps but «la torre»), which see and move as they always did.
   */
  levels?: Level[];
  /** Lit tiles at night: the map's street lights, Bengalas and fires (missing in older saves). */
  lights?: Light[];
  /** What closes the map round what is played (docs/MAPAS.md §5.10); missing on older saves and maps without an outline. */
  frame?: FramePiece[];
  units: Unit[];
  pods: Pod[];
  mission: MissionState;
  turn: number;
  activeTeam: Team;
  concealed: boolean;
  /**
   * Misses in a row by each player's soldiers (bad luck, XCOM 2's aim assist):
   * the next shot gets +10 per miss, up to +20. Missing in older saves.
   */
  missStreak?: [number, number];
  /**
   * The last attack or explosive and whose it was (Fase 9, skills.ts): who
   * gets the kill or the blast that `died` and `damaged` count for. `landed`:
   * the explosive already hit someone.
   */
  blow?: { unit: string; kind: 'gun' | 'blade' | 'blast'; landed?: boolean };
  ready: [boolean, boolean];
  /**
   * Who has the command (el mando) in the squad's turn: only that player gives
   * orders, and passes it to the partner when they choose. Missing in missions
   * saved before it existed: the first player to act takes it.
   */
  command?: Slot;
  /** The soldier in play (playtest 2026-10-15, #9): the holder finishes it before another; once it's done, the partner plays one of theirs. */
  activeUnit?: string;
  /** A player asked to evacuate from the bus and waits for the partner's yes (docs/PRUEBA-2026-10-14.md P1). */
  evacCall?: { by: Slot; unit: string };
  /** A player asked to abandon the mission and waits for the partner's yes (playtest 16/10). */
  abandonCall?: { by: Slot };
  outcome: Outcome | null;
  outcomeReason: EndReason | null;
  /**
   * Set when the mission belongs to a campaign (the client offers "back to base"). `story` is its
   * chapter in lore.ts STORY; `kicker` titles the arrival briefing and `item` names what a recovery
   * carries out (story chapters and side quests).
   */
  /** `briefing`: the operation's briefing, for the mission's intro card (playtest 13/10: what the mission is for). */
  campaignMission: { id: string; name: string; story?: number; kicker?: string; item?: string; briefing?: string } | null;
  smoke: SmokeCloud[];
  /** La Lata's vehicle (Arturo, 18/10 #45): 0 todoterreno, 1 furgoneta, 2 camión, 3 autobús; absent, the bus. What comes to take the squad out. */
  vehicle?: number;
  /** Under snow (playtest 16/10): still winter in the campaign. Missing, only the opening chapter is. */
  snow?: boolean;
  /** Tiles already searched, by node (grid.ts nodeOf; on the ground, y * width + x); missing before searching existed. */
  searched?: number[];
  /** Rooms the squad has searched, by id (loot.ts roomAt; docs/FASE8.md §B). */
  roomsSearched?: number[];
  /**
   * Things left on the floor (docs/FASE8.md §C): dropped, found with a full backpack, or what a fallen soldier
   * carried (Arturo, 18/10 #49: «la mochila de ‹nombre›», Lore), `from` that soldier (its unit id).
   */
  dropped?: { id: string; pos: Pos; loot: Loot; from?: string }[];
  /** How many things have been left on the floor so far: the next one's id. */
  droppedCount?: number;
  /**
   * In a siege (docs/FASE12.md §4), what each door and barricade still holds,
   * by node (grid.ts nodeOf); 0 once it gave way, to blows or a blast: the
   * breaches stay known through a reload.
   */
  breakables?: Record<number, number>;
  /** The campaign's base under siege (docs/FASE12.md §11): its pieces as they stood when it began, and its stumps (base.ts). */
  base?: { pieces: BasePiece[]; felled?: number[]; level?: number };
  /** The base's traps set (docs/FASE12.md §11.4), with their uses left; spent, 0. */
  traps?: Trap[];
  /** Tiles with barbed wire, by node: costlier to cross. */
  slow?: number[];
}
