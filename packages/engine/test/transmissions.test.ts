import { describe, expect, it } from 'vitest';
import { addPlayer, createCampaign, gainXp } from '../src/campaign';
import { ENEMY_FILES, EXPEDIENTE, STORY } from '../src/lore';
import { LEVEL_XP } from '../src/progression';
import { Rng } from '../src/rng';
import { nextScene, RADIO_TIPS, recapLines, sceneLines, SCENES, SPEAKERS, STORY_RADIO, unlockedScenes, type SceneId } from '../src/transmissions';

const ids = (c: Parameters<typeof unlockedScenes>[0]) => unlockedScenes(c).map((s) => s.id);

describe('transmissions', () => {
  it('every scene has a unique id, known speakers and short lines', () => {
    expect(new Set(SCENES.map((s) => s.id)).size).toBe(SCENES.length);
    for (const s of SCENES) {
      expect(s.lines.length).toBeGreaterThan(0);
      expect(s.lines.length).toBeLessThanOrEqual(7);
      for (const l of s.lines) expect(SPEAKERS[l.who]).toBeDefined();
    }
    expect(STORY_RADIO).toHaveLength(STORY.length);
  });

  it('a new campaign starts with no radio: the welcome comes back with the first mission', () => {
    const c = createCampaign([0, 1], new Rng(1));
    expect(ids(c)).toEqual([]);
    c.story.done = 1;
    expect(ids(c)).toEqual(['welcome']);
    expect(c.uid).toBeTruthy();
  });

  it('scenes follow the story: chapters, levels and Simão', () => {
    const c = createCampaign([0], new Rng(1));
    c.story.done = 1;
    gainXp(c, LEVEL_XP[1]! - c.xp, new Rng(1));
    expect(ids(c)).toEqual(['welcome', 'ch1After', 'ch2Before']);
    c.event = 'simon';
    expect(ids(c)).toContain('simon');
    const ch5 = SCENES.find((s) => s.id === 'ch5After')!;
    c.story.simon = 'refused';
    expect(sceneLines(ch5, c).some((l) => l.who === 'simon')).toBe(false);
    c.story.simon = 'trusted';
    expect(sceneLines(ch5, c).some((l) => l.who === 'simon')).toBe(true);
  });

  it('one scene per moment, in story order; urgent ones play at once', () => {
    const c = createCampaign([0], new Rng(1));
    c.story.done = 1;
    gainXp(c, LEVEL_XP[1]! - c.xp, new Rng(1));
    const seen = new Set<SceneId>(['welcome']);
    expect(nextScene(c, seen, false)).toBeNull();
    expect(nextScene(c, seen, true)?.id).toBe('ch1After');
    seen.add('ch1After');
    expect(nextScene(c, seen, true)?.id).toBe('ch2Before');
    c.event = 'simon';
    expect(nextScene(c, seen, false)?.id).toBe('simon');
  });

  it("Morse's warnings say something and the rule behind it", () => {
    for (const tip of Object.values(RADIO_TIPS)) {
      expect(SPEAKERS[tip.who]).toBeDefined();
      expect(tip.line.length).toBeGreaterThan(5);
      expect(tip.rule.length).toBeGreaterThan(tip.line.length);
    }
  });

  it('the recap tells a late commander where things stand and who is theirs', () => {
    const c = createCampaign([0], new Rng(1));
    c.day = 12;
    addPlayer(c, 1, new Rng(2));
    const lines = recapLines(c, 1);
    expect(lines[0]!.text).toContain('día 12');
    const mine = c.soldiers.filter((s) => s.owner === 1);
    expect(mine).toHaveLength(4);
    for (const s of mine) expect(lines.map((l) => l.text).join(' ')).toContain(s.name);
    // Whoever was already there gets a pop-up naming them.
    expect(c.log[0]!.alert?.kicker).toBe('RELEVO');
    expect(c.log[0]!.text).toContain(mine[0]!.name);
  });
});

describe('the song of Velmar', () => {
  it('the Taller de oficios and the first soldier who learns a trade unlock their scenes', () => {
    const c = createCampaign([0], new Rng(1));
    expect(ids(c)).not.toContain('velmar');
    (c.base.plots[0] as { building: string | null }).building = 'echoes';
    expect(ids(c)).toContain('velmar');
    expect(ids(c)).not.toContain('firstSong');
    (c.soldiers[0]!.talents as Record<string, number>).bolt = 1;
    expect(ids(c)).toContain('firstSong');
    expect(RADIO_TIPS.song.rule).toMatch(/ruido/);
  });
});

describe('Expediente', () => {
  it('every page has a one-line summary for the level-up pop-up', () => {
    for (const page of EXPEDIENTE) {
      expect(page.summary.length).toBeGreaterThan(20);
      expect(page.summary.length).toBeLessThanOrEqual(110);
    }
  });
});

describe('enemy files', () => {
  it('every enemy kind has its file', () => {
    for (const id of ['trooper', 'officer', 'xenoid', 'lancer', 'mec', 'sectoid', 'zombie', 'relay'] as const) {
      expect(ENEMY_FILES[id]?.what).toBeTruthy();
      expect(ENEMY_FILES[id]?.does).toBeTruthy();
      expect(ENEMY_FILES[id]?.tip).toBeTruthy();
    }
  });
});
