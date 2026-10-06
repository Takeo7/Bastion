// Style votes for the comparison page. Published on claude.ai, votes live in
// the artifact's shared store (one document per person at `votes/<id>`,
// readable by everyone, writable only by its owner); anywhere else (the dev
// server) they stay in this browser.

export interface Rating {
  /** 1–5. */
  score: number;
  note: string;
}

/** A person's ratings, keyed by style id. */
export type Ratings = Record<string, Rating>;

export interface Person {
  name: string;
  color: string;
}

export interface VoteStore {
  /** Votes are shared with everyone who opens the page. */
  readonly shared: boolean;
  /** The viewer's id (a stable local key when not shared). */
  readonly me: string;
  /** False once the platform refused this viewer's writes. */
  readonly canWrite: boolean;
  onChange(fn: (people: Map<string, Ratings>) => void): void;
  /** Saves the viewer's whole set of ratings (coalesced, one write at a time). */
  save(ratings: Ratings): void;
  people(ids: string[]): Promise<Record<string, Person>>;
}

const LOCAL_KEY = 'bastion.cmp.votes';

/** Keeps only well-formed ratings: shared data is untrusted. */
function clean(raw: unknown): Ratings {
  const out: Ratings = {};
  if (!raw || typeof raw !== 'object') return out;
  for (const [id, r] of Object.entries(raw as Record<string, unknown>)) {
    if (!r || typeof r !== 'object' || !/^[\w-]{1,40}$/.test(id)) continue;
    const score = Math.round(Number((r as Rating).score));
    const note = typeof (r as Rating).note === 'string' ? (r as Rating).note.slice(0, 280) : '';
    if (score >= 1 && score <= 5) out[id] = { score, note };
  }
  return out;
}

class LocalStore implements VoteStore {
  readonly shared = false;
  readonly me = 'local';
  readonly canWrite = true;
  private listeners: ((people: Map<string, Ratings>) => void)[] = [];
  private ratings: Ratings = {};

  constructor() {
    try {
      this.ratings = clean(JSON.parse(localStorage.getItem(LOCAL_KEY) ?? '{}'));
    } catch {
      /* storage unavailable: start empty */
    }
  }

  onChange(fn: (people: Map<string, Ratings>) => void): void {
    this.listeners.push(fn);
    fn(new Map([[this.me, this.ratings]]));
  }

  save(ratings: Ratings): void {
    this.ratings = clean(ratings);
    try {
      localStorage.setItem(LOCAL_KEY, JSON.stringify(this.ratings));
    } catch {
      /* storage unavailable: the votes last until the page closes */
    }
    for (const fn of this.listeners) fn(new Map([[this.me, this.ratings]]));
  }

  async people(): Promise<Record<string, Person>> {
    return { [this.me]: { name: 'Tú', color: '#54d1ff' } };
  }
}

// Minimal shapes of the platform capabilities this page uses.
interface DocSnap {
  id: string;
  exists: boolean;
  data(): Record<string, unknown> | undefined;
}
interface Db {
  doc(path: string): { set(data: Record<string, unknown>): Promise<void> };
  collection(path: string): { onSnapshot(next: (s: { docs: DocSnap[] }) => void, error?: (e: { code: string }) => void): () => void };
}
interface UserCap {
  id(): Promise<string | null>;
  profiles(ids: string[]): Promise<Record<string, { name: string; color: string }>>;
}
interface ClaudeRuntime {
  use(name: string): Promise<unknown>;
}

class SharedStore implements VoteStore {
  readonly shared = true;
  canWrite = true;
  private pending: Ratings | null = null;
  private writing = false;
  private listeners: ((people: Map<string, Ratings>) => void)[] = [];
  private latest = new Map<string, Ratings>();

  constructor(
    private readonly db: Db,
    private readonly user: UserCap,
    readonly me: string,
  ) {
    db.collection('votes').onSnapshot(
      (snap) => {
        const people = new Map<string, Ratings>();
        for (const d of snap.docs) if (d.exists) people.set(d.id, clean(d.data()?.ratings));
        this.latest = people;
        for (const fn of this.listeners) fn(people);
      },
      () => {
        /* terminal: keep the last delivered votes on screen */
      },
    );
  }

  onChange(fn: (people: Map<string, Ratings>) => void): void {
    this.listeners.push(fn);
    fn(this.latest);
  }

  save(ratings: Ratings): void {
    if (!this.canWrite) return;
    this.pending = clean(ratings);
    if (!this.writing) void this.flush();
  }

  private async flush(): Promise<void> {
    this.writing = true;
    while (this.pending) {
      const body = this.pending;
      this.pending = null;
      try {
        await this.db.doc(`votes/${this.me}`).set({ ratings: body });
      } catch (e) {
        if ((e as { code?: string }).code === 'invalid_argument') {
          // This viewer may not write (view-only access): show their votes as read-only.
          this.canWrite = false;
          this.pending = null;
          for (const fn of this.listeners) fn(this.latest);
        }
      }
    }
    this.writing = false;
  }

  async people(ids: string[]): Promise<Record<string, Person>> {
    const ps = await this.user.profiles(ids);
    const out: Record<string, Person> = {};
    for (const id of ids) out[id] = { name: ps[id]?.name || (id === this.me ? 'Tú' : 'Alguien'), color: ps[id]?.color || '#8aa1b0' };
    return out;
  }
}

/** Shared votes when the page runs on claude.ai with a signed-in viewer; local otherwise. */
export async function openVoteStore(): Promise<VoteStore> {
  const runtime = (window as unknown as { claude?: ClaudeRuntime }).claude;
  if (runtime?.use) {
    const [db, user] = (await Promise.all([runtime.use('db'), runtime.use('user')])) as [Db | null, UserCap | null];
    if (db && user) {
      const me = await user.id();
      if (me) return new SharedStore(db, user, me);
    }
  }
  return new LocalStore();
}
