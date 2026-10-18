import { describe, expect, it } from 'vitest';
import {
  advanceTime,
  CAMPAIGN_VERSION,
  contactBlocker,
  createCampaign,
  DOOM_MAX,
  executeCampaignCommand,
  FACILITIES,
  floodedZones,
  HOME_REGION,
  mareaThreshold,
  migrateCampaign,
  missionInfo,
  REGIONS,
  roadHops,
  roadRoute,
  ROADS,
  underMarea,
  type CampaignCommand,
  type CampaignState,
  type MissionOffer,
  type RegionId,
} from '../src/campaign';
import { doomName, mareaAlert, missionName } from '../src/lore';
import { LEVEL_XP } from '../src/progression';
import { Rng } from '../src/rng';
import type { Slot } from '../src/types';
import { raise } from './helpers';

// Phase 5: the valley of Kessra (docs/FASE5.md).

const ZONES = Object.keys(REGIONS) as RegionId[];
const run = (c: CampaignState, cmd: CampaignCommand, slot: Slot = 0, connected: Slot[] = [0]) => executeCampaignCommand(c, cmd, { slot, connected, rng: new Rng(7) });

/** A campaign where nothing moves but what is being tested. */
function quiet(): CampaignState {
  const c = createCampaign([0], new Rng(1));
  Object.assign(c, { nextEventDay: 999, nextMissionDay: 999, nextRumorDay: 999, nextFacilityDay: 999, nextSupplyDay: 999, nextDoomDay: 999, nextMedicineDay: 999, nextFoodDay: 999, credits: 5000 });
  // The Sala de radio at its top: the Centro de comunicaciones in it (docs/FASE12.md §11.3).
  raise(c, 'radio', 3);
  return c;
}

/** An operation of its own in `region` (its groups its own too: the copy of another's would move with it). */
const offerIn = (c: CampaignState, region: RegionId): MissionOffer => {
  const like = c.offers.find((o) => !o.story && !o.tide)!;
  return { ...like, pods: like.pods.map((p) => [...p]), id: `t-${region}`, region, story: null, quest: null, facility: null, expiresDay: null };
};
/** Any command the base takes looks at where the Marea is; this one passes no time. */
const look = (c: CampaignState) => run(c, { type: 'advance', ready: false });
/** Days go by until something happens (the clock goes on inside, with the missions: docs/FASE12.md §11.6), and the base looks. */
const wait = (c: CampaignState) => {
  advanceTime(c, new Rng(7));
  look(c);
};

