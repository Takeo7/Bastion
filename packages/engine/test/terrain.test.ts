import { describe, expect, it } from 'vitest';
import { abilitiesOf, abilityBlocker, canTargetTile } from '../src/abilities';
import { previewShot } from '../src/aim';
import { NOISE } from '../src/content';
import { coverAgainst, coverSides } from '../src/cover';
import { applyEvent, cloneState } from '../src/events';
import { lineClear, sightOrigin } from '../src/los';
import { buildPath, computeReach, destinations } from '../src/path';
import { Rng } from '../src/rng';
import { executeCommand, SQUAD_LOUDER } from '../src/rules';
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
    const path = buildPath(s, u, reach, { x: 5, y: 3, z: 0 });
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
    expect(sightOrigin(s, { x: 3, y: 0, z: 0 }, { x: 3, y: 3, z: 0 }, 18)).toBeNull();
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

  // Playtest 2026-10-13: opening a door beside you costs nothing.
  it('a soldier beside a door opens and closes it without moving, for free, quietly', () => {
    const s = stateFrom(rows);
    const u = addUnit(s, 's1', 'assault', 3, 0);
    addUnit(s, 'a1', 'trooper', 6, 5);
    const door = { x: 3, y: 1, z: 0 };
    expect(abilitiesOf(u)).toEqual(expect.arrayContaining(['openDoor', 'closeDoor']));
    expect(abilityBlocker(s, u, 'openDoor')).toBeNull();
    expect(abilityBlocker(s, u, 'closeDoor')).toBe('No hay ninguna puerta abierta a tu lado.');
    expect(sightOrigin(s, u.pos, { x: 3, y: 3, z: 0 }, 18)).toBeNull();
    const host = cloneState(s);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'openDoor', tile: door }, ctx);
    expect(r.ok).toBe(true);
    expect(s.tiles[1 * 7 + 3]).toBe('doorOpen');
    expect(u.ap).toBe(2);
    expect(u.pos).toEqual({ x: 3, y: 0, z: 0 });
    expect(sightOrigin(s, u.pos, { x: 3, y: 3, z: 0 }, 18)).not.toBeNull();
    // As quiet as walking through it, heard from the door.
    const events = r.ok ? r.events : [];
    expect(events.filter((e) => e.t === 'noise')).toEqual([{ t: 'noise', tile: door, radius: Math.round(NOISE.door * SQUAD_LOUDER * 10) / 10, by: 's1', heard: [] }]);
    // Clients get there by the events alone.
    for (const e of events) applyEvent(host, e);
    expect(host.tiles).toEqual(s.tiles);

    expect(abilityBlocker(s, u, 'openDoor')).toBe('No hay ninguna puerta cerrada a tu lado.');
    const shut = executeCommand(s, { type: 'ability', unit: 's1', ability: 'closeDoor', tile: door }, ctx);
    expect(shut.ok).toBe(true);
    expect(s.tiles[1 * 7 + 3]).toBe('door');
    expect(u.ap).toBe(2);
    expect(sightOrigin(s, u.pos, { x: 3, y: 3, z: 0 }, 18)).toBeNull();
    // Even disoriented: it's done with the hands.
    u.disoriented = 1;
    expect(abilityBlocker(s, u, 'openDoor')).toBeNull();
  });

  it('only a door beside the soldier, on its level and not round a corner; nobody can be in the doorway to close it', () => {
    const s = stateFrom(rows);
    addUnit(s, 'a1', 'trooper', 6, 5);
    const far = addUnit(s, 's1', 'assault', 6, 0);
    const door = { x: 3, y: 1, z: 0 };
    expect(abilityBlocker(s, far, 'openDoor')).toBe('No hay ninguna puerta cerrada a tu lado.');
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'openDoor', tile: door }, ctx)).toEqual({ ok: false, error: 'No hay ninguna puerta cerrada a tu lado.' });
    // Diagonally beside it, from outside or from inside, is fine; a wall or a window is no door.
    const near = addUnit(s, 's2', 'assault', 2, 0);
    expect(executeCommand(s, { type: 'ability', unit: 's2', ability: 'openDoor', tile: { x: 2, y: 1 } }, ctx)).toEqual({ ok: false, error: 'Esa casilla no es una puerta cerrada contigua.' });
    const inside = addUnit(s, 's3', 'assault', 4, 2);
    expect(canTargetTile(s, near, 'openDoor', door)).toBe(true);
    expect(canTargetTile(s, inside, 'openDoor', door)).toBe(true);
    expect(canTargetTile(s, near, 'openDoor', { x: 2, y: 1, z: 0 })).toBe(false);
    expect(canTargetTile(s, addUnit(s, 's4', 'assault', 0, 3), 'openDoor', { x: 1, y: 3, z: 0 })).toBe(false);
    // Somebody in the doorway: it can't be shut on them.
    executeCommand(s, { type: 'ability', unit: 's2', ability: 'openDoor', tile: door }, ctx);
    const blocking = addUnit(s, 's5', 'assault', 3, 1);
    expect(abilityBlocker(s, near, 'closeDoor')).toBe('No hay ninguna puerta abierta a tu lado.');
    expect(executeCommand(s, { type: 'ability', unit: 's2', ability: 'closeDoor', tile: door }, ctx)).toEqual({ ok: false, error: 'No hay ninguna puerta abierta a tu lado.' });
    blocking.alive = false;
    s.units = s.units.filter((x) => x !== blocking);
    expect(canTargetTile(s, near, 'closeDoor', door)).toBe(true);

    // Round a wall's corner (both sides of the diagonal are wall): out of reach.
    const corner = stateFrom(['..#..', '.#+..', '.....']);
    const stuck = addUnit(corner, 's1', 'assault', 1, 0);
    expect(abilityBlocker(corner, stuck, 'openDoor')).toBe('No hay ninguna puerta cerrada a tu lado.');
    // On a roof above the doorway, neither.
    const roof = stateFrom(['.....', '..+..', '.....'], ['.1...']);
    expect(abilityBlocker(roof, addUnit(roof, 's1', 'assault', 1, 0), 'openDoor')).toBe('No hay ninguna puerta cerrada a tu lado.');
  });
});
