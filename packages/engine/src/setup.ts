import { defaultAppearance } from './appearance';
import { AP_PER_TURN, apPerTurn, clipOf, NIGHT, PISTOL_CLIP, RECOVERY_TURNS, SOLDIER_NAMES, SPARE_MAGS, SQUAD_SIZE, TEMPLATES, WEAPONS, ZERO_MODS, type WeaponId } from './content';
import { ground, toPos } from './grid';
import { lightTiles } from './los';
import { generateLayout, handmadeLayout, type MissionLayout } from './mapgen';
import { MAPS } from './maps';
import { STORY } from './lore';
import { repairStairAccess } from './stairAccess';
import type { Rng } from './rng';
import { fighterStats, missionLines, recruitSkills, veteranSkills, type FighterStats, type MissionLines } from './skills';
import type {
  Alarm,
  Appearance,
  Arms,
  Biome,
  ChargeId,
  ClassId,
  FoundWeaponId,
  GameState,
  ItemId,
  MissionKind,
  MissionState,
  PerkId,
  Pod,
  Reinforcement,
  Site,
  Slot,
  Team,
  TemplateId,
  Unit,
  UnitMods,
  Pos,
  Vec2,
} from './types';

/** A campaign soldier going into a mission, with everything already computed. */
export interface SquadMember {
  campaignId: string;
  name: string;
  nickname?: string;
  appearance?: Appearance;
  weaponTier?: number;
  armorTier?: number;
  /** A found weapon's name, when the soldier carries one (docs/FASE4.md). */
  weaponName?: string;
  owner: Slot;
  /** The class, or 'rookie' for a recruit with no class yet. */
  template: TemplateId;
  rank: number;
  perks: PerkId[];
  mods: UnitMods;
  maxHp: number;
  hp: number;
  charges: Partial<Record<ChargeId, number>>;
  will: number;
  armor: number;
  gear: ItemId[];
  /** Backpack slots, when not BACKPACK_SLOTS (the Chapistería's one more, docs/FASE8.md §C). */
  packSlots?: number;
  /** Its own weapons (docs/FASE8.md §A); absent, its class's. */
  arms?: Arms;
  /** Spare magazines it sets out with (the Taller's munition); absent, SPARE_MAGS. */
  mags?: number;
  /** Its experience lines, to fill out there (docs/FASE10.md §2). */
  lines?: MissionLines;
}

/** Hand-made map by id, or a procedural one from a biome and a seed. */
/** `site`: a scenario other than the biome's usual one (playtest 2026-10-14, #25). */
export type MapSource = { kind: 'handmade'; id: string } | { kind: 'generated'; biome: Biome; seed: number; site?: Site; medical?: 'clinic' | 'ward' | 'hospital' };

export interface MissionOptions {
  map: MapSource;
  /** Seats taking part; soldiers are split evenly between them. */
  slots: Slot[];
  rng: Rng;
  kind?: MissionKind;
  squadSize?: number;
  /** Campaign squad; when absent a default squad is generated (skirmish). */
  squad?: SquadMember[];
  /** Alien pods in order; defaults to the map's pods or a plan for the difficulty. */
  pods?: TemplateId[][];
  /** 1 (easy) to 4 (hard): default pods and reinforcements. */
  difficulty?: number;
  /** Objective timer override. */
  turns?: number;
  /** Added to every hack attempt (campaign research). */
  hackBonus?: number;
  /** Skirmish: soldiers start as sergeants to captains with random abilities. */
  veterans?: boolean;
  campaignMission?: GameState['campaignMission'];
  /** Rescue: who waits at the objective (story chapters, side quests); a random name otherwise. */
  vipName?: string;
  /** A sabotage's alarm (playtest 2026-10-15, #14), and what the objective is called. */
  alarm?: Alarm;
  objectiveName?: string;
  /** Names the first enemy of this kind (a side quest's leader). */
  leader?: { template: TemplateId; name: string };
  /** A night mission (docs/ALTURA-Y-NOCHE.md): one more group of the dead, and the living see less. */
  night?: boolean;
  /** The living hold it (docs/ZOMBIS-PRIMERO.md): their bands and their garrison; without it, the dead's. */
  living?: boolean;
  /** A recovery whose story puts the item outdoors (playtest 2026-10-14, #27). */
  itemOutdoors?: boolean;
  /** Medicines also stop a bite's contagion, for everyone (docs/FASE8.md §C). */
  medicineCures?: boolean;
  /** The campaign's Bastion has a Cuarentena (#13): false, the bitten can't board La Lata. */
  quarantine?: boolean;
  /** Still winter in the campaign (campaign.ts wintry): the map under snow. */
  snow?: boolean;
}

