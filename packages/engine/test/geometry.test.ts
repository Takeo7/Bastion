import { describe, expect, it } from 'vitest';
import { coverAgainst } from '../src/cover';
import { lineClear, peekOrigins, sightOrigin } from '../src/los';
import { buildPath, computeReach, COST_ORTHO } from '../src/path';
import { addUnit, stateFrom } from './helpers';

describe('line of sight', () => {
  it('walls block, low cover does not', () => {
    const s = stateFrom(['.....', '..#..', '.....', '..h..', '.....']);
    expect(lineClear(s, { x: 2, y: 0 }, { x: 2, y: 2 })).toBe(false);
    expect(lineClear(s, { x: 2, y: 2 }, { x: 2, y: 4 })).toBe(true);
  });

  it('a diagonal through a corner is blocked only when both sides block', () => {
    const one = stateFrom(['.#', '..']);
    expect(lineClear(one, { x: 0, y: 1 }, { x: 1, y: 0 })).toBe(true);
    const both = stateFrom(['.#', '#.']);
    expect(lineClear(both, { x: 0, y: 0 }, { x: 1, y: 1 })).toBe(false);
  });

  it('a soldier behind a wall can step out to see around it', () => {
    const s = stateFrom([
      '.....',
      '.###.',
      '.....',
    ]);
    // Standing under the wall end at (3,2), the step-out tile (4,2) sees (4,0).
    expect(peekOrigins(s, { x: 3, y: 2 })).toContainEqual({ x: 4, y: 2 });
    expect(sightOrigin(s, { x: 3, y: 2 }, { x: 4, y: 0 }, 10)).not.toBeNull();
    // The middle of the wall has no corner to peek around.
    expect(sightOrigin(s, { x: 2, y: 2 }, { x: 2, y: 0 }, 10)).toBeNull();
  });
});

describe('cover', () => {
  const s = stateFrom([
    '.......',
    '.......',
    '...h...',
    '...u...',
    '.......',
  ]);
  const target = { x: 3, y: 3 };

  it('low cover protects against attacks from in front', () => {
    expect(coverAgainst(s, target, { x: 3, y: 0 })).toBe(1);
    expect(coverAgainst(s, target, { x: 4, y: 0 })).toBe(1);
  });

  it('attacks from the side or behind flank the target', () => {
    expect(coverAgainst(s, target, { x: 6, y: 3 })).toBe(0);
    expect(coverAgainst(s, target, { x: 3, y: 4 })).toBe(0);
  });

  it('full cover beats half cover', () => {
    const t = stateFrom(['.#.', '.u.', '...']);
    t.tiles[3] = 'low';
    expect(coverAgainst(t, { x: 1, y: 1 }, { x: 1, y: -5 })).toBe(2);
  });
});

describe('pathfinding', () => {
  it('uses 1.5-tile diagonals and never cuts wall corners', () => {
    const s = stateFrom(['...', '.#.', '...']);
    const u = addUnit(s, 's1', 'specialist', 0, 0);
    const reach = computeReach(s, u, 100);
    expect(reach.cost.get(2 * 3 + 2)).toBe(COST_ORTHO * 4); // around the wall, no corner cutting
    const path = buildPath(s, u, reach, { x: 2, y: 2 })!;
    expect(path.at(-1)).toEqual({ x: 2, y: 2 });
  });

  it('can pass through allies but not stop on them, and enemies block', () => {
    const s = stateFrom(['.....']);
    const u = addUnit(s, 's1', 'specialist', 0, 0);
    addUnit(s, 's2', 'specialist', 1, 0);
    const reach = computeReach(s, u, 100);
    expect(buildPath(s, u, reach, { x: 1, y: 0 })).toBeNull();
    expect(buildPath(s, u, reach, { x: 2, y: 0 })).toHaveLength(2);

    addUnit(s, 'a1', 'trooper', 3, 0);
    expect(computeReach(s, u, 100).cost.has(4)).toBe(false);
  });
});
