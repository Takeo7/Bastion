import { describe, expect, it } from 'vitest';
import { abilitiesOf, abilityBlocker } from '../src/abilities';
import {
  applyMissionResult,
  buildSquad,
  craftBlocker,
  createCampaign,
  executeCampaignCommand,
  gainXp,
  migrateCampaign,
  squadMember,
  type CampaignCommand,
  type CampaignState,
} from '../src/campaign';
import { attackWeapon, clipOf, hasSquadsight, PISTOL_CLIP } from '../src/content';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { CRAFTS, LEVEL_XP, spareMags } from '../src/progression';
import { Rng } from '../src/rng';
import { executeCommand } from '../src/rules';
import { createMission } from '../src/setup';
import type { Arms, GameState, Pod, Slot, Unit } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// The weapons from zero (docs/FASE8.md §A): the class no longer brings a weapon.

const run = (c: CampaignState, cmd: CampaignCommand, slot: Slot = 0) => executeCampaignCommand(c, cmd, { slot, connected: [0], rng: new Rng(7) });
const ctx = () => ({ slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(1) });
const of = <T extends GameEvent['t']>(events: GameEvent[], t: T) => events.filter((e): e is Extract<GameEvent, { t: T }> => e.t === t);

/** A field with a zombie asleep far away, so the mission goes on, and an armed soldier. */
function field(arms: Arms, extra: Partial<Unit> = {}): { s: GameState; u: Unit } {
  const s = stateFrom(Array.from({ length: 20 }, () => '.'.repeat(20)));
  const z = addUnit(s, 'a1', 'zombie', 19, 19);
  const pod: Pod = { id: 'p1', active: false, unitIds: [z.id], patrol: [], patrolIndex: 0 };
  z.podId = pod.id;
  s.pods.push(pod);
  const u = addUnit(s, 's1', 'rookie', 5, 5, { arms, mags: 0, ...extra });
  u.ammo = clipOf(u, 'shoot');
  if (arms.primary) u.pistolAmmo = PISTOL_CLIP;
  return { s, u };
}