/** What a skirmish soldier carries in the hand (docs/FASE9.md §6): one of these, at random. */
const SKIRMISH_ARMS: FoundWeaponId[] = ['assaultRifle', 'pumpShotgun', 'precisionRifle', 'machineGun'];
/** And on the belt: grenades, and one of these. */
const SKIRMISH_BELT: ItemId[] = ['firstAid', 'smokeGrenade', 'flashbang'];

/** A skirmish soldier (docs/FASE10.md §7): a weapon and gear at random; a veteran, bars filled in every line and its board. */
function skirmishSoldier(rng: Rng, veteran: boolean): { stats: FighterStats; arms: Arms; gear: ItemId[]; lines: MissionLines } {
  const arms: Arms = { primary: rng.pick(SKIRMISH_ARMS), melee: rng.next() < 0.5 ? 'machete' : null };
  const gear: ItemId[] = ['fragGrenade', rng.pick(SKIRMISH_BELT)];
  const { skills, growth, board } = veteran ? veteranSkills(rng) : { ...recruitSkills(rng), board: [] };
  return { stats: fighterStats(skills, growth, board, gear, arms), arms, gear, lines: missionLines(skills, growth) };
}

/** Whether a unit counts its spare magazines (docs/FASE7.md): the squad's, with a weapon that takes them. */
function countsMags(team: Team, weapon: WeaponId): boolean {
  return team === 'xcom' && WEAPONS[weapon].clip > 0;
}

/** A soldier's own weapons, loaded, and its spare magazines (docs/FASE8.md §A). */
function armed(u: Unit, m: Pick<SquadMember, 'arms' | 'mags'>): Partial<Unit> {
  if (!m.arms) return m.mags !== undefined && u.mags !== undefined ? { mags: m.mags } : {};
  const arms: Arms = { ...m.arms };
  const unit = { ...u, arms };
  return {
    arms,
    ammo: clipOf(unit, 'shoot'),
    ...(arms.primary ? { pistolAmmo: PISTOL_CLIP } : {}),
    mags: m.mags ?? 0,
  };
}

/** A unit of a kind, on `pos` (on the ground unless it says its level). */
export function makeUnit(id: string, template: TemplateId, name: string, owner: Slot | null, pos: Vec2 & { z?: number }, podId: string | null): Unit {
  const t = TEMPLATES[template];
  return {
    id,
    team: t.team,
    template,
    name,
    owner,
    pos: toPos(pos),
    hp: t.hp,
    maxHp: t.hp,
    armor: t.armor,
    ap: t.team === 'xcom' ? AP_PER_TURN : 0,
    ammo: WEAPONS[t.weapon].clip,
    ...(countsMags(t.team, t.weapon) ? { mags: SPARE_MAGS } : {}),
    charges: { ...t.charges },
    overwatch: false,
    hunkered: false,
    aided: 0,
    stunned: false,
    alive: true,
    evacuated: false,
    podId,
    rank: 0,
    perks: [],
    mods: { ...ZERO_MODS },
    kills: 0,
    campaignId: null,
    objective: false,
    captive: false,
    will: t.will,
    cooldowns: {},
    killZone: false,
    reactedTo: [],
    suppressing: null,
    suppressedBy: null,
    marked: 0,
    ruptured: 0,
    disoriented: 0,
    panicked: false,
    controlledBy: null,
    controlTurns: 0,
    reanimated: false,
    refunded: false,
    gear: [],
  };
}

export const MISSION_NAMES: Record<MissionKind, string> = {
  elimination: 'Limpieza',
  recovery: 'Botín',
  sabotage: 'Sabotaje',
  hack: 'Arreglar la radio',
  rescue: 'Rescate',
  scavenge: 'Saqueo',
};

/** How a night mission is labelled (placeholder for Lore). */
export const NIGHT_LABEL = 'De noche';

