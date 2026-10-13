import { describe, expect, it } from 'vitest';
import { abilityTargets, canTargetTile, psiChance } from '../src/abilities';
import { previewShot } from '../src/aim';
import { createCampaign, executeCampaignCommand, pointsToSpend, RETRAIN_COST, type CampaignCommand, type CampaignState } from '../src/campaign';
import {
  ATTR_CAP,
  ATTR_IDS,
  ATTR_MAX,
  attrBlocker,
  attrPointsFree,
  attrCap,
  autoBuild,
  AXES,
  axesOf,
  baseAttrs,
  BRANCHES,
  CENTER,
  CLASS_TOP,
  dormant,
  leaningClass,
  learnTalent,
  pointsAbove,
  rowGate,
  ROW_GATES,
  SONG_GATES,
  soldierStats,
  specialize,
  talentBlocker,
  talentPointsFree,
  talentsOf,
  TALENTS,
  woundDays,
  wouldSpecialize,
  type Attributes,
  type Build,
} from '../src/classes';
import { ABILITIES } from '../src/content';
import type { GameEvent } from '../src/events';
import { Rng } from '../src/rng';
import { Sim } from '../src/rules';
import type { ClassId, PerkId, Slot } from '../src/types';
import { addUnit, stateFrom } from './helpers';

const field = (w = 20, h = 20) => stateFrom(Array.from({ length: h }, () => '.'.repeat(w)));
const with_ = (cls: ClassId | null, extra: Partial<Attributes>): Attributes => ({ ...baseAttrs(cls), ...extra });
const build = (cls: ClassId | null, rank: number, talents: Build['talents'] = {}, attrs: Partial<Attributes> = {}): Build => ({ cls, rank, attrs: with_(cls, attrs), talents });

function run(c: CampaignState, cmd: CampaignCommand, slot: Slot = 0) {
  return executeCampaignCommand(c, cmd, { slot, connected: [0, 1], rng: new Rng(7) });
}

describe('aptitudes', () => {
  it('recruits start with 22 points and a cap of 8; a class adds +1 to its two aptitudes and lets them reach 10', () => {
    const recruit = baseAttrs(null);
    expect(ATTR_IDS.reduce((sum, id) => sum + recruit[id], 0)).toBe(22);
    for (const id of ATTR_IDS) expect(attrCap(null, id)).toBe(ATTR_CAP);
    for (const cls of AXES) {
      const base = baseAttrs(cls);
      for (const id of ATTR_IDS) {
        const own = CLASS_TOP[cls].includes(id);
        expect(base[id]).toBe(recruit[id] + (own ? 1 : 0));
        expect(attrCap(cls, id)).toBe(own ? ATTR_MAX : ATTR_CAP);
      }
    }
  });

  it('two points per promotion, up to the cap', () => {
    expect(attrBlocker(build('grenadier', 0), 'pul')).toMatch(/No quedan puntos/);
    expect(attrBlocker(build('grenadier', 1), 'pul')).toBeNull();
    expect(attrBlocker(build('grenadier', 7, {}, { ref: 8 }), 'ref')).toMatch(/tope \(8\)/);
    expect(attrBlocker(build('grenadier', 7, {}, { agu: 9 }), 'agu')).toBeNull();
    expect(attrBlocker(build(null, 7, {}, { agu: 8 }), 'agu')).toMatch(/especialízate para subirla a 10/);
  });

  it('aptitudes set aim, health, defence, will and hacking; 7 and 10 bring extras', () => {
    // A rookie sniper: Pulso 6, Aguante 5, Reflejos 5, Temple 5, Maña 3.
    const rookie = soldierStats(build('sharpshooter', 0));
    expect(rookie.template).toBe('sharpshooter');
    expect(70 + rookie.mods.aim).toBe(68);
    expect(rookie.maxHp).toBe(5);
    expect(rookie.mods.defense).toBe(4);
    expect(rookie.will).toBe(46);
    expect(rookie.mods.hack).toBe(-10);
    expect(rookie.mods.crit).toBe(0);
    // Veterancy still adds a little by itself.
    expect(soldierStats(build('sharpshooter', 3)).mods.aim).toBe(rookie.mods.aim + 6);
    // A recruit fights as one, with a rifle.
    const recruit = soldierStats(build(null, 0));
    expect(recruit.template).toBe('rookie');
    expect(65 + recruit.mods.aim).toBe(65);

    const top = soldierStats(build('sharpshooter', 7, {}, { pul: 10, agu: 10, ref: 10, tem: 10, ofi: 10 }));
    expect(top.mods.crit).toBe(10);
    expect(top.mods.critDamage).toBe(1);
    expect(top.armor).toBe(1);
    expect(top.mods.mobility).toBe(1);
    expect(top.mods.reactionDefense).toBe(20);
    expect(top.mods.psiGuard).toBe(2);
    expect(top.charges.grenade).toBe(2);
    expect(top.mods.heal).toBe(1);
    expect(top.mods.grenadeDamage).toBe(1);
    expect(soldierStats(build('sharpshooter', 0, {}, { tem: 7 })).mods.psiGuard).toBe(1);
  });

  it('Aguante 7 shortens wounds', () => {
    expect(woundDays(3, with_('assault', { agu: 6 }))).toBe(8);
    expect(woundDays(3, with_('assault', { agu: 7 }))).toBe(6);
  });
});