describe('the weapons from zero', () => {
  it('a new campaign: knife and pistol, nothing else, and no spare magazines', () => {
    const c = createCampaign([0], new Rng(1));
    c.squad = c.soldiers.slice(0, 4).map((x) => x.id);
    expect(c.foundWeapons ?? []).toEqual([]);
    const m = buildSquad(c)[0]!;
    expect(m).toMatchObject({ arms: { primary: null, melee: null }, mags: 0, weaponName: 'Pistola' });
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 3 }, slots: [0], rng: new Rng(1), squad: buildSquad(c) });
    const u = s.units.find((x) => x.team === 'xcom')!;
    expect(attackWeapon(u, 'shoot').id).toBe('pistol');
    expect(attackWeapon(u, 'slash').id).toBe('knife');
    expect(u).toMatchObject({ ammo: PISTOL_CLIP, mags: 0 });
    expect(u.pistolAmmo).toBeUndefined();
    expect(abilitiesOf(u)).toEqual(expect.arrayContaining(['shoot', 'slash']));
    expect(abilitiesOf(u)).not.toContain('pistol');
  });

  it('the pistol counts its rounds; beside a primary it has its own', () => {
    const bare = field({ primary: null, melee: null });
    addUnit(bare.s, 'a2', 'zombie', 8, 5);
    expect(executeCommand(bare.s, { type: 'ability', unit: 's1', ability: 'shoot', target: 'a2' }, ctx()).ok).toBe(true);
    expect(bare.u.ammo).toBe(PISTOL_CLIP - 1);
    bare.u.ammo = 0;
    bare.u.ap = 2;
    expect(abilityBlocker(bare.s, bare.u, 'shoot')).toBe('Sin munición y sin cargadores.');

    const { s, u } = field({ primary: 'assaultRifle', primaryId: 'w1', melee: null }, { mags: 1 });
    addUnit(s, 'a2', 'zombie', 8, 5);
    expect(abilitiesOf(u)).toContain('pistol');
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'pistol', target: 'a2' }, ctx()).ok).toBe(true);
    expect(u.pistolAmmo).toBe(PISTOL_CLIP - 1);
    expect(u.ammo).toBe(clipOf(u, 'shoot'));
    // The rifle is full: reloading fills the pistol.
    u.ap = 2;
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'reload' }, ctx()).ok).toBe(true);
    expect(u).toMatchObject({ pistolAmmo: PISTOL_CLIP, mags: 0 });
    expect(abilityBlocker(s, u, 'reload')).toBe('Los cargadores están llenos.');
  });

  it('the far shot is the Francotirador with a fusil de precisión', () => {
    const { s } = field({ primary: null, melee: null });
    const sharp = addUnit(s, 's2', 'sharpshooter', 2, 2, { arms: { primary: 'assaultRifle', melee: null } });
    expect(hasSquadsight(sharp)).toBe(false);
    sharp.arms = { primary: 'precisionRifle', melee: null };
    expect(hasSquadsight(sharp)).toBe(true);
    expect(hasSquadsight(addUnit(s, 's3', 'rookie', 3, 3, { arms: { primary: 'scopedRifle', melee: null } }))).toBe(false);
    // Without arms of its own (a skirmish), its class's.
    expect(hasSquadsight(addUnit(s, 's4', 'sharpshooter', 4, 4))).toBe(true);
  });

  it('anyone takes any weapon: one in the hand and one at the belt; Mano de machete wants the machete', () => {
    const c = createCampaign([0], new Rng(1));
    const s = c.soldiers[0]!;
    s.board = ['bladeMaster'];
    c.foundWeapons = [
      { id: 'w1', kind: 'pumpShotgun', holder: null },
      { id: 'w2', kind: 'machete', holder: null },
      { id: 'w3', kind: 'assaultRifle', holder: null },
    ];
    const knife = squadMember(c, s).mods;
    for (const w of ['w1', 'w2']) expect(run(c, { type: 'assignWeapon', weapon: w, soldier: s.id }).ok).toBe(true);
    expect(squadMember(c, s)).toMatchObject({ arms: { primary: 'pumpShotgun', primaryId: 'w1', melee: 'machete', meleeId: 'w2' }, weaponName: 'Escopeta' });
    expect(squadMember(c, s).mods.meleeDamage - knife.meleeDamage).toBe(2);
    expect(squadMember(c, s).mods.meleeAim - knife.meleeAim).toBe(15);
    // A second primary takes the first one's place; the machete stays.
    expect(run(c, { type: 'assignWeapon', weapon: 'w3', soldier: s.id }).ok).toBe(true);
    expect(c.foundWeapons.map((w) => w.holder)).toEqual([null, s.id, s.id]);
  });

  it('the Taller makes weapons again and again, and munition three times; the Bastion\'s level brings some', () => {
    const c = createCampaign([0], new Rng(1));
    c.base.plots.find((p) => p.terrain === 'clear' && !p.building)!.building = 'workshop';
    c.credits = 999;
    c.parts = 99;
    expect(craftBlocker(c, 'makeRifle')).toBeNull();
    expect(craftBlocker(c, 'makeSniper')).toBe('Hace falta el nivel 3 del Bastión.');
    expect(craftBlocker(c, 'ammo2')).toBe('Hace falta el nivel 3 del Bastión.');
    c.crafted = ['ammo1'];
    expect(spareMags(c)).toBe(1);
    expect(CRAFTS.makeRifle.makes).toBe('assaultRifle');
    // Made once, it can be made again.
    c.crafted.push('makeRifle');
    expect(craftBlocker(c, 'makeRifle')).toBeNull();
    expect(craftBlocker(c, 'ammo1')).toBe('Ya está hecho.');
    gainXp(c, LEVEL_XP[2]! - c.xp, new Rng(1));
    expect((c.foundWeapons ?? []).map((w) => w.kind).sort()).toEqual(['assaultRifle', 'machete', 'precisionRifle', 'pumpShotgun']);
  });

});