/** The extra group of the dead a night mission starts with (NIGHT in content.ts). */
export function nightPod(difficulty: number): TemplateId[] {
  // Twice as many as before the playtest of 2026-10-14 (#26): the night is theirs.
  return difficulty >= 3 ? ['zombie', 'zombie', 'zombie', 'zombie', 'xenoid', 'xenoid'] : ['zombie', 'zombie', 'zombie', 'zombie'];
}

export const BIOME_NAMES: Record<Biome, string> = {
  city: 'Ciudad',
  wilds: 'Afueras',
  facility: 'Fábrica',
};

/** XCOM turns to secure each objective, tuned on 36×36 maps (see `missionTurns`). */
export const MISSION_TURNS: Partial<Record<MissionKind, number>> = {
  recovery: RECOVERY_TURNS,
  sabotage: 10,
  hack: 10,
  rescue: 14,
};

/**
 * Turns to secure the objective on a map `size` tiles across: on bigger maps
 * the objective lies further away, so every 6 tiles beyond 36 add a turn, half
 * as much again when the data or the VIP has to be brought back to the evac.
 */
export function missionTurns(kind: MissionKind, size: number): number | null {
  const base = MISSION_TURNS[kind];
  if (base === undefined) return null;
  const extra = Math.max(0, (size - 36) / 6);
  return base + Math.round(kind === 'recovery' || kind === 'rescue' ? extra * 1.5 : extra);
}

/**
 * The dead by difficulty for procedural maps (docs/ZOMBIS-PRIMERO.md): every
 * mission is the dead's, but those where the living hold something
 * (livingPlan). Three times as many as at first (playtest 2026-10-14, #26:
 * going into town has to be a risk): about 20 at difficulty 1 and 45 at 4, in
 * groups of three to five spread over the map, so the streets are full of them.
 */
export function podPlan(difficulty: number): TemplateId[][] {
  const z = 'zombie' as const;
  switch (Math.max(1, Math.min(4, difficulty))) {
    case 1:
      return [[z, z, z], [z, z, z], [z, z, z, 'bloater'], [z, z, z], [z, z, z, z], [z, z, z]];
    case 2:
      return [[z, z, 'xenoid'], ['sectoid', z, z], [z, z, z, 'bloater'], [z, 'xenoid', z, z], [z, z, z], [z, z, z, z], [z, 'xenoid', z], [z, z, z, 'bloater']];
    case 3:
      return [
        [z, z, z, 'xenoid'],
        ['mec', z, z, z],
        ['sectoid', z, 'xenoid', z],
        [z, z, z, 'bloater'],
        [z, z, 'xenoid', z],
        [z, z, z, z],
        ['mec', z, z],
        [z, z, z, 'bloater'],
        [z, 'xenoid', z, z],
      ];
    default:
      return [
        ['mec', z, z, z, z],
        ['sectoid', 'xenoid', z, z],
        ['xenoid', 'xenoid', z, z],
        ['bloater', z, z, z, z],
        ['mec', 'xenoid', z, z],
        [z, z, z, z, z],
        ['sectoid', z, z, z],
        ['bloater', z, z, z, 'xenoid'],
        ['mec', z, z, z],
        [z, z, 'xenoid', z, z],
      ];
  }
}

/**
 * Bands of the living (the Cuervos, the Congregación) by difficulty: only in
 * the missions that are theirs (docs/ZOMBIS-PRIMERO.md), two of them with two
 * groups of the dead about (missionPods).
 */
export function livingPlan(difficulty: number): TemplateId[][] {
  switch (Math.max(1, Math.min(4, difficulty))) {
    case 1:
      return [['trooper', 'trooper'], ['trooper', 'officer'], ['trooper', 'trooper', 'lancer']];
    case 2:
      return [['trooper', 'lancer'], ['trooper', 'officer'], ['trooper', 'trooper', 'lancer']];
    case 3:
      return [['officer', 'lancer'], ['officer', 'trooper', 'trooper'], ['lancer', 'trooper', 'trooper']];
    default:
      return [['officer', 'lancer', 'trooper'], ['officer', 'trooper', 'trooper'], ['lancer', 'officer']];
  }
}

