import { autoBuild, baseAttrs, soldierStats } from './classes';
import { AP_PER_TURN, RECOVERY_TURNS, SOLDIER_NAMES, SQUAD_CLASSES, TEMPLATES, WEAPONS, ZERO_MODS } from './content';
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
  elimination: 'Limpieza',
  recovery: 'Botín',
  sabotage: 'Sabotaje',
  hack: 'Arreglar la radio',
  rescue: 'Rescate',
};

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

const VIP_NAMES = ['Dra. Irene Saldaña', 'Ing. Tomás Arrieta', 'Alcaldesa Mara Velasco', 'Dr. Bruno Lasarte', 'Agente Inés Corral'];

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
      const rank = opts.veterans ? 3 + opts.rng.int(3) : 0;
      const build = opts.veterans ? autoBuild(cls, rank, opts.rng) : { cls, rank, attrs: baseAttrs(cls), talents: {} };
      const stats = soldierStats(build);
      units.push({ ...u, rank, perks: stats.perks, mods: stats.mods, maxHp: stats.maxHp, hp: stats.maxHp, charges: stats.charges, will: stats.will, armor: stats.armor });
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
    turnsLeft: kind === 'elimination' ? null : opts.turns ?? missionTurns(kind, Math.max(layout.width, layout.height)),
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
    ...(layout.ground ? { ground: [...layout.ground] } : {}),
    ...(layout.decor ? { decor: [...layout.decor] } : {}),
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