describe('the skill tree', () => {
  it('a centre, and four axes of three branches plus the Oficio, each closed by a capstone the rows above can pay for', () => {
    expect(CENTER.length).toBe(4);
    for (const cls of AXES) {
      const talents = talentsOf(cls);
      expect(BRANCHES[cls]).toHaveLength(4);
      expect(BRANCHES[cls][3].song).toBe(true);
      for (const branch of [0, 1, 2, 3]) {
        const mine = talents.filter((d) => d.branch === branch);
        expect(mine.filter((d) => d.row === 4)).toHaveLength(1);
        const above = mine.filter((d) => d.row < 4).reduce((sum, d) => sum + d.max, 0);
        expect(above).toBeGreaterThanOrEqual(branch === 3 ? SONG_GATES[4] : ROW_GATES[4]);
      }
      // The Oficio: one talent per row, every one flagged as a song (noisy).
      expect(talents.filter((d) => d.branch === 3).map((d) => d.row)).toEqual([0, 1, 2, 3, 4]);
      expect(talents.filter((d) => d.branch === 3).every((d) => d.song)).toBe(true);
      // One "llave doble" per axis, and every prerequisite comes from the same branch, no lower.
      expect(talents.filter((d) => d.coop)).toHaveLength(1);
      for (const d of talents) {
        if (d.ability) expect(ABILITIES[d.ability]).toBeDefined();
        if (d.needs) expect(TALENTS[d.needs]).toMatchObject({ axis: cls, branch: d.branch });
        if (d.needs) expect(TALENTS[d.needs].row).toBeLessThanOrEqual(d.row);
      }
    }
  });

  it('a row opens with the points spent above it in the same branch', () => {
    expect(talentBlocker(build('sharpshooter', 7), 'farsight')).toMatch(/pide 2 puntos en Tirador \(lleva 0\)/);
    expect(talentBlocker(build('sharpshooter', 7, { patience: 2 }), 'farsight')).toMatch(/lleva 0/);
    expect(talentBlocker(build('sharpshooter', 7, { deadeye: 2 }), 'farsight')).toBeNull();
    expect(talentBlocker(build('sharpshooter', 1, { deadeye: 2 }), 'farsight')).toMatch(/No quedan puntos de talento/);
    expect(talentBlocker(build('sharpshooter', 7, { deadeye: 3 }), 'deadeye')).toMatch(/al máximo/);
  });

  it('some talents ask for an aptitude or another talent first', () => {
    const talents = { deadeye: 3, hawkEye: 2 } as const;
    expect(talentBlocker(build('sharpshooter', 7, talents), 'aimedShot')).toMatch(/Pide Pulso 7 \(tiene 6\)/);
    expect(talentBlocker(build('sharpshooter', 7, talents, { pul: 7 }), 'aimedShot')).toBeNull();
    expect(talentBlocker(build('specialist', 7, { marksman: 2 }), 'battery')).toMatch(/Antes hace falta Calambrazo/);
  });

  it('a recruit learns from two axes without a class, and leans to the one taught most', () => {
    const b = build(null, 4);
    learnTalent(b, 'coolHead');
    // A recruit may learn a trade too, when the Taller de oficios allows it.
    expect(talentBlocker(b, 'bolt', 3)).toBeNull();
    learnTalent(b, 'patience');
    learnTalent(b, 'hawkEye');
    learnTalent(b, 'runner');
    expect(b.cls).toBeNull();
    expect(wouldSpecialize(b, 'hawkEye')).toBeNull();
    expect(leaningClass(b)).toBe('sharpshooter');
    // The last two rows of an axis wait for the class.
    expect(talentBlocker(build(null, 4, { runner: 1, light: 2, feline: 2, evasion: 2 }), 'untouchable')).toMatch(/al especializarse, en Capitán/);
    // Aptitudes count too: Reflejos raised tips a tie towards the Asalto.
    expect(leaningClass(build(null, 2, { hawkEye: 1, runner: 1 }, { ref: 7 }))).toBe('assault');
    // Specialising adds the class's +1 + 1, with the new base: the free points don't change.
    const before = { ...b.attrs };
    const free = attrPointsFree(b);
    specialize(b, 'sharpshooter');
    expect(b.attrs.pul).toBe(before.pul + 1);
    expect(b.attrs.tem).toBe(before.tem + 1);
    expect(attrPointsFree(b)).toBe(free);
    expect(soldierStats(b).template).toBe('sharpshooter');
    // A retrained captain specialises again with two points in an axis.
    const captain = build(null, 5, { patience: 1 });
    expect(wouldSpecialize(captain, 'hawkEye')).toBe('sharpshooter');
    expect(learnTalent(captain, 'hawkEye')).toEqual({ specialized: 'sharpshooter' });
  });

  it('a recruit may learn a kit talent ahead: it sleeps until the soldier carries the kit', () => {
    expect(talentBlocker(build(null, 2), 'keenBlade')).toBeNull();
    const recruit = build(null, 2, { slug: 1 });
    expect(dormant(recruit, 'slug')).toBe(true);
    expect(soldierStats(recruit).perks).toEqual([]);
    expect(soldierStats(recruit).mods.damage).toBe(soldierStats(build(null, 2)).mods.damage);
    expect(soldierStats(build('assault', 2, { slug: 1 })).perks).toEqual(['slug']);
    expect(talentBlocker(build('sharpshooter', 7), 'keenBlade')).toMatch(/Necesita la espada del Asalto/);
  });

  it('a second axis up to its third row; never a third one', () => {
    const sniper = build('sharpshooter', 7, { hawkEye: 2 });
    expect(axesOf(sniper)).toMatchObject({ main: 'sharpshooter', second: null });
    expect(talentBlocker(sniper, 'runner')).toBeNull();
    sniper.talents.runner = 1;
    sniper.talents.light = 2;
    sniper.talents.feline = 2;
    expect(axesOf(sniper)).toMatchObject({ main: 'sharpshooter', second: 'assault' });
    expect(talentBlocker(sniper, 'evasion')).toBeNull();
    sniper.talents.evasion = 2;
    expect(talentBlocker(sniper, 'untouchable')).toMatch(/Las dos últimas filas son solo de tu eje principal/);
    expect(talentBlocker(sniper, 'steadyCannon')).toMatch(/Ya tienes dos ejes: Francotirador y Asalto/);
    // The centre belongs to everyone.
    expect(talentBlocker(sniper, 'drill')).toBeNull();
    // A recruit can try two axes before the third one is closed.
    const recruit = build(null, 3, { runner: 1, patience: 1 });
    expect(talentBlocker(recruit, 'steadyCannon')).toMatch(/Ya tienes dos ejes: Asalto y Francotirador/);
  });

  it('the Oficio opens row by row with the Taller de oficios, and the big trades ask for Temple 7', () => {
    const grenadier = build('grenadier', 7, {}, { tem: 7 });
    expect(talentBlocker(grenadier, 'bolt')).toBe('Hace falta el Taller de oficios.');
    expect(talentBlocker(grenadier, 'bolt', 1)).toBeNull();
    grenadier.talents = { bolt: 1, silence: 1 };
    expect(talentBlocker(grenadier, 'fireball', 1)).toBe('Hace falta el Taller de oficios a nivel 2.');
    expect(talentBlocker(grenadier, 'fireball', 2)).toBeNull();
    grenadier.talents = { bolt: 1, silence: 1, fireball: 1, highFlame: 2 };
    expect(talentBlocker(grenadier, 'velmarStorm', 2)).toBe('Hace falta el Taller de oficios a nivel 3.');
    expect(talentBlocker(grenadier, 'velmarStorm', 3)).toBeNull();
    expect(talentBlocker({ ...grenadier, attrs: baseAttrs('grenadier') }, 'velmarStorm', 3)).toMatch(/Pide Temple 7/);
  });

  it('knowing a trade of the Oficio makes a singer for «A dos voces», at no price against the Chillón', () => {
    const singer = soldierStats(build('grenadier', 2, { bolt: 1 }));
    const plain = soldierStats(build('grenadier', 2, { demolition: 1 }));
    expect(singer.mods.singer).toBe(1);
    expect(singer.perks).toEqual(['bolt']);
    expect(plain.mods.singer).toBe(0);
    expect(singer.will).toBe(plain.will);
    // Machetazo disorients only at its last level; Prismáticos see through smoke from the first.
    expect(soldierStats(build('assault', 2, { songBlade: 1 })).mods.slashDisorient).toBe(0);
    expect(soldierStats(build('assault', 2, { songBlade: 2 })).mods).toMatchObject({ slashDisorient: 1, meleeDamage: 2 });
    expect(soldierStats(build('sharpshooter', 2, { windEyes: 1 })).mods).toMatchObject({ sight: 2, smokeSight: 1 });
    expect(soldierStats(build('sharpshooter', 7, { velmarOath: 1 })).charges.velmarOath).toBe(1);
  });

  it('auto builds spend every point within the rules', () => {
    for (const cls of AXES) {
      for (let seed = 1; seed <= 20; seed++) {
        const b = autoBuild(cls, 7, new Rng(seed));
        expect(b.cls).toBe(cls);
        expect(ATTR_IDS.reduce((sum, id) => sum + b.attrs[id], 0)).toBe(24 + 14);
        for (const id of ATTR_IDS) expect(b.attrs[id]).toBeLessThanOrEqual(attrCap(cls, id));
        expect(talentPointsFree(b)).toBe(0);
        for (const id of Object.keys(b.talents) as PerkId[]) {
          const d = TALENTS[id];
          expect(d.axis === cls || d.axis === null).toBe(true);
          if (d.axis) expect(pointsAbove(b.talents, d.axis, d.branch, d.row)).toBeGreaterThanOrEqual(rowGate(d));
          if (d.req) expect(b.attrs[d.req[0]]).toBeGreaterThanOrEqual(d.req[1]);
        }
      }
    }
  });

  it('talent levels multiply their bonuses and grant their abilities', () => {
    const stats = soldierStats(build('specialist', 4, { combatProtocol: 1, battery: 2, guardian: 3 }));
    expect(stats.charges.combatProtocol).toBe(4);
    expect(stats.mods.aid).toBe(15);
    expect(stats.perks).toEqual(['combatProtocol', 'battery', 'guardian']);
  });
});

