import { levelCount, type GameState } from '@bastion/engine';

// What the squad has seen this mission, tile by tile: the memory behind the
// fog of war. It knows nothing about how the map is drawn (any renderer can
// read `tiles` and the sight mask), and it lasts for the tab: a reload of the
// page rejoins the same mission with what was already explored.

const STORAGE_KEY = 'bastion.explored';

/** Which mission a state belongs to. */
const missionKey = (s: GameState) => `${s.mapId}|${s.campaignMission?.id ?? 'skirmish'}|${s.width}x${s.height}`;

export class ExploredMemory {
  /** 1 for every place seen this mission (index: the engine's node, the ground's tiles first, then each storey's). */
  tiles: Uint8Array = new Uint8Array(0);
  private mission = '';
  /** The seq of the state when the squad last looked. */
  private seq = 0;

  /**
   * Starts the memory for the mission of `s`, or keeps it for a snapshot of the
   * same one (a resync). A later seq than the snapshot's is another mission on
   * the same map: that one starts unexplored.
   */
  begin(s: GameState): void {
    const key = missionKey(s);
    const size = s.width * s.height * levelCount(s);
    if (key !== this.mission || s.seq < this.seq || this.tiles.length !== size) this.tiles = load(key, s.seq, size) ?? new Uint8Array(size);
    this.mission = key;
    this.seq = s.seq;
  }

  /** Adds the places in sight (shared by the whole squad; a mask by place, as teamVisibility); true if any of them is new. */
  see(visible: readonly boolean[], seq: number): boolean {
    let found = false;
    for (let i = 0; i < this.tiles.length; i++) {
      if (visible[i] && !this.tiles[i]) {
        this.tiles[i] = 1;
        found = true;
      }
    }
    this.seq = Math.max(this.seq, seq);
    if (found) save(this.mission, this.seq, this.tiles);
    return found;
  }
}

function load(key: string, seq: number, size: number): Uint8Array | null {
  try {
    const saved = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? 'null') as { key: string; seq: number; tiles: string } | null;
    if (!saved || saved.key !== key || saved.seq > seq || saved.tiles.length !== size) return null;
    return Uint8Array.from(saved.tiles, (c) => (c === '1' ? 1 : 0));
  } catch {
    return null;
  }
}

function save(key: string, seq: number, tiles: Uint8Array): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ key, seq, tiles: tiles.join('') }));
  } catch {
    // Storage full or blocked: the memory then lasts until the page reloads.
  }
}