describe('the valley', () => {
  // Lore, 2026-10-13: invented, neutral names.
  it('the zones have their names, and ordinary operations are named for the place they go', () => {
    expect(REGIONS.weu.name).toBe('Brena');
    expect([REGIONS.afr.name, REGIONS.eeu.name, REGIONS.me.name, REGIONS.sa.name]).toEqual(['Corran', 'Velsa', 'Galen', 'Carvik']);
    expect([REGIONS.oce.name, REGIONS.eas.name, REGIONS.na.name]).toEqual(['La Vega', 'El Pantano Gris', 'Los Llanos de Ceniza']);
    let ordinary = 0;
    for (let seed = 1; seed <= 12; seed++) {
      const c = createCampaign([0], new Rng(seed));
      for (const o of c.offers.filter((x) => !x.story && !x.final && !x.quest && x.map.kind === 'generated')) {
        ordinary++;
        expect(o.name).toBe(missionName(o.map.kind === 'generated' ? o.map.biome : 'city', REGIONS[o.region].name, o.map.kind === 'generated' ? o.map.seed : 0, o.map.kind === 'generated' ? o.map.site : undefined));
        expect(o.name).not.toMatch(/Operación/);
        expect(o.name).toContain(REGIONS[o.region].name.replace(/^El /, ''));
      }
      // The opening mission is presented without the Diario label.
      const story = c.offers.find((x) => x.story === 1)!;
      expect(missionInfo(c, story).kicker).toBeUndefined();
      expect(missionInfo(c, story).item).toBe('el maletín de urgencias');
    }
    expect(ordinary).toBeGreaterThan(10);
    expect(FACILITIES.simulation.name).toBe('Sala de mapas');
  });

  it('roads go both ways, join every zone, and are the ones the map draws', () => {
    for (const a of ZONES) for (const b of REGIONS[a].roads) expect(REGIONS[b].roads).toContain(a);
    for (const a of ZONES) for (const b of ZONES) expect(roadHops(a, b)).toBeGreaterThanOrEqual(0);
    expect(ROADS).toHaveLength(10);
    const pairs = ROADS.map(([a, b]) => [a, b].sort().join('-')).sort();
    const drawn = [['weu', 'oce'], ['weu', 'sa'], ['weu', 'eeu'], ['eeu', 'eas'], ['eas', 'oce'], ['oce', 'afr'], ['oce', 'me'], ['afr', 'na'], ['me', 'na'], ['sa', 'afr']].map((p) => p.sort().join('-')).sort();
    expect(pairs).toEqual(drawn);
    // Where they lie on Arturo's map (15/10): 51,2 of its pixels a unit, the middle at (768, 512).
    expect(REGIONS.weu).toMatchObject({ x: -6.11, z: -5.86 });
    expect(REGIONS.na).toMatchObject({ x: 2.87, z: 6.95 });
    // The Marea comes from the south: Los Llanos de Ceniza lie south of every other zone.
    expect(Object.values(REGIONS).every((r) => r.id === 'na' || r.z < REGIONS.na.z)).toBe(true);
  });

  it('stretches and routes by road', () => {
    expect(roadHops('weu', 'weu')).toBe(0);
    expect(roadRoute('weu', 'weu')).toEqual(['weu']);
    expect(roadHops('weu', 'na')).toBe(3);
    expect(roadRoute('weu', 'na')).toEqual(['weu', 'oce', 'afr', 'na']);
    expect(roadRoute('na', 'eeu')).toEqual(['na', 'afr', 'oce', 'weu', 'eeu']);
    expect(roadHops('eeu', 'na')).toBe(4);
  });

});

describe('contacting by road', () => {
  it('only zones a road joins to Brena or to a contacted zone; the reason says where to go first', () => {
    const c = quiet();
    expect(contactBlocker(c, 'afr')).toBe('No hay carretera desde vuestras zonas: contactad antes con La Vega o Carvik.');
    expect(contactBlocker(c, 'na')).toBe('No hay carretera desde vuestras zonas: contactad antes con La Vega.');
    expect(run(c, { type: 'contact', region: 'na' }).ok).toBe(false);
    expect(run(c, { type: 'contact', region: 'oce' }).ok).toBe(true);
    expect(contactBlocker(c, 'afr')).toBeNull();
    expect(run(c, { type: 'contact', region: 'afr' }).ok).toBe(true);
    expect(run(c, { type: 'contact', region: 'na' }).ok).toBe(true);
  });

});

