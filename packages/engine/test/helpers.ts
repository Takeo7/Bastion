import { BASE_EDGE, besidePieces, fellTree, PIECES, pieceCells, placeBlocker, placePiece, type BasePiece, type PieceId } from '../src/base';
import { executeCampaignCommand, type CampaignState } from '../src/campaign';
import { bastionLevel, MAX_LEVEL } from '../src/progression';
import { Rng } from '../src/rng';
import { makeUnit } from '../src/setup';
import type { GameState, Slot, TemplateId, TileKind, Unit } from '../src/types';

const LEGEND: Record<string, TileKind> = { '#': 'wall', H: 'high', h: 'low', '+': 'door', '=': 'window', L: 'ladder', S: 'ladder', C: 'console' };

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

/**
 * Gives a state its storeys above the ground (docs/FASE6-PLAN.md), bottom up,
 * drawn like the ground: a space where a storey has no slab (open air, a
 * stairwell), `R` a roof nobody stands on, `S` stairs (a ladder).
 */
export function withLevels(s: GameState, levels: string[][]): GameState {
  s.levels = levels.map((rows) => ({
    tiles: rows.flatMap((row) => [...row].map((ch): TileKind | null => (ch === ' ' ? null : ch === 'R' ? 'roof' : (LEGEND[ch] ?? 'floor')))),
  }));
  return s;
}

export function addUnit(
  s: GameState,
  id: string,
  template: TemplateId,
  x: number,
  y: number,
  extra: Partial<Unit> & { owner?: Slot | null; z?: number } = {},
): Unit {
  const { z = 0, ...rest } = extra;
  // Without a facing given, the dead look all round, as before the cone (los.ts inViewCone): tests about it set one.
  const { facing: _, ...base } = makeUnit(id, template, id, null, { x, y, z }, null);
  const u: Unit = { ...base, owner: base.team === 'xcom' ? 0 : null, ap: 2, ...rest };
  s.units.push(u);
  return u;
}

/**
 * Raises a building (or any piece) on a campaign's new base (docs/FASE12.md
 * §11) at `level`: at the free place nearest the caravan touching nothing (no
 * «junto a» bonus by chance), or wall to wall with `beside` when given; felling
 * what is in the way, if anything.
 */
export function raise(c: CampaignState, piece: PieceId, level = 1, beside?: BasePiece): BasePiece {
  const base = c.bastion;
  const spot = freeSpot(c, piece, beside);
  for (const cell of pieceCells(piece, spot.x, spot.y, 0)) fellTree(base, cell.x, cell.y);
  const placed = placePiece(base, piece, spot.x, spot.y, 0);
  if (placed.level) placed.level = level;
  return placed;
}

/** The place nearest the caravan where `piece` fits unturned once the pines are felled: touching nothing, or wall to wall with `beside`. */
export function freeSpot(c: CampaignState, piece: PieceId, beside?: BasePiece): { x: number; y: number } {
  const base = c.bastion;
  const caravan = base.pieces.find((p) => p.piece === 'caravan')!;
  const [w, h] = PIECES[piece].size;
  const spots: { x: number; y: number; d: number }[] = [];
  for (let y = BASE_EDGE; y <= base.height - BASE_EDGE - h; y++) {
    for (let x = BASE_EDGE; x <= base.width - BASE_EDGE - w; x++) {
      spots.push({ x, y, d: Math.hypot(x + w / 2 - caravan.x - 2, y + h / 2 - caravan.y - 1) });
    }
  }
  spots.sort((a, b) => a.d - b.d || a.y - b.y || a.x - b.x);
  const touches = (x: number, y: number) => {
    const p = placePiece({ ...base, pieces: [], nextPiece: 0 }, piece, x, y, 0);
    const next = besidePieces({ ...base, pieces: [...base.pieces, p] }, p);
    return beside ? next.includes(beside) : !next.length;
  };
  return spots.find((s) => !placeBlocker({ ...base, trees: [] }, piece, s.x, s.y, 0) && touches(s.x, s.y))!;
}

/**
 * The Bastion straight up to `level`, every level bought as the players buy it
 * (the `levelUp` order, alone at the base), without spending what the test
 * set: the suministros and the missions flown are put back after.
 */
export function levelTo(c: CampaignState, level: number, rng = new Rng(1)): void {
  const { credits, missionsFlown } = c;
  const slot = c.players[0]!;
  while (bastionLevel(c) < Math.min(level, MAX_LEVEL)) {
    c.credits = 1e6;
    c.missionsFlown = 1e3;
    const r = executeCampaignCommand(c, { type: 'levelUp' }, { slot, connected: [slot], rng });
    if (!r.ok) throw new Error(r.error);
  }
  c.credits = credits;
  if (missionsFlown === undefined) delete c.missionsFlown;
  else c.missionsFlown = missionsFlown;
}