/**
 * The enemy groups of a procedural mission (docs/ZOMBIS-PRIMERO.md): a
 * Saqueo's dead scattered about; a mission of the living, two of their bands
 * and two groups of the dead; any other, the dead.
 */
export function missionPods(difficulty: number, kind: MissionKind, living = false): TemplateId[][] {
  if (kind === 'scavenge') return scavengePlan(difficulty);
  return living ? [...livingPlan(difficulty).slice(0, 2), ...podPlan(difficulty).slice(0, 2)] : podPlan(difficulty);
}

/**
 * Saqueo: the dead scattered about the town in small groups, unaware. The
 * horde comes later, by the edges (HORDE_WAVES).
 */
export function scavengePlan(difficulty: number): TemplateId[][] {
  // Three times as many as at first (playtest 2026-10-14, #26), in small groups all over the town.
  const z = 'zombie' as const;
  switch (Math.max(1, Math.min(4, difficulty))) {
    case 1:
      return [[z, z], [z, z], [z, z, z], ['xenoid', z], [z, 'bloater', z], [z, z], [z, z, z], [z, z], [z, z, z], [z, 'xenoid']];
    case 2:
      return [[z, z, z], [z, 'xenoid', z], [z, z, 'bloater'], ['sectoid', z, z], [z, z, z], [z, z], [z, 'xenoid', z], [z, z, z, 'bloater'], [z, z, z], [z, z, z]];
    case 3:
      return [
        [z, z, 'xenoid'],
        ['mec', z, z],
        [z, z, 'bloater'],
        ['sectoid', z, z, z],
        ['xenoid', z, z],
        [z, z, z, z],
        [z, 'xenoid', z],
        [z, z, z, 'bloater'],
        ['mec', z, z],
        [z, z, z, z],
      ];
    default:
      return [
        [z, z, z, 'xenoid'],
        ['mec', z, z, z],
        ['bloater', z, z, z],
        ['sectoid', z, 'xenoid', z],
        ['mec', 'xenoid', z],
        [z, z, z, 'bloater'],
        ['xenoid', z, z, z],
        [z, z, z, z, z],
        ['sectoid', z, z],
        ['mec', z, z, z],
        [z, 'xenoid', z, z],
      ];
  }
}

/** Who comes in each wave of a horde, by difficulty: the Saqueo's (rules.ts callHorde), sabotage and hack's, and a failed hack's alarm. */
/** Twice what they were (playtest 2026-10-14, #26): with three times the dead on the map, a horde is a horde. */
const HORDE_WAVES: Record<number, TemplateId[]> = {
  1: ['zombie', 'zombie', 'zombie', 'zombie'],
  2: ['zombie', 'zombie', 'zombie', 'zombie', 'xenoid', 'xenoid'],
  3: ['zombie', 'zombie', 'zombie', 'zombie', 'xenoid', 'xenoid', 'bloater', 'zombie'],
  4: ['zombie', 'zombie', 'zombie', 'zombie', 'zombie', 'zombie', 'xenoid', 'xenoid', 'mec', 'bloater'],
};

export function hordeWave(difficulty: number): TemplateId[] {
  return [...HORDE_WAVES[Math.max(1, Math.min(4, difficulty))]!];
}

/**
 * Nobody drops in out of nowhere (playtest 2026-10-13): one more group on the
 * map from the start, in the missions that had one (elimination from
 * difficulty 3 on, and every mission with an objective but the Saqueo). The
 * living's when the mission is theirs, the dead's round the objective
 * otherwise (docs/ZOMBIS-PRIMERO.md).
 */
const GARRISON: Record<number, TemplateId[]> = {
  1: ['trooper', 'trooper'],
  2: ['trooper', 'lancer'],
  3: ['officer', 'trooper', 'lancer'],
  4: ['officer', 'lancer', 'trooper'],
};
const DEAD_GARRISON: Record<number, TemplateId[]> = {
  1: ['zombie', 'zombie', 'zombie'],
  2: ['zombie', 'zombie', 'xenoid'],
  3: ['zombie', 'zombie', 'xenoid', 'bloater'],
  4: ['zombie', 'zombie', 'mec', 'xenoid'],
};

export function garrison(kind: MissionKind, difficulty: number, living = false): TemplateId[] | null {
  if (kind === 'scavenge' || (kind === 'elimination' && difficulty < 3)) return null;
  return [...(living ? GARRISON : DEAD_GARRISON)[Math.max(1, Math.min(4, difficulty))]!];
}

