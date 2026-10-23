// What each level of the Bastion brings, and which of it this player hasn't
// looked at yet (playtest 20/10 #13): what comes with a level shows «Nuevo»
// where it lives (a piece in Construir's palette, a building's upgrade on its
// card, a belt item, an armour or a work in the Armería) until the pointer goes
// over it. Per player and campaign, in this browser. Nothing of a level not
// reached is shown anywhere: the screens leave it out (screen.ts, buildMode.ts).
import { CRAFTS, ITEMS, PIECES, TECHS, upgradeLevel, type CraftDef, type PieceId } from '@bastion/engine';
import { storageKey } from '../net';
import { h } from '../ui/dom';

/** Where a thing a level brings is found: Construir (the palette, a building's card) or the Armería. */
export type FreshView = 'facilities' | 'engineering';

export interface FreshThing {
  key: string;
  view: FreshView;
  /** A building's upgrade: only news where there is one of that building to raise to it (`piece` at `to - 1`). */
  raise?: { piece: PieceId; to: number };
}

/** The Bastion level a work of the Taller needs: its technology's, else its own. */
export function craftLevel(def: CraftDef): number {
  return def.tech ? TECHS[def.tech].level : (def.level ?? 1);
}

/** The armour tiers made in the Taller, by the Armería's row keys, and the level each needs. */
export const ARMOR_LEVELS: Record<string, number> = { a1: 1, a2: TECHS.plateArmor.level, a3: TECHS.predatorArmor.level };

/** Everything `level` brings, keyed as the screens mark it. */
export function levelThings(level: number): FreshThing[] {
  const out: FreshThing[] = [];
  for (const def of Object.values(PIECES)) {
    if (def.id === 'caravan') continue;
    if (def.level === level) out.push({ key: `piece:${def.id}`, view: 'facilities' });
    if (def.kind === 'building') for (const to of [2, 3]) if (upgradeLevel(def.level, to) === level) out.push({ key: `raise:${def.id}:${to}`, view: 'facilities', raise: { piece: def.id, to } });
  }
  // Only the belt's: the Armería shows nothing else, and an ammunition of level 2 (now the weapons' improvements) was
  // new for ever, its «Nuevo» on the Armería's tab with nowhere to see it (Ana, 23/10 #39).
  for (const item of Object.values(ITEMS)) if (item.level === level && item.slot === 'utility') out.push({ key: `item:${item.id}`, view: 'engineering' });
  for (const [key, at] of Object.entries(ARMOR_LEVELS)) if (at === level && at > 1) out.push({ key: `armor:${key}`, view: 'engineering' });
  for (const def of Object.values(CRAFTS)) if (def.kind !== 'armor' && craftLevel(def) === level && level > 1) out.push({ key: `craft:${def.id}`, view: 'engineering' });
  return out;
}

const KEY = storageKey('fresh');

/** This player's marks for one campaign: from which level things count as new, and those already looked at. */
export class FreshMarks {
  private uid: string | null = null;
  private from = 1;
  private readonly seen = new Set<string>();

  /**
   * This campaign's marks, read once: a campaign first opened in this browser starts with nothing new, only what
   * the levels bought from now on bring.
   */
  load(uid: string, level: number): void {
    if (this.uid === uid) return;
    this.uid = uid;
    this.seen.clear();
    let stored: { from: number; seen: string[] } | undefined;
    try {
      stored = (JSON.parse(localStorage.getItem(KEY) ?? '{}') as Record<string, { from: number; seen: string[] }>)[uid];
    } catch {
      /* storage unavailable: nothing is new until a level is bought */
    }
    this.from = stored?.from ?? level;
    for (const k of stored?.seen ?? []) this.seen.add(k);
    if (!stored) this.save();
  }

  /** What is new up to `level`, not looked at yet. */
  fresh(level: number): FreshThing[] {
    const out: FreshThing[] = [];
    for (let n = this.from + 1; n <= level; n++) out.push(...levelThings(n).filter((t) => !this.seen.has(t.key)));
    return out;
  }

  isFresh(key: string, level: number): boolean {
    return this.fresh(level).some((t) => t.key === key);
  }

  /** Looked at: its «Nuevo» goes. Whether it was new. */
  see(key: string): boolean {
    if (this.seen.has(key)) return false;
    this.seen.add(key);
    this.save();
    return true;
  }

  private save(): void {
    if (this.uid === null) return;
    try {
      const all = JSON.parse(localStorage.getItem(KEY) ?? '{}') as Record<string, { from: number; seen: string[] }>;
      all[this.uid] = { from: this.from, seen: [...this.seen] };
      localStorage.setItem(KEY, JSON.stringify(all));
    } catch {
      /* storage unavailable: the marks come back on reload */
    }
  }
}

/** «Nuevo» on `el`, after its name (or in its button), while what it shows is new; the pointer over it, and it goes (`see`). */
export function markNew(el: HTMLElement, fresh: boolean, see: () => void): HTMLElement {
  if (!fresh) return el;
  const tag = h('span.new-tag', {}, 'Nuevo');
  (el.querySelector('b') ?? el.querySelector('button') ?? el).append(tag);
  el.addEventListener(
    'pointerenter',
    () => {
      tag.remove();
      see();
    },
    { once: true },
  );
  return el;
}
