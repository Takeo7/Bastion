import { makeUnit } from '../src/setup';
import type { GameState, Slot, TemplateId, TileKind, Unit } from '../src/types';

const LEGEND: Record<string, TileKind> = { '#': 'wall', H: 'high', h: 'low', '+': 'door', '=': 'window', L: 'ladder', C: 'console' };

/**
 * Builds a small state from ASCII rows (same legend as maps, without pods),
 * with optional elevation digits. Units are added explicitly with `addUnit`.
 */
export function stateFrom(rows: string[], heights?: string[]): GameState {
  const tiles: TileKind[] = [];
  const elev: number[] = [];
  rows.forEach((row, y) => {
    [...row].forEach((ch, x) => {
      tiles.push(LEGEND[ch] ?? 'floor');
      elev.push(Number(heights?.[y]?.[x] ?? 0) || 0);
    });
  });
  return {
    seq: 0,
    mapId: 'test',
    mapName: 'Prueba',
    biome: 'city',
    width: rows[0]!.length,
    height: rows.length,
    tiles,
    elev,
    units: [],
    pods: [],
    mission: { kind: 'elimination', turnsLeft: null, item: null, evacZone: [], terminal: null, objectiveUnit: null, objectiveDone: false, hackBonus: 0, reinforcements: [] },
    turn: 1,
    activeTeam: 'xcom',
    concealed: false,
    ready: [false, false],
    outcome: null,
    outcomeReason: null,
    campaignMission: null,
    smoke: [],
  };
}

export function addUnit(
  s: GameState,
  id: string,
  template: TemplateId,
  x: number,
  y: number,
  extra: Partial<Unit> & { owner?: Slot | null } = {},
): Unit {
  const base = makeUnit(id, template, id, null, { x, y }, null);
  const u: Unit = { ...base, owner: base.team === 'xcom' ? 0 : null, ap: 2, ...extra };
  s.units.push(u);
  return u;
}