describe('training in the barracks', () => {
  function campaign() {
    const c = createCampaign([0, 1], new Rng(3));
    const s = c.soldiers.find((x) => x.owner === 0)!;
    specialize(s, 'sharpshooter');
    s.rank = 1;
    return { c, s };
  }

  it('a promotion gives two aptitude and two talent points to spend', () => {
    const { c, s } = campaign();
    expect(pointsToSpend(s)).toEqual({ attrs: 2, talents: 2 });
    expect(run(c, { type: 'train', soldier: s.id, attr: 'pul' }).ok).toBe(true);
    expect(run(c, { type: 'train', soldier: s.id, attr: 'pul' }).ok).toBe(true);
    expect(run(c, { type: 'train', soldier: s.id, attr: 'pul' })).toMatchObject({ ok: false, error: expect.stringMatching(/No quedan puntos/) });
    expect(s.attrs.pul).toBe(8);
    expect(run(c, { type: 'talent', soldier: s.id, talent: 'deadeye' }).ok).toBe(true);
    expect(run(c, { type: 'talent', soldier: s.id, talent: 'farsight' })).toMatchObject({ ok: false, error: expect.stringMatching(/lleva 1/) });
    expect(c.log.some((l) => l.text.includes('aprende Precisión letal'))).toBe(true);
    // The partner can look but not train someone else's soldier.
    expect(run(c, { type: 'talent', soldier: s.id, talent: 'deadeye' }, 1).ok).toBe(false);
  });

  it('a recruit arrives without a class and keeps it until Capitán', () => {
    const { c } = campaign();
    c.soldiers.at(-1)!.alive = false;
    expect(run(c, { type: 'recruit' })).toMatchObject({ ok: true });
    const r = c.soldiers.at(-1)!;
    expect(r.cls).toBeNull();
    r.rank = 1;
    run(c, { type: 'talent', soldier: r.id, talent: 'marksman' });
    run(c, { type: 'talent', soldier: r.id, talent: 'marksman' });
    expect(r.cls).toBeNull();
    expect(leaningClass(r)).toBe('specialist');
  });

  it('retraining gives every point back for credits, and the soldier is a recruit again', () => {
    const { c, s } = campaign();
    expect(run(c, { type: 'retrain', soldier: s.id })).toMatchObject({ ok: false, error: 'No hay nada que reentrenar.' });
    run(c, { type: 'train', soldier: s.id, attr: 'tem' });
    run(c, { type: 'talent', soldier: s.id, talent: 'hawkEye' });
    const credits = c.credits;
    expect(run(c, { type: 'retrain', soldier: s.id }).ok).toBe(true);
    expect(c.credits).toBe(credits - RETRAIN_COST);
    expect(s.cls).toBeNull();
    expect(s.attrs).toEqual(baseAttrs(null));
    expect(s.talents).toEqual({});
    expect(pointsToSpend(s)).toEqual({ attrs: 2, talents: 2 });
  });
});

