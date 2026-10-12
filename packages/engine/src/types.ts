export interface Vec2 {
  x: number;
  y: number;
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
 * ladder:       walkable floor that also lets units climb to an orthogonal neighbour one storey up.
 * console:      objective terminal — indestructible full-cover object.
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
  | 'console';

/** Visual and generation theme of a map. */
export type Biome = 'city' | 'wilds' | 'facility';

/** 0 = none, 1 = half (low), 2 = full (high). */
export type CoverLevel = 0 | 1 | 2;

export type ClassId = 'assault' | 'grenadier' | 'sharpshooter' | 'specialist';
export type AlienId = 'trooper' | 'officer' | 'xenoid' | 'lancer' | 'mec' | 'sectoid' | 'zombie';
/** Mission objectives that are units: the alien relay to destroy and the VIP to extract. */
export type ObjectiveId = 'relay' | 'vip';
/** 'rookie': a recruit with no class yet (classes.ts), a rifle and the basic actions. */
export type TemplateId = ClassId | 'rookie' | AlienId | ObjectiveId;

/** Head pieces a soldier can wear (barracks customization). */
export type HeadId = 'helmet' | 'hood' | 'beret' | 'cap' | 'mask' | 'bare';
/** Body builds. */
export type BuildId = 'slim' | 'normal' | 'heavy';

/** How a soldier looks; colours are `#rrggbb`. Purely cosmetic. */
export interface Appearance {
  /** Main armour colour. */
  primary: string;
  /** Plates, trim and cloth. */
  secondary: string;
  /** Visor and lens glow. */
  visor: string;
  head: HeadId;
  build: BuildId;
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
  | 'medkit'
  | 'aid'
  | 'evac'
  | 'hack'
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
  | 'combatProtocol'
  | 'revival'
  | 'restoration'
  | 'discharge'
  // Alien powers.
  | 'claw'
  | 'mindspin'
  | 'reanimate'
  // Granted by equipment.
  | 'smoke'
  | 'flashbang'
  | 'firstAid'
  // Velmar's Canto (the Cámara de Ecos): songs, `song: true` in ABILITIES.
  | 'aegis'
  | 'echoStep'
  | 'layOnHands'
  | 'velmarOath'
  | 'bolt'
  | 'silence'
  | 'fireball'
  | 'velmarStorm'
  | 'huntersMark'
  | 'spectralArrow'
  | 'shadowStep'
  | 'lightRain'
  | 'counterSong'
  | 'hymn'
  | 'lastVerse';

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
  | 'faceoff'
  | 'spectralArrow'
  | 'lightRain';

/** Area abilities thrown or fired at a tile. */
export type BlastAbility = 'grenade' | 'launch' | 'missiles' | 'discharge' | 'smoke' | 'flashbang' | 'echoStep' | 'shadowStep' | 'silence' | 'fireball';

/** Limited-use items. */
export type ChargeId =
  | 'grenade'
  | 'medkit'
  | 'missiles'
  | 'combatProtocol'
  | 'smoke'
  | 'flashbang'
  | 'firstAid'
  // Songs with uses: named after their ability.
  | 'layOnHands'
  | 'velmarOath'
  | 'silence'
  | 'fireball'
  | 'velmarStorm'
  | 'lightRain'
  | 'lastVerse';

/** Equipment fabricated in the base: one utility item and one ammo type per soldier. */
export type ItemId = 'fragGrenade' | 'smokeGrenade' | 'flashbang' | 'firstAid' | 'nanoVest' | 'mindShield' | 'apRounds' | 'tracerRounds';
export type ItemSlot = 'utility' | 'ammo';

/** What a successful Mindspin does to its victim. */
export type PsiEffect = 'disoriented' | 'panicked' | 'controlled';

export type ShotResult = 'miss' | 'graze' | 'hit' | 'crit';

export type Outcome = 'victory' | 'defeat';
export type EndReason = 'objective' | 'wiped' | 'timer' | 'abandoned' | 'objectiveLost';

/**
 * elimination: kill every hostile.
 * recovery:    pick up the data and extract it.
 * sabotage:    destroy the alien relay before the timer runs out.
 * hack:        hack the terminal before the timer runs out (failures raise the alarm).
 * rescue:      reach the VIP and extract them alive.
 */
export type MissionKind = 'elimination' | 'recovery' | 'sabotage' | 'hack' | 'rescue';

/** Talents of the Rosa (classes.ts TALENTS); the first ten of each class are the original perks. */
export type PerkId =
  // Centre: open to every soldier
  | 'drill'
  | 'fitness'
  | 'coolHead'
  | 'spareGrenade'
  // Canto (Velmar), one branch per class
  | 'songBlade'
  | 'aegis'
  | 'echoStep'
  | 'layOnHands'
  | 'velmarOath'
  | 'bolt'
  | 'silence'
  | 'fireball'
  | 'highFlame'
  | 'velmarStorm'
  | 'huntersMark'
  | 'spectralArrow'
  | 'shadowStep'
  | 'windEyes'
  | 'lightRain'
  | 'calmSong'
  | 'counterSong'
  | 'hymn'
  | 'lightHands'
  | 'lastVerse'
  // Assault
  | 'pointBlank'
  | 'keenBlade'
  | 'shadowstep'
  | 'runner'
  | 'runAndGun'
  | 'implacable'
  | 'rapidFire'
  | 'toughSkin'
  | 'bladeMaster'
  | 'hunter'
  | 'steadyHand'
  | 'bladeCrit'
  | 'instinct'
  | 'slug'
  | 'sideBySide'
  | 'buckshot'
  | 'relentless'
  | 'closeKill'
  | 'light'
  | 'feline'
  | 'evasion'
  | 'scoutEye'
  | 'untouchable'
  // Grenadier
  | 'demolition'
  | 'heavyFire'
  | 'shredder'
  | 'bigBore'
  | 'suppression'
  | 'holoTargeting'
  | 'salvo'
  | 'armoured'
  | 'chainShot'
  | 'rupture'
  | 'bandolier'
  | 'shrapnel'
  | 'longArm'
  | 'safeBlast'
  | 'blastWave'
  | 'steadyCannon'
  | 'apCannon'
  | 'heavyCrit'
  | 'tripod'
  | 'marker'
  | 'bulwark'
  | 'suppressor'
  | 'ironNerves'
  | 'dugIn'
  // Sharpshooter
  | 'deadeye'
  | 'gunslinger'
  | 'longWatch'
  | 'quickdraw'
  | 'aimedShot'
  | 'lightningHands'
  | 'deathFromAbove'
  | 'hawkEye'
  | 'faceoff'
  | 'killZone'
  | 'farsight'
  | 'caliber'
  | 'armorRounds'
  | 'serenity'
  | 'magnum'
  | 'dancer'
  | 'holster'
  | 'fineTrigger'
  | 'patience'
  | 'nest'
  | 'crossfire'
  | 'post'
  | 'coldBlood'
  | 'guardCrit'
  // Specialist
  | 'fieldMedic'
  | 'guardian'
  | 'coveringFire'
  | 'combatProtocol'
  | 'threatAssessment'
  | 'revivalProtocol'
  | 'slippery'
  | 'marksman'
  | 'restoration'
  | 'capacitorDischarge'
  | 'bigKit'
  | 'vocation'
  | 'surgeon'
  | 'overcharge'
  | 'battery'
  | 'intrusion'
  | 'tunedRifle'
  | 'bridge'
  | 'calm'
  | 'mutualCover'
  | 'instructor';

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
  protocolDamage: number;
  hack: number;
  /** Against mindspin: 1 = never panics, 2 = can't be controlled either. */
  psiGuard: number;
  // Velmar's Canto (filled in by soldierStats from the Canto talents).
  /** Knows some song (1/0): for «A dos voces». */
  singer: number;
  /** Extra chance a Cantor's song gets against this unit (10 for singers without Temple 10). */
  songOpen: number;
  /** Hoja cantada level 2: the Slash disorients on a hit. */
  slashDisorient: number;
  /** Llama alta: extra damage of Saeta, Bola de fuego and Tormenta. */
  songDamage: number;
  /** Ojos del viento: extra tiles of sight. */
  sight: number;
  /** Ojos del viento: smoke doesn't shield targets from this unit (1/0). */
  smokeSight: number;
  /** Canto de calma level: +10 will per level to allies within 4 tiles. */
  calmAura: number;
}

export interface Unit {
  id: string;
  team: Team;
  template: TemplateId;
  name: string;
  /** Owning player for XCOM soldiers; null for aliens and shared (pool) units. */
  owner: Slot | null;
  pos: Vec2;
  hp: number;
  maxHp: number;
  /** Damage absorbed per hit; explosives shred it. */
  armor: number;
  ap: number;
  ammo: number;
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
  // Velmar's Canto (absent until a song touches the unit).
  /** Juramento de Velmar: turns left (allies around can't panic or be controlled; half damage). */
  oath?: number;
  /** Contracanto: turns of immunity to the Cantor's songs. */
  ward?: number;
  /** Himno and Paso de sombra: extra aim until the unit's team's next turn. */
  blessed?: number;
  /** Marca del cazador: the shooter whose shots get +15 aim and crit against this unit. */
  huntedBy?: string | null;
  /** Turn it fell on (Último verso brings back those who fell this turn). */
  fellTurn?: number;
}

/** Silencio: inside, the Cantores can't sing. */
export interface SilenceZone {
  pos: Vec2;
  radius: number;
  /** XCOM turns it still lasts. */
  turns: number;
}

/** A smoke cloud: units inside are harder to hit. */
export interface SmokeCloud {
  pos: Vec2;
  radius: number;
  /** XCOM turns it still lasts. */
  turns: number;
}

export interface Pod {
  id: string;
  active: boolean;
  unitIds: string[];
  /** Waypoints an inactive pod walks between during the alien turn (empty = it stays put). */
  patrol: Vec2[];
  patrolIndex: number;
}

/** Alien reinforcements: announced by a flare one turn before they drop. */
export interface Reinforcement {
  id: string;
  /** Alien turn at the end of which they land. */
  turn: number;
  /** Landing tile, chosen when the flare goes up; null until announced. */
  pos: Vec2 | null;
  units: TemplateId[];
  landed: boolean;
}

export interface MissionItem {
  /** Where it lies, or null while someone carries it. */
  pos: Vec2 | null;
  carrier: string | null;
  evacuated: boolean;
}

export interface MissionState {
  kind: MissionKind;
  /** XCOM turns left to secure the objective; null when there is no timer. */
  turnsLeft: number | null;
  item: MissionItem | null;
  evacZone: Vec2[];
  /** Hack missions: the terminal (a `console` tile). */
  terminal: Vec2 | null;
  /** Sabotage: the relay unit; rescue: the VIP unit. */
  objectiveUnit: string | null;
  /** Relay destroyed, terminal hacked, data or VIP extracted. */
  objectiveDone: boolean;
  /** Hack bonus from the campaign (research), added to every hack attempt. */
  hackBonus: number;
  reinforcements: Reinforcement[];
}

export interface GameState {
  /** Number of events applied so far. Clients use it to detect gaps. */
  seq: number;
  mapId: string;
  /** Display name of the area (generated maps get a name per biome). */
  mapName: string;
  biome: Biome;
  width: number;
  height: number;
  /** Row-major: index = y * width + x. */
  tiles: TileKind[];
  /** Elevation of each tile in storeys (same indexing as `tiles`). */
  elev: number[];
  units: Unit[];
  pods: Pod[];
  mission: MissionState;
  turn: number;
  activeTeam: Team;
  concealed: boolean;
  ready: [boolean, boolean];
  /**
   * Who has the command (el mando) in the squad's turn: only that player gives
   * orders, and passes it to the partner when they choose. Missing in missions
   * saved before it existed: the first player to act takes it.
   */
  command?: Slot;
  outcome: Outcome | null;
  outcomeReason: EndReason | null;
  /**
   * Set when the mission belongs to a campaign (the client offers "back to base"). `story` is its
   * chapter in lore.ts STORY; `kicker` titles the arrival briefing and `item` names what a recovery
   * carries out (story chapters and side quests).
   */
  campaignMission: { id: string; name: string; story?: number; kicker?: string; item?: string } | null;
  smoke: SmokeCloud[];
  /** Silencio zones (missing in missions saved before the Canto). */
  silences?: SilenceZone[];
  /** The Cámara de Ecos is next to the listening post: songs don't break concealment. */
  quietSongs?: boolean;
}
