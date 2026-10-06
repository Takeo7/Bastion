import {
  addMods,
  AP_PER_TURN,
  ITEMS,
  PERKS,
  perksFor,
  RANK_AIM,
  RANK_HP,
  RANK_WILL,
  RANKS,
  RECOVERY_TURNS,
  SOLDIER_NAMES,
  SQUAD_CLASSES,
  TEMPLATES,
  WEAPONS,
  ZERO_MODS,
  type PerkTier,
} from './content';
import { generateLayout, handmadeLayout, type MissionLayout } from './mapgen';
import { MAPS } from './maps';
import type { Rng } from './rng';
import type {
  Appearance,
  Biome,
  ChargeId,
  ClassId,
  GameState,
  ItemId,
  MissionKind,
  MissionState,
  PerkId,
  Pod,
  Reinforcement,
  Slot,
  TemplateId,
  Unit,
  UnitMods,
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
  owner: Slot;
  template: ClassId;
  rank: number;
  perks: PerkId[];
  mods: UnitMods;
  maxHp: number;
  hp: number;
  charges: Partial<Record<ChargeId, number>>;
  will: number;
  armor: number;
  gear: ItemId[];
}

/** Hand-made map by id, or a procedural one from a biome and a seed. */
export type MapSource = { kind: 'handmade'; id: string } | { kind: 'generated'; biome: Biome; seed: number };

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
  /** Names the first enemy of this kind (a side quest's leader). */
  leader?: { template: TemplateId; name: string };
}

/** What rank, perks and equipment add to a soldier of a class (campaign upgrades come on top). */
export function soldierStats(cls: ClassId, rank: number, perks: PerkId[], gear: ItemId[] = []): Pick<SquadMember, 'mods' | 'maxHp' | 'charges' | 'will'> {
  const t = TEMPLATES[cls];
  let mods = addMods({ ...ZERO_MODS }, { aim: RANK_AIM * rank });
  let maxHp = t.hp;
  for (let r = 1; r <= rank; r++) maxHp += RANK_HP[r] ?? 0;
  const charges = { ...t.charges };
  for (const source of [...perks.map((id) => PERKS[id]), ...gear.map((id) => ITEMS[id])]) {
    if (source.mods) mods = addMods(mods, source.mods);
    maxHp += source.hp ?? 0;
    for (const [c, n] of Object.entries(source.charges ?? {}) as [ChargeId, number][]) charges[c] = (charges[c] ?? 0) + n;
  }
  return { mods, maxHp, charges, will: t.will + RANK_WILL * rank + mods.will };
}

/** A random pick of one perk per tier up to the soldier's rank. */
export function randomPerks(cls: ClassId, rank: number, rng: Rng): PerkId[] {
  const out: PerkId[] = [];
  for (let r = 1; r <= rank; r++) {
    const tier = RANKS[r]?.perkTier;
    if (tier) out.push(rng.pick(perksFor(cls, tier as PerkTier)).id);
  }
  return out;
}

