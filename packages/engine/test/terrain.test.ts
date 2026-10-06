import { describe, expect, it } from 'vitest';
import { previewShot } from '../src/aim';
import { coverAgainst, coverSides } from '../src/cover';
import { lineClear, sightOrigin } from '../src/los';
import { buildPath, computeReach, destinations } from '../src/path';
import { Rng } from '../src/rng';
import { executeCommand } from '../src/rules';
import { addUnit, stateFrom } from './helpers';

const ctx = { slot: 0 as const, connected: [0 as const], rng: new Rng(1) };

describe('heights', () => {
  // A one-storey block in the middle with a ladder on its west side.
  const rows = [
    '..........',
    '..........',
    '...L111...',
    '....111...',
    '....111...',
    '..........',
  ].map((r) => r.replace(/1/g, '.'));
  const heights = [
    '0000000000',
    '0000000000',
    '0000111000',
    '0000111000',
    '0000111000',
    '0000000000',
  ];

  it('a raised block hides what is behind it from the ground but not from its roof', () => {
    const s = stateFrom(rows, heights);
    expect(lineClear(s, { x: 2, y: 3 }, { x: 8, y: 3 })).toBe(false);
    expect(lineClear(s, { x: 5, y: 3 }, { x: 9, y: 3 })).toBe(true);
  });

  it('only ladders lead up; anyone can drop down', () => {
    const s = stateFrom(rows, heights);
    const u = addUnit(s, 's1', 'assault', 1, 3);
    const reach = computeReach(s, u, 40);
    // Climbing straight up the wall is impossible, the ladder at (3,2) leads to the roof.
    const path = buildPath(s, u, reach, { x: 5, y: 3 });
    expect(path).not.toBeNull();
    expect(path!.some((p) => p.x === 3 && p.y === 2)).toBe(true);
    const s2 = stateFrom(rows, heights);
    const top = addUnit(s2, 's2', 'assault', 6, 3);
    const down = computeReach(s2, top, 6);
    expect(down.cost.has(3 * 10 + 7)).toBe(true);
  });

  it('shooting down from a roof adds the height advantage', () => {
    const s = stateFrom(rows, heights);
    const sniper = addUnit(s, 's1', 'specialist', 6, 3);
    const alien = addUnit(s, 'a1', 'trooper', 9, 3);
    const shot = previewShot(s, sniper, alien)!;
    expect(shot.mods.find((m) => m.label === 'Ventaja de altura')?.value).toBe(20);
    const back = previewShot(s, alien, sniper)!;
    expect(back.mods.some((m) => m.label === 'Ventaja de altura')).toBe(false);
  });

  it('the foot of a raised block is full cover; its edge gives nothing to whoever stands on top', () => {
    const s = stateFrom(rows, heights);
    expect(coverSides(s, { x: 7, y: 3 })[3]).toBe(2);
    expect(coverSides(s, { x: 6, y: 3 })[1]).toBe(0);
  });
});

describe('doors and windows', () => {
  const rows = [
    '.......',
    '.##+##.',
    '.#...#.',
    '.=...#.',
    '.#####.',
    '.......',
  ];

  it('a closed door blocks sight until someone walks through it', () => {
    const s = stateFrom(rows);
    addUnit(s, 's1', 'assault', 3, 0);
    addUnit(s, 'a1', 'trooper', 3, 3);
    expect(sightOrigin(s, { x: 3, y: 0 }, { x: 3, y: 3 }, 18)).toBeNull();
    const r = executeCommand(s, { type: 'move', unit: 's1', to: { x: 3, y: 2 } }, ctx);
    expect(r.ok).toBe(true);
    expect(s.tiles[1 * 7 + 3]).toBe('doorOpen');
  });

  it('windows are transparent and give full cover; vaulting costs extra and breaks the glass', () => {
    const s = stateFrom(rows);
    expect(lineClear(s, { x: 0, y: 3 }, { x: 3, y: 3 })).toBe(true);
    expect(coverAgainst(s, { x: 2, y: 3 }, { x: 0, y: 3 })).toBe(2);
    const u = addUnit(s, 's1', 'assault', 0, 3);
    addUnit(s, 'a1', 'trooper', 6, 5);
    const reach = computeReach(s, u, 40);
    expect(destinations(s, u, reach).some((d) => d.pos.x === 1 && d.pos.y === 3)).toBe(false);
    expect(reach.cost.get(3 * 7 + 2)).toBe(2 + 2 + 2);
    executeCommand(s, { type: 'move', unit: 's1', to: { x: 2, y: 3 } }, ctx);
    expect(s.tiles[3 * 7 + 1]).toBe('windowBroken');
  });
});