describe('the Marea', () => {
  it('rises zone by zone from Los Llanos as the activity reaches each threshold; Brena only falls with the campaign', () => {
    const c = quiet();
    const at = (doom: number) => floodedZones({ doom }).sort();
    expect(at(3)).toEqual([]);
    expect(at(4)).toEqual(['na']);
    expect(at(6)).toEqual(['afr', 'me', 'na']);
    expect(at(8)).toEqual(['afr', 'me', 'na']);
    expect(at(9)).toEqual(['afr', 'me', 'na', 'oce', 'sa']);
    expect(at(10)).toEqual(['afr', 'eas', 'me', 'na', 'oce', 'sa']);
    expect(at(11)).toEqual(['afr', 'eas', 'eeu', 'me', 'na', 'oce', 'sa']);
    expect(mareaThreshold(HOME_REGION)).toBeNull();
    expect(underMarea({ doom: 11 }, 'weu')).toBe(false);
    // At 12, the campaign is lost, as ever.
    c.doom = DOOM_MAX - 1;
    c.nextDoomDay = c.day + 1;
    wait(c);
    expect(c.outcome).toBe('defeat');
  });

  it('the base hears when a zone falls and when it is freed, in words the Bastion knows; the Marea recedes with the activity', () => {
    const c = quiet();
    const titles = (level: number, ids: RegionId[], reached: boolean) => ids.map((id) => mareaAlert(level, REGIONS[id].name, reached).title).sort();
    const alerts = (kicker: string) => c.log.filter((l) => l.alert?.kicker === kicker).map((l) => l.alert!.title).sort();
    // Before level 4, nobody has a word for the Marea: the dead take the zones.
    c.doom = 6;
    look(c);
    expect(alerts(doomName(1))).toEqual(titles(1, ['afr', 'me', 'na'], true));
    expect(alerts(doomName(1))[0]).toMatch(/Los muertos toman/);
    expect(c.flooded!.sort()).toEqual(['afr', 'me', 'na']);
    c.doom = 5;
    look(c);
    expect(c.log[0]!.alert?.title).toMatch(/se retira/);
    expect(c.flooded).toEqual(['na']);
    // From level 4, the Marea by its name.
    c.xp = LEVEL_XP[3]!;
    c.doom = 9;
    look(c);
    expect(alerts(doomName(4))).toEqual(titles(4, ['afr', 'me', 'oce', 'sa'], true));
    expect(alerts(doomName(4))[0]).toMatch(/La Marea llega/);
    c.doom = 8;
    look(c);
    expect(c.log[0]!.alert?.title).toMatch(/se retira/);
  });

  it('a zone under the Marea sends nothing, can’t be newly contacted, and its missions bring one more group until it is freed', () => {
    const c = quiet();
    run(c, { type: 'contact', region: 'oce' });
    c.doom = 9;
    // Nothing from La Vega: no supplies, medicines or food.
    c.nextSupplyDay = c.day + 1;
    c.nextMedicineDay = c.day + 1;
    c.nextFoodDay = c.day + 1;
    const credits = c.credits;
    const medicines = c.medicines!;
    wait(c);
    expect(c.credits).toBe(credits + 60 + 30);
    expect(c.medicines).toBe(medicines);
    expect(c.log.some((l) => /La Vega manda/.test(l.text))).toBe(false);
    // Not to be contacted while under it.
    expect(contactBlocker(c, 'sa')).toMatch(/La Marea cubre|Los muertos han tomado/);
    // An operation there: one more group of the dead (the difficulty stays), gone when the zone is freed.
    const o = offerIn(c, 'oce');
    o.difficulty = 2;
    const pods = o.pods.length;
    c.offers.push(o);
    look(c);
    expect(o).toMatchObject({ difficulty: 2, tide: true });
    expect(o.pods).toHaveLength(pods + 1);
    c.doom = 8;
    look(c);
    expect(o.difficulty).toBe(2);
    expect(o.pods).toHaveLength(pods);
    expect(o.tide).toBeUndefined();
  });

  it('Nidos gather under the Marea or beside it', () => {
    const near = ['na', 'afr', 'me', 'oce', 'sa'];
    let close = 0;
    const n = 300;
    for (let seed = 1; seed <= n; seed++) {
      const c = createCampaign([0], new Rng(seed));
      Object.assign(c, { nextEventDay: 999, nextMissionDay: 999, nextRumorDay: 999, nextSupplyDay: 999, nextDoomDay: 999, doom: 6, nextFacilityDay: c.day + 1 });
      wait(c);
      const nest = c.regions.find((r) => r.facility);
      if (nest && near.includes(nest.id)) close++;
    }
    // Three times as often: 15 of 18 weights, against 5 of 8 zones if they were even.
    expect(close / n).toBeGreaterThan(0.75);
  });
});