export function makeUnit(id: string, template: TemplateId, name: string, owner: Slot | null, pos: Vec2, podId: string | null): Unit {
  const t = TEMPLATES[template];
  return {
    id,
    team: t.team,
    template,
    name,
    owner,
    pos: { ...pos },
    hp: t.hp,
    maxHp: t.hp,
    armor: t.armor,
    ap: t.team === 'xcom' ? AP_PER_TURN : 0,
    ammo: WEAPONS[t.weapon].clip,
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
  elimination: 'Eliminación',
  recovery: 'Recuperación',
  sabotage: 'Sabotaje',
  hack: 'Pirateo',
  rescue: 'Rescate',
};

export const BIOME_NAMES: Record<Biome, string> = {
  city: 'Ciudad',
  wilds: 'Afueras',
  facility: 'Imprenta',
};

/** XCOM turns to secure each objective. */
export const MISSION_TURNS: Partial<Record<MissionKind, number>> = {
  recovery: RECOVERY_TURNS,
  sabotage: 10,
  hack: 10,
  rescue: 14,
};

/** Pods by difficulty for procedural maps. */
export function podPlan(difficulty: number): TemplateId[][] {
  switch (Math.max(1, Math.min(4, difficulty))) {
    case 1:
      return [['trooper', 'trooper'], ['trooper', 'officer'], ['trooper', 'trooper', 'lancer']];
    case 2:
      return [['trooper', 'lancer'], ['sectoid', 'trooper'], ['trooper', 'mec', 'xenoid'], ['lancer', 'xenoid']];
    case 3:
      return [['officer', 'lancer'], ['mec', 'officer'], ['sectoid', 'trooper', 'xenoid'], ['lancer', 'sectoid', 'trooper']];
    default:
      return [['mec', 'lancer'], ['mec', 'officer', 'trooper'], ['officer', 'sectoid', 'xenoid'], ['xenoid', 'sectoid'], ['lancer', 'officer']];
  }
}

const REINFORCEMENT_UNITS: Record<number, TemplateId[]> = {
  1: ['trooper', 'trooper'],
  2: ['trooper', 'lancer'],
  3: ['officer', 'trooper', 'lancer'],
  4: ['officer', 'mec', 'trooper'],
};

/** Alien turns at the end of which a drop is scheduled. */
function reinforcementTurns(kind: MissionKind, difficulty: number): number[] {
  switch (kind) {
    case 'elimination':
      return difficulty >= 3 ? [6] : [];
    case 'sabotage':
    case 'hack':
      return [4, 8];
    default:
      return [5, 9];
  }
}

const VIP_NAMES = ['Dra. Irene Saldaña', 'Ing. Tomás Arrieta', 'Consejera Mara Velasco', 'Dr. Bruno Lasarte', 'Agente Inés Corral'];

export function missionLayout(map: MapSource, kind: MissionKind, podSizes: number[]): MissionLayout {
  if (map.kind === 'handmade') {
    const def = MAPS[map.id];
    if (!def) throw new Error(`Unknown map ${map.id}`);
    return handmadeLayout(def, kind);
  }
  return generateLayout({ biome: map.biome, kind, podSizes, seed: map.seed });
}

export function createMission(opts: MissionOptions): GameState {
  if (!opts.slots.length) throw new Error('A mission needs at least one player');
  const difficulty = opts.difficulty ?? 2;
  const handmade = opts.map.kind === 'handmade' ? MAPS[opts.map.id] : undefined;
  const plan = opts.pods ?? (handmade ? Object.keys(handmade.pods).sort().map((k) => handmade.pods[k]!) : podPlan(difficulty));
  let kind = opts.kind ?? 'elimination';
  const layout = missionLayout(opts.map, kind, plan.map((p) => p.length));
  // A map that can't host the objective falls back to elimination.
  const objectiveMissing =
    (kind === 'recovery' && !layout.item) ||
    (kind === 'hack' && !layout.terminal) ||
    ((kind === 'sabotage' || kind === 'rescue') && !layout.objectiveSpot) ||
    (kind !== 'elimination' && !layout.evacZone.length);
  if (objectiveMissing) kind = 'elimination';
  const squadSize = Math.min(opts.squadSize ?? SQUAD_CLASSES.length, layout.spawns.length);

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
        will: m.will,
        armor: m.armor,
        gear: [...m.gear],
      });
    });
  } else {
    const perPlayer = Math.ceil(squadSize / opts.slots.length);
    for (let i = 0; i < squadSize; i++) {
      const owner = opts.slots[Math.min(Math.floor(i / perPlayer), opts.slots.length - 1)]!;
      const cls = SQUAD_CLASSES[i % SQUAD_CLASSES.length]!;
      const u = makeUnit(`s${i + 1}`, cls, names[i]!, owner, layout.spawns[i]!, null);
      if (opts.veterans) {
        const rank = 3 + opts.rng.int(3);
        const perks = randomPerks(cls, rank, opts.rng);
        const stats = soldierStats(cls, rank, perks);
        units.push({ ...u, rank, perks, mods: stats.mods, maxHp: stats.maxHp, hp: stats.maxHp, charges: stats.charges, will: stats.will });
      } else {
        units.push(u);
      }
    }
  }

  let objectiveUnit: string | null = null;
  if (kind === 'sabotage' && layout.objectiveSpot) {
    units.push({ ...makeUnit('relay', 'relay', TEMPLATES.relay.name, null, layout.objectiveSpot, null), objective: true });
    objectiveUnit = 'relay';
  }
  if (kind === 'rescue' && layout.objectiveSpot) {
    units.push({ ...makeUnit('vip', 'vip', opts.vipName ?? opts.rng.pick(VIP_NAMES), null, layout.objectiveSpot, null), captive: true, ap: 0 });
    objectiveUnit = 'vip';
  }

  const pods: Pod[] = [];
  let alienIndex = 0;
  plan.forEach((members, i) => {
    const spot = layout.pods[i];
    if (!spot) return;
    const pod: Pod = { id: `p${i + 1}`, active: false, unitIds: [], patrol: spot.patrol.map((p) => ({ ...p })), patrolIndex: 0 };
    members.slice(0, spot.tiles.length).forEach((template, k) => {
      const id = `a${++alienIndex}`;
      units.push(makeUnit(id, template, TEMPLATES[template].name, null, spot.tiles[k]!, pod.id));
      pod.unitIds.push(id);
    });
    if (pod.unitIds.length) pods.push(pod);
  });
  const leader = opts.leader && units.find((u) => u.team === 'alien' && u.template === opts.leader!.template);
  if (leader) leader.name = opts.leader!.name;

  const reinforcements: Reinforcement[] = reinforcementTurns(kind, difficulty).map((turn, i) => ({
    id: `r${i + 1}`,
    turn,
    pos: null,
    units: [...REINFORCEMENT_UNITS[Math.max(1, Math.min(4, difficulty))]!],
    landed: false,
  }));

  const mission: MissionState = {
    kind,
    turnsLeft: kind === 'elimination' ? null : opts.turns ?? MISSION_TURNS[kind] ?? null,
    item: kind === 'recovery' && layout.item ? { pos: { ...layout.item }, carrier: null, evacuated: false } : null,
    evacZone: kind === 'elimination' ? [] : layout.evacZone,
    terminal: kind === 'hack' ? layout.terminal : null,
    objectiveUnit,
    objectiveDone: false,
    hackBonus: opts.hackBonus ?? 0,
    reinforcements,
  };

  const tiles = [...layout.tiles];
  // The terminal only exists on hack missions (hand-made maps add it on demand).
  if (kind !== 'hack' && layout.terminal) tiles[layout.terminal.y * layout.width + layout.terminal.x] = 'floor';

  return {
    seq: 0,
    mapId: layout.id,
    mapName: layout.name,
    biome: layout.biome,
    width: layout.width,
    height: layout.height,
    tiles,
    elev: [...layout.elev],
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
    smoke: [],
  };
}