describe('a weapon found in the mission (docs/FASE8.md §C)', () => {
  it('is equipped for an action; the one in hand goes into the backpack, and a client replays it', () => {
    const { s, u } = field({ primary: 'assaultRifle', primaryId: 'w1', melee: null }, { backpack: [{ weapon: 'sawnOff' }] });
    u.ammo = 1;
    const client = cloneState(s);
    const r = executeCommand(s, { type: 'item', unit: 's1', action: 'equip', index: 0 }, ctx());
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(of(r.events, 'weaponEquipped')).toHaveLength(1);
    expect(u.arms).toEqual({ primary: 'sawnOff', melee: null });
    expect(u.ammo).toBe(clipOf(u, 'shoot'));
    expect(u.backpack).toEqual([{ weapon: 'assaultRifle', weaponId: 'w1' }]);
    expect(u.ap).toBe(1);
    for (const e of r.events) applyEvent(client, e);
    expect(client).toEqual(s);
    // Only weapons, and only a campaign soldier.
    u.backpack!.push({ food: 2 });
    expect(executeCommand(s, { type: 'item', unit: 's1', action: 'equip', index: 1 }, ctx())).toEqual({ ok: false, error: 'Solo se equipan las armas.' });
  });

  it('without a primary, the pistol keeps its rounds beside the new gun', () => {
    const { s, u } = field({ primary: null, melee: null }, { backpack: [{ weapon: 'machineGun' }] });
    u.ammo = 2;
    expect(executeCommand(s, { type: 'item', unit: 's1', action: 'equip', index: 0 }, ctx()).ok).toBe(true);
    expect(u).toMatchObject({ pistolAmmo: 2, ammo: clipOf(u, 'shoot'), backpack: [] });
    expect(abilitiesOf(u)).toContain('pistol');
  });

  it('back home, the find is the soldier\'s; its own comes back to the rack in a backpack, or is lost on the floor', () => {
    const c = createCampaign([0], new Rng(1));
    const [a, b] = c.soldiers;
    c.foundWeapons = [
      { id: 'w1', kind: 'assaultRifle', holder: a!.id },
      { id: 'w2', kind: 'pumpShotgun', holder: b!.id },
    ];
    c.squad = [a!.id, b!.id];
    const offer = c.offers.find((o) => !o.story) ?? c.offers[0]!;
    c.activeMission = offer.id;
    const mission = createMission({ map: offer.map, slots: [0], rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    const ua = mission.units.find((u) => u.campaignId === a!.id)!;
    const ub = mission.units.find((u) => u.campaignId === b!.id)!;
    // A swapped its rifle for a hunting rifle and kept it in the backpack; B left its shotgun on the floor for a machine gun.
    Object.assign(ua, { arms: { primary: 'huntingRifle', melee: null }, backpack: [{ weapon: 'assaultRifle', weaponId: 'w1' }] });
    Object.assign(ub, { arms: { primary: 'machineGun', melee: null }, backpack: [] });
    for (const u of mission.units.filter((x) => x.team === 'xcom')) Object.assign(u, { alive: false, evacuated: true });
    mission.outcome = 'victory';
    mission.outcomeReason = 'objective';
    const report = applyMissionResult(c, mission, new Rng(4));
    const held = (id: string) => c.foundWeapons!.filter((w) => w.holder === id).map((w) => w.kind);
    expect(held(a!.id)).toEqual(['huntingRifle']);
    expect(held(b!.id)).toEqual(['machineGun']);
    expect(c.foundWeapons!.find((w) => w.id === 'w1')).toMatchObject({ holder: null });
    expect(c.foundWeapons!.some((w) => w.id === 'w2')).toBe(false);
    // The rifle coming back is not a find.
    expect(report.loot?.weapons ?? []).toEqual([]);
    expect(c.log.some((l) => l.text.includes('vuelve sin su escopeta'))).toBe(true);
  });
});