/** The pressure on the clock of sabotage and hack: zombies drawn in by the edges, at the end of these enemy turns. */
function hordeTurns(kind: MissionKind): number[] {
  return kind === 'sabotage' || kind === 'hack' ? [4, 8] : [];
}

/** Who a rescue frees, when it isn't a story's or a quest's (their sex is in lore.ts NAMED_SEX). */
/** Who an ordinary rescue is for; the campaign picks one when it offers the operation (MissionOffer.vip). */
export const VIP_NAMES = ['Dra. Irina Sokolova', 'Ing. Kenji Mori', 'Alcaldesa Mara Velasco', 'Dr. Femi Adebayo', 'Agente Claire Dubois'];

/**
 * The battlefield a kind asks the generator for, when it isn't its own: a
 * Saqueo is laid out like a recovery (houses, shops and offices toward the
 * evac), and the recovery's item is never placed.
 */
const LAYOUT_KIND: Partial<Record<MissionKind, MissionKind>> = { scavenge: 'recovery' };

export function missionLayout(map: MapSource, kind: MissionKind, podSizes: number[], itemOutdoors = false, clinic = false, hospital = false, alarm?: Alarm): MissionLayout {
  if (map.kind === 'handmade') {
    const def = MAPS[map.id];
    if (!def) throw new Error(`Unknown map ${map.id}`);
    return handmadeLayout(def, kind);
  }
  return generateLayout({ biome: map.biome, kind: LAYOUT_KIND[kind] ?? kind, podSizes, seed: map.seed, ...(map.site ? { site: map.site } : {}), ...(map.medical ? { medical: map.medical } : {}), ...(itemOutdoors ? { itemOutdoors } : {}), ...(clinic ? { clinic } : {}), ...(hospital ? { hospital } : {}), ...(alarm ? { alarm } : {}) });
}

