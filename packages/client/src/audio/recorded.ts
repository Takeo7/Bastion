import { audio, type PlayOptions } from './engine';

// Recorded sounds (docs/SONIDO.md: CC0 from Freesound and OpenGameArt), cut
// and converted into public/audio/recorded/ by tools/sound/convert.mjs, which
// writes the index this reads: for each group (a zombie's growl, a soldier's
// scream…), its takes and how loud each sits. A group plays one of its takes at
// random, never the one it played last. Until the index and the takes are in
// (or with no files at all), nothing plays and callers keep their own sound.
//
// Short takes are decoded once into memory; the long beds the music lays under
// combat (music.ts) stream from the page instead (bed()), a few minutes of
// stereo being far too much to hold decoded.

/** public/audio/recorded/index.json: group → takes (file under recorded/, level in dB). */
type Index = Record<string, { file: string; db?: number }[]>;

const BASE = '/audio/recorded/';
let index: Index | null = null;
let indexing: Promise<void> | null = null;
const buffers = new Map<string, AudioBuffer>();
const loading = new Set<string>();
const last = new Map<string, string>();

function loadIndex(): Promise<void> {
  indexing ??= fetch(`${BASE}index.json`)
    .then((r) => (r.ok ? (r.json() as Promise<Index>) : null))
    .then((i) => {
      index = i;
    })
    .catch(() => {
      /* no recordings: the game's own sounds */
    });
  return indexing;
}

/** Fetches and decodes the short takes once the context exists (beds stream, they are not loaded here). */
export function loadRecorded(): void {
  audio.onReady(() => {
    void loadIndex().then(() => {
      const ctx = audio.ctx;
      if (!index || !ctx) return;
      for (const [group, takes] of Object.entries(index)) {
        if (group.startsWith('bed.')) continue;
        for (const t of takes) {
          if (buffers.has(t.file) || loading.has(t.file)) continue;
          loading.add(t.file);
          fetch(BASE + t.file)
            .then((r) => r.arrayBuffer())
            .then((data) => ctx.decodeAudioData(data))
            .then((buf) => buffers.set(t.file, buf))
            .catch(() => {
              /* that take is left out */
            })
            .finally(() => loading.delete(t.file));
        }
      }
    });
  });
}

/** Whether `group` has a take ready to play. */
export function hasRecorded(group: string): boolean {
  return !!index?.[group]?.some((t) => buffers.has(t.file));
}

/**
 * Plays one take of `group` (never the one before, when there are others) and
 * returns true, or false when there is none ready: the caller plays its own.
 */
export function playRecorded(group: string, o: PlayOptions & { spread?: number } = {}): boolean {
  const ready = index?.[group]?.filter((t) => buffers.has(t.file)) ?? [];
  if (!ready.length || !audio.live()) return false;
  const fresh = ready.length > 1 ? ready.filter((t) => t.file !== last.get(group)) : ready;
  const take = fresh[Math.floor(Math.random() * fresh.length)]!;
  last.set(group, take.file);
  const spread = o.spread ?? 0.04;
  audio.play(buffers.get(take.file)!, {
    ...o,
    bus: o.bus ?? 'sfx',
    gain: (o.gain ?? 1) * Math.pow(10, (take.db ?? 0) / 20),
    rate: (o.rate ?? 1) * (1 + (Math.random() * 2 - 1) * spread),
  });
  return true;
}

/** A bed the music lays down (music.ts): one of `group`'s files at random, to stream, or null if there is none. */
export async function bed(group: string): Promise<{ url: string; db: number } | null> {
  await loadIndex();
  const takes = index?.[group];
  if (!takes?.length) return null;
  const t = takes[Math.floor(Math.random() * takes.length)]!;
  return { url: BASE + t.file, db: t.db ?? 0 };
}