describe('talents in combat', () => {
  it('llave doble: Codo con codo, Parapeto and Fuego cruzado count only the partner’s soldiers', () => {
    const s = field();
    const shooter = addUnit(s, 's1', 'assault', 2, 10, { owner: 0, mods: { ...soldierStats(build('assault', 1, { sideBySide: 1 })).mods } });
    const target = addUnit(s, 'a1', 'trooper', 9, 10);
    const label = (name: string) => previewShot(s, shooter, target)!.mods.some((m) => m.label === name);
    addUnit(s, 's2', 'specialist', 3, 11, { owner: 0 });
    expect(label('Codo con codo')).toBe(false);
    addUnit(s, 's3', 'grenadier', 3, 9, { owner: 1 });
    expect(label('Codo con codo')).toBe(true);

    const spotter = addUnit(s, 's4', 'sharpshooter', 2, 2, { owner: 0, mods: { ...shooter.mods, pairAim: 0, spotterAim: 15 } });
    expect(previewShot(s, spotter, target)!.mods.some((m) => m.label === 'Fuego cruzado')).toBe(false);
    addUnit(s, 's5', 'assault', 10, 12, { owner: 1 });
    expect(previewShot(s, spotter, target)!.mods).toContainEqual({ label: 'Fuego cruzado', value: 15 });

    const bulwark = addUnit(s, 's6', 'grenadier', 14, 10, { owner: 0, mods: { ...shooter.mods, pairAim: 0, pairDefense: 15 } });
    const alien = addUnit(s, 'a2', 'trooper', 18, 10);
    expect(previewShot(s, alien, bulwark)!.mods.some((m) => m.label === 'Parapeto')).toBe(false);
    addUnit(s, 's7', 'specialist', 14, 13, { owner: 1 });
    expect(previewShot(s, alien, bulwark)!.mods).toContainEqual({ label: 'Parapeto', value: -15 });
  });

  it('Serenidad: rifle shots never graze', () => {
    const s = field();
    const sniper = addUnit(s, 's1', 'sharpshooter', 2, 10);
    const target = addUnit(s, 'a1', 'trooper', 12, 10, { hunkered: true });
    expect(previewShot(s, sniper, target)!.graze).toBeGreaterThan(0);
    sniper.perks = ['serenity'];
    expect(previewShot(s, sniper, target)!.graze).toBe(0);
  });

  it('Paciencia and Trípode soften the reaction penalty', () => {
    const s = field();
    const watcher = addUnit(s, 's1', 'grenadier', 2, 10);
    const target = addUnit(s, 'a1', 'trooper', 8, 10);
    const penalty = () => previewShot(s, watcher, target, { reaction: true })!.mods.find((m) => m.label === 'Fuego de reacción')?.value ?? 0;
    const full = penalty();
    watcher.mods = { ...watcher.mods, reactionKeep: 30 };
    expect(full).toBeLessThan(0);
    expect(penalty()).toBe(0);
  });

  it('Detonación controlada spares the squad; Brazo largo throws farther', () => {
    const s = field();
    const grenadier = addUnit(s, 's1', 'grenadier', 2, 10, { perks: ['safeBlast'], mods: { ...soldierStats(build('grenadier', 3, { longArm: 2 })).mods } });
    const ally = addUnit(s, 's2', 'assault', 16, 10, { owner: 1 });
    const enemy = addUnit(s, 'a1', 'trooper', 16, 11, { hp: 20, maxHp: 20 });
    expect(canTargetTile(s, grenadier, 'launch', { x: 16, y: 10, z: 0 })).toBe(true);
    expect(canTargetTile(s, { ...grenadier, mods: { ...grenadier.mods, grenadeRange: 0 } }, 'launch', { x: 17, y: 10, z: 0 })).toBe(false);
    const sim = new Sim(s, new Rng(1), [0, 1]);
    sim.perform(grenadier, 'launch', { tile: { x: 16, y: 10, z: 0 } });
    expect(ally.hp).toBe(ally.maxHp);
    expect(enemy.hp).toBeLessThan(20);
  });

  it('Temple 7 never panics at the scream; Temple 10 («Nervios de acero») is out of its reach', () => {
    const effects = new Set<string>();
    for (let seed = 1; seed < 300; seed++) {
      const s = field();
      s.activeTeam = 'alien';
      const soldier = addUnit(s, 's1', 'assault', 2, 2, { will: 0, mods: { ...soldierStats(build('assault', 0)).mods, psiGuard: 1 } });
      const sectoid = addUnit(s, 'a1', 'sectoid', 9, 2, { podId: 'p1', ap: 2 });
      s.pods.push({ id: 'p1', active: true, unitIds: ['a1'], patrol: [], patrolIndex: 0 });
      const sim = new Sim(s, new Rng(seed), [0]);
      sim.perform(sectoid, 'mindspin', { target: soldier });
      const psi = sim.events.find((e) => e.t === 'psi') as Extract<GameEvent, { t: 'psi' }>;
      if (psi.effect) effects.add(psi.effect);
    }
    expect([...effects]).toEqual(['disoriented']);

    const s = field();
    const steel = addUnit(s, 's1', 'assault', 2, 2, { will: 0, mods: { ...soldierStats(build('assault', 0, {}, { tem: 10 })).mods } });
    const sectoid = addUnit(s, 'a1', 'sectoid', 9, 2, { ap: 2 });
    expect(steel.mods.psiGuard).toBe(2);
    expect(psiChance(steel, s)).toBe(0);
    expect(abilityTargets(s, sectoid, 'mindspin')).not.toContain(steel);
  });
});