export function createMission(opts: MissionOptions): GameState {
  if (!opts.slots.length) throw new Error('A mission needs at least one player');
  const difficulty = opts.difficulty ?? 2;
  const handmade = opts.map.kind === 'handmade' ? MAPS[opts.map.id] : undefined;
  let kind = opts.kind ?? 'elimination';
  const base = opts.pods ?? (handmade ? Object.keys(handmade.pods).sort().map((k) => handmade.pods[k]!) : missionPods(difficulty, kind, opts.living));
  // The garrison from the start (the living's, if it is theirs); at night, the dead out in force too (a hand-made map has no room for either).
  const extra = garrison(kind, difficulty, opts.living);
  const plan = [...base, ...(extra ? [extra] : []), ...(opts.night ? [nightPod(difficulty)] : [])];
  const layout = missionLayout(opts.map, kind, plan.map((p) => p.length), opts.itemOutdoors, opts.campaignMission?.story === 1, opts.campaignMission?.story === 2, opts.alarm);
  // A map that can't host the objective falls back to elimination.
  const objectiveMissing =
    (kind === 'recovery' && !layout.item) ||
    (kind === 'hack' && !layout.terminal) ||
    ((kind === 'sabotage' || kind === 'rescue') && !layout.objectiveSpot) ||
    (kind !== 'elimination' && !layout.evacZone.length);
  if (objectiveMissing) kind = 'elimination';
  const squadSize = Math.min(opts.squadSize ?? SQUAD_SIZE, layout.spawns.length);

  const names = [...SOLDIER_NAMES];
  for (let i = names.length - 1; i > 0; i--) {
    const j = opts.rng.int(i + 1);
    [names[i], names[j]] = [names[j]!, names[i]!];
  }

  const units: Unit[] = [];
  if (opts.squad) {
    opts.squad.slice(0, layout.spawns.length).forEach((m, i) => {
      const u = makeUnit(`s${i + 1}`, m.template, m.name, m.owner, layout.spawns[i]!, null);
      units.push({
        ...u,
        ap: apPerTurn({ team: u.team, rank: m.rank }),
        rank: m.rank,
        perks: [...m.perks],
        mods: { ...m.mods },
        maxHp: m.maxHp,
        hp: m.hp,
        charges: { ...m.charges },
        campaignId: m.campaignId,
        nickname: m.nickname,
        appearance: m.appearance ? { ...m.appearance } : undefined,
        weaponTier: m.weaponTier,
        armorTier: m.armorTier,
        ...(m.weaponName ? { weaponName: m.weaponName } : {}),
        will: m.will,
        armor: m.armor,
        ...(m.packSlots ? { packSlots: m.packSlots } : {}),
        ...(m.lines ? { lines: missionLines(m.lines.start, m.lines.growth, m.lines.bonus) } : {}),
        ...armed(u, m),
        gear: [...m.gear],
      });
    });
  } else {
    const perPlayer = Math.ceil(squadSize / opts.slots.length);
    for (let i = 0; i < squadSize; i++) {
      const owner = opts.slots[Math.min(Math.floor(i / perPlayer), opts.slots.length - 1)]!;
      const { name, sex } = names[i]!;
      const u = makeUnit(`s${i + 1}`, 'rookie', name, owner, layout.spawns[i]!, null);
      const { stats, arms, gear, lines } = skirmishSoldier(opts.rng, !!opts.veterans);
      const unit: Unit = {
        ...u,
        ap: apPerTurn({ team: u.team, rank: stats.rank }),
        rank: stats.rank,
        perks: stats.perks,
        mods: stats.mods,
        maxHp: stats.maxHp,
        hp: stats.maxHp,
        charges: stats.charges,
        will: stats.will,
        armor: stats.armor,
        gear,
        appearance: defaultAppearance(owner, undefined, sex),
        lines,
      };
      units.push({ ...unit, ...armed(unit, { arms, mags: SPARE_MAGS }) });
    }
  }

  let objectiveUnit: string | null = null;
  if (kind === 'sabotage' && layout.objectiveSpot) {
    units.push({ ...makeUnit('relay', 'relay', opts.objectiveName ?? TEMPLATES.relay.name, null, layout.objectiveSpot, null), objective: true });
    objectiveUnit = 'relay';
  }
  if (kind === 'rescue' && layout.objectiveSpot) {
    units.push({ ...makeUnit('vip', 'vip', opts.vipName ?? opts.rng.pick(VIP_NAMES), null, layout.objectiveSpot, null), captive: true, ap: 0 });
    objectiveUnit = 'vip';
  }

  const pods: Pod[] = [];
  let alienIndex = 0;
  // From difficulty 2, one group of the living posted upstairs, where the map has room for it (decision 7 in docs/FASE6-PLAN.md).
  const perch = layout.perch ?? [];
  const perched = difficulty >= 2 ? plan.findIndex((members) => members.length > 0 && members.length <= perch.length && members.every((t) => !TEMPLATES[t].undead)) : -1;
  plan.forEach((members, i) => {
    const spot = i === perched ? { tiles: perch, patrol: [] } : layout.pods[i];
    if (!spot) return;
    const pod: Pod = { id: `p${i + 1}`, active: false, unitIds: [], patrol: spot.patrol.map(toPos), patrolIndex: 0 };
    members.slice(0, spot.tiles.length).forEach((template, k) => {
      const id = `a${++alienIndex}`;
      units.push(makeUnit(id, template, TEMPLATES[template].name, null, spot.tiles[k]!, pod.id));
      pod.unitIds.push(id);
    });
    if (pod.unitIds.length) pods.push(pod);
  });
  const leader = opts.leader && units.find((u) => u.team === 'alien' && u.template === opts.leader!.template);
  if (leader) leader.name = opts.leader!.name;

  const reinforcements: Reinforcement[] = hordeTurns(kind).map((turn, i) => ({ id: `r${i + 1}`, turn, pos: null, units: hordeWave(difficulty), landed: false, horde: true }));

  const mission: MissionState = {
    kind,
    turnsLeft: kind === 'elimination' ? null : opts.turns ?? missionTurns(kind, Math.max(layout.width, layout.height)),
    item: kind === 'recovery' && layout.item ? { pos: toPos(layout.item), carrier: null, evacuated: false } : null,
    evacZone: kind === 'elimination' ? [] : layout.evacZone,
    ...(kind !== 'elimination' && layout.evacBus ? { evacBus: layout.evacBus } : {}),
    terminal: kind === 'hack' && layout.terminal ? toPos(layout.terminal) : null,
    objectiveUnit,
    objectiveDone: false,
    hackBonus: opts.hackBonus ?? 0,
    ...(opts.medicineCures ? { medicineCures: true } : {}),
    ...(opts.alarm ? { alarm: opts.alarm } : {}),
    ...(opts.quarantine !== undefined ? { quarantine: opts.quarantine } : {}),
    reinforcements,
    difficulty,
    // The dead keep coming by the edges on every mission you leave by bus (playtest 2026-10-15, #11).
    ...(kind !== 'elimination' ? { horde: hordeWave(difficulty) } : {}),
    night: !!opts.night,
  };

  const tiles = [...layout.tiles];
  // The terminal only exists on hack missions (hand-made maps add it on demand).
  if (kind !== 'hack' && layout.terminal) tiles[layout.terminal.y * layout.width + layout.terminal.x] = 'floor';

  const state: GameState = {
    seq: 0,
    mapId: layout.id,
    // In the campaign the map is called as the operation (playtest 2026-10-15, #20): one name for the place everywhere.
    mapName: opts.campaignMission?.name ?? layout.name,
    biome: layout.biome,
    ...(layout.site ? { site: layout.site } : {}),
    width: layout.width,
    height: layout.height,
    tiles,
    elev: [...layout.elev],
    ...(layout.ground ? { ground: [...layout.ground] } : {}),
    ...(layout.medical ? { medical: layout.medical.map((b) => ({ ...b, ...(b.rooms ? { rooms: b.rooms.map((r) => ({ ...r })) } : {}), ...(b.dressing ? { dressing: b.dressing.map((d) => ({ ...d })) } : {}) })) } : {}),
    ...(layout.decor ? { decor: [...layout.decor] } : {}),
    ...(layout.storeys ? { storeys: [...layout.storeys] } : {}),
    ...(layout.levels ? { levels: layout.levels.map((l) => ({ tiles: [...l.tiles], ...(l.decor ? { decor: [...l.decor] } : {}) })) } : {}),
    lights: [],
    units,
    pods,
    mission,
    turn: 1,
    activeTeam: 'xcom',
    concealed: true,
    ready: [false, false],
    command: opts.slots[0] ?? 0,
    outcome: null,
    outcomeReason: null,
    campaignMission: opts.campaignMission ?? null,
    ...(opts.snow ? { snow: true } : {}),
    smoke: [],
  };
  // The map's street lights (they never go out): each lights what its lamp sees round it, worked out once.
  state.lights = (layout.lights ?? []).map((tile, i) => ({
    id: `l${i + 1}`,
    tile: ground(tile),
    radius: NIGHT.streetLight,
    tiles: lightTiles(state, ground(tile), NIGHT.streetLight, NIGHT.lampHeight),
  }));
  return state;
}

