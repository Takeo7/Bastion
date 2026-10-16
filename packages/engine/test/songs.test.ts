import { describe, expect, it } from 'vitest';
import { abilityBlocker, abilityTargets, apNeeded, canTargetTile, noiseOf, psiChance } from '../src/abilities';
import { previewShot } from '../src/aim';
import { Rng } from '../src/rng';
import { executeCommand, Sim } from '../src/rules';
import type { GameState } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// Velmar's Canto (docs/LORE.md): the rules behind the songs. The talents that
// grant them live in classes.ts; here the songs are performed directly.

const field = (w = 20, h = 20) => stateFrom(Array.from({ length: h }, () => '.'.repeat(w)));
const sim = (s: GameState, seed = 1) => new Sim(s, new Rng(seed), [0]);

describe('songs', () => {
  it('Salto de eco: to a free tile in sight within 6; the Oficio never gives the squad away, and is quieter with the Taller by the listening post', () => {
    const s = field();
    s.concealed = true;
    const u = addUnit(s, 's1', 'assault', 2, 2);
    addUnit(s, 's2', 'grenadier', 4, 2);
    addUnit(s, 'a1', 'zombie', 7, 15);
    expect(canTargetTile(s, u, 'echoStep', { x: 4, y: 2, z: 0 })).toBe(false);
    expect(canTargetTile(s, u, 'echoStep', { x: 9, y: 2, z: 0 })).toBe(false);
    expect(canTargetTile(s, u, 'echoStep', { x: 6, y: 4, z: 0 })).toBe(true);
    const run = sim(s);
    run.perform(u, 'echoStep', { tile: { x: 6, y: 4, z: 0 } });
    expect(u.pos).toEqual({ x: 6, y: 4, z: 0 });
    expect(s.concealed).toBe(true);
    expect(run.events.find((e) => e.t === 'noise')).toMatchObject({ tile: { x: 6, y: 4 }, radius: 1 });
    expect(noiseOf(s, u, 'silence')).toBe(12);
    expect(noiseOf(s, u, 'fireball')).toBe(10);

    const q = field();
    q.concealed = true;
    q.quietSongs = true;
    const v = addUnit(q, 's1', 'assault', 2, 2);
    const quiet = sim(q);
    quiet.perform(v, 'echoStep', { tile: { x: 5, y: 2, z: 0 } });
    expect(q.concealed).toBe(true);
    expect(quiet.events.some((e) => e.t === 'noise')).toBe(false);
    expect(noiseOf(q, v, 'silence')).toBe(10);
    // The shotgun is as loud as ever.
    expect(noiseOf(q, v, 'shoot')).toBe(10);
  });

  it('a song costs its own action: the spectral arrow needs no sniper set-up', () => {
    const s = field();
    const u = addUnit(s, 's1', 'sharpshooter', 2, 2, { ap: 1 });
    expect(apNeeded(u, 'shoot')).toBe(2);
    expect(apNeeded(u, 'spectralArrow')).toBe(1);
  });

  it('Flecha espectral: the target counts as flanked behind its cover', () => {
    const s = stateFrom(['..........', '....H.....', '..........']);
    const u = addUnit(s, 's1', 'sharpshooter', 4, 2);
    const t = addUnit(s, 'a1', 'trooper', 4, 0);
    expect(previewShot(s, u, t, { ability: 'shoot' })!.cover).toBe(2);
    const arrow = previewShot(s, u, t, { ability: 'spectralArrow' })!;
    expect(arrow).toMatchObject({ cover: 0, flanked: true });
  });

  it('Saeta never misses and ignores cover, but not armour; Llama alta and a partner nearby add to it', () => {
    const s = field();
    const u = addUnit(s, 's1', 'grenadier', 2, 2);
    const mec = addUnit(s, 'a1', 'mec', 8, 2);
    expect(abilityTargets(s, u, 'bolt')).toContain(mec);
    const hp = mec.hp;
    sim(s).perform(u, 'bolt', { target: mec });
    expect(mec.hp).toBe(hp - (3 - mec.armor));
    u.mods.songDamage = 1;
    addUnit(s, 's2', 'specialist', 3, 2, { owner: 1, mods: { ...u.mods, singer: 1 } });
    const before = mec.hp;
    sim(s).perform(u, 'bolt', { target: mec });
    expect(mec.hp).toBe(before - (3 + 1 + 1 - mec.armor));
  });

  it('Juramento de Velmar: half damage to the singer, and no panic or control around it', () => {
    const s = field();
    const paladin = addUnit(s, 's1', 'assault', 2, 2);
    const ally = addUnit(s, 's2', 'grenadier', 4, 2);
    addUnit(s, 'a1', 'sectoid', 9, 2);
    sim(s).perform(paladin, 'velmarOath');
    expect(paladin.oath).toBe(2);
    const hp = paladin.hp;
    sim(s).damage(paladin, 4, 0);
    expect(paladin.hp).toBe(hp - 2);
    const sectoid = s.units.find((x) => x.id === 'a1')!;
    for (let seed = 1; seed < 40; seed++) {
      const events = (() => {
        const run = sim(s, seed);
        ally.panicked = false;
        ally.controlledBy = null;
        run.perform(sectoid, 'mindspin', { target: ally });
        return run.events;
      })();
      const psi = events.find((e) => e.t === 'psi');
      expect(psi && psi.t === 'psi' ? psi.effect : null).not.toBe('panicked');
      expect(psi && psi.t === 'psi' ? psi.effect : null).not.toBe('controlled');
    }
  });

  it('Petardos: enemies in the zone are disoriented and the Chillones there cannot scream', () => {
    const s = field();
    const u = addUnit(s, 's1', 'grenadier', 2, 2);
    const sectoid = addUnit(s, 'a1', 'sectoid', 9, 2);
    sim(s).perform(u, 'silence', { tile: { x: 9, y: 3, z: 0 } });
    expect(s.silences).toHaveLength(1);
    expect(sectoid.disoriented).toBeGreaterThan(0);
    sectoid.ap = 2;
    expect(abilityBlocker(s, sectoid, 'mindspin')).toMatch(/Petardos/);
  });

  it('Contracanto wards; Canto de calma steadies those around', () => {
    const s = field();
    const singer = addUnit(s, 's1', 'specialist', 2, 2);
    const base = psiChance(singer, s);
    addUnit(s, 's2', 'specialist', 4, 2, { mods: { ...singer.mods, calmAura: 2 } });
    expect(psiChance(singer, s)).toBe(base - 20);
    singer.ward = 2;
    expect(psiChance(singer, s)).toBe(0);
  });

  it('Arenga and Rastreo show in the odds', () => {
    const s = field();
    const bard = addUnit(s, 's1', 'specialist', 2, 2);
    const sniper = addUnit(s, 's2', 'sharpshooter', 3, 2);
    const other = addUnit(s, 's3', 'grenadier', 2, 3);
    const t = addUnit(s, 'a1', 'trooper', 10, 2);
    const plain = previewShot(s, sniper, t, { ability: 'shoot' })!;
    sim(s).perform(bard, 'hymn');
    expect(sniper.aided).toBe(10);
    sim(s).perform(sniper, 'huntersMark', { target: t });
    const marked = previewShot(s, sniper, t, { ability: 'shoot' })!;
    expect(marked.mods.map((m) => m.label)).toEqual(expect.arrayContaining(['Arenga', 'Rastreo']));
    expect(marked.hit).toBe(Math.min(100, plain.hit + 10 + 15));
    // The mark is the hunter's alone.
    expect(previewShot(s, other, t, { ability: 'shoot' })!.mods.some((m) => m.label === 'Rastreo')).toBe(false);
  });


  it('Último verso brings back whoever fell this turn; otherwise it heals those around', () => {
    const s = field();
    const cleric = addUnit(s, 's1', 'specialist', 2, 2);
    const fallen = addUnit(s, 's2', 'assault', 3, 2);
    const run = sim(s);
    run.damage(fallen, 99, 0);
    expect(fallen.alive).toBe(false);
    run.perform(cleric, 'lastVerse');
    expect(fallen).toMatchObject({ alive: true, hp: 3 });

    const t = field();
    const healer = addUnit(t, 's1', 'specialist', 2, 2);
    const hurt = addUnit(t, 's2', 'assault', 3, 2);
    hurt.hp = 1;
    sim(t).perform(healer, 'lastVerse');
    expect(hurt.hp).toBe(Math.min(hurt.maxHp, 5));
  });
});
