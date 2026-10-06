import { describe, expect, it } from 'vitest';
import { createCampaign, gainXp } from '../src/campaign';
import { STORY } from '../src/lore';
import { LEVEL_XP } from '../src/progression';
import { Rng } from '../src/rng';
import { nextScene, sceneLines, SCENES, SPEAKERS, STORY_RADIO, unlockedScenes, type SceneId } from '../src/transmissions';

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

  it('a new campaign unlocks only the welcome', () => {
    const c = createCampaign([0, 1], new Rng(1));
    expect(ids(c)).toEqual(['welcome']);
    expect(c.uid).toBeTruthy();
  });

  it('scenes follow the story: chapters, levels and Simón', () => {
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
});