/**
 * A mission saved before buildings had storeys (docs/FASE6-PLAN.md): every
 * place in it gets its level, the ground. Harmless on a mission that has them.
 */
export function normalizeMission(s: GameState): GameState {
  // An opening mission already in progress keeps its state, with the approved presentation.
  if (s.campaignMission?.story === 1) {
    s.campaignMission.name = STORY[0]!.title;
    s.campaignMission.briefing = STORY[0]!.briefing;
    delete s.campaignMission.kicker;
  }
  const fix = <T extends Vec2>(p: T): T & Pos => Object.assign(p, { z: (p as Partial<Pos>).z ?? 0 });
  for (const u of s.units) {
    fix(u.pos);
    if (u.heard) fix(u.heard);
    // A mission saved before magazines were counted (docs/FASE7.md): the squad starts with its spares.
    if (u.mags === undefined && countsMags(u.team, TEMPLATES[u.template].weapon)) u.mags = SPARE_MAGS;
  }
  for (const p of s.pods) p.patrol.forEach(fix);
  const m = s.mission;
  if (m.item?.pos) fix(m.item.pos);
  if (m.terminal) fix(m.terminal);
  for (const r of m.reinforcements) if (r.pos) fix(r.pos);
  for (const c of s.smoke) fix(c.pos);
  for (const z of s.silences ?? []) fix(z.pos);
  for (const l of s.lights ?? []) fix(l.tile);
  repairStairAccess(s);
  return s;
}
