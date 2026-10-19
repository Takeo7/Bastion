import { closeSync, constants, copyFileSync, existsSync, fsyncSync, mkdirSync, openSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { CAMPAIGN_VERSION, campaignOutdated, migrateCampaign, normalizeMission, ZERO_MODS, type CampaignState, type GameState, type MissionKind, type RoomPhase, type SkirmishMap, type Slot } from '@bastion/engine';

/** Bump when the saved state shape changes; older saves are migrated when possible. */
const SAVE_VERSION = 6;

/**
 * The release whose progression a campaign must be from (engine campaignOutdated, FRESH_CAMPAIGN_VERSION):
 * older ones are kept aside under its name, save.antes-<release>.json, and the messages say it. Change it
 * with FRESH_CAMPAIGN_VERSION; copies kept aside for an earlier release stay as they are.
 */
export const FRESH_RELEASE = '0.7';

export interface SavedSeat {
  slot: Slot;
  name: string;
  token: string;
}

export interface SaveFile {
  version: typeof SAVE_VERSION;
  seats: (SavedSeat | null)[];
  phase: RoomPhase;
  mode: 'skirmish' | 'campaign';
  state: GameState | null;
  campaign: CampaignState | null;
  resultApplied: boolean;
  rng: [number, number, number, number];
  missionKind: MissionKind;
  skirmishMap: SkirmishMap;
  skirmishVeterans: boolean;
  /** Skirmishes at night (docs/ALTURA-Y-NOCHE.md); missing in older saves: by day. */
  skirmishNight?: boolean;
}

/**
 * Older saves keep the seats and the campaign (upgraded to the new shape); a
 * tactical mission in progress can't be carried over, so the squad is back in
 * the base (or the lobby for a skirmish).
 */
function migrate(data: { version: number } & Record<string, unknown>): SaveFile | null {
  // From a newer Bastión: taken down to this shape it would lose what it has, so it isn't read (load keeps it aside).
  const campaignVersion = (data.campaign as { version?: unknown } | null | undefined)?.version;
  if (data.version > SAVE_VERSION || (typeof campaignVersion === 'number' && campaignVersion > CAMPAIGN_VERSION)) {
    throw new Error('es de una versión más nueva de Bastión');
  }
  if (!Array.isArray(data.seats)) throw new Error('no tiene puestos');
  if (data.version === SAVE_VERSION) {
    const save = data as unknown as SaveFile;
    if (save.campaign && save.campaign.version !== CAMPAIGN_VERSION) save.campaign = migrateCampaign(save.campaign);
    // A mission saved before a new kind of bonus existed: its units fight on with that bonus at zero.
    for (const u of save.state?.units ?? []) u.mods = { ...ZERO_MODS, ...u.mods };
    // One saved before buildings had storeys: every place in it is on the ground (docs/FASE6-PLAN.md).
    if (save.state) normalizeMission(save.state);
    return save;
  }
  if (data.version < 3) return null;
  const old = data as unknown as Omit<SaveFile, 'version' | 'skirmishMap' | 'skirmishVeterans'>;
  const campaign = old.campaign ? migrateCampaign(old.campaign) : null;
  console.warn(`[save] Partida de la versión ${data.version}: se conserva la campaña; una misión en curso se pierde.`);
  return {
    version: SAVE_VERSION,
    seats: old.seats,
    phase: campaign && old.phase !== 'lobby' ? 'base' : 'lobby',
    mode: campaign ? old.mode : 'skirmish',
    state: null,
    campaign,
    resultApplied: false,
    rng: old.rng,
    missionKind: old.missionKind,
    skirmishMap: 'city',
    skirmishVeterans: true,
  };
}

export class SaveStore {
  /** Where a campaign from before FRESH_RELEASE was put aside on loading, if one was (docs/FASE9.md §7, FASE10.md). */
  setAside: string | null = null;

  /** `log`: a line for the host's red.log (index.ts); the room writes there too. */
  constructor(
    private readonly path: string,
    readonly log: (line: string) => void = () => {},
  ) {}

  /** The save before the last one, <name>.anterior.json: save() keeps it on every write, load() reads it when the save can't be. */
  private get backup(): string {
    return this.beside('anterior');
  }

  load(): SaveFile | null {
    // No save but its backup: save() stopped between its two renames, and the backup is the last one.
    if (!existsSync(this.path)) return existsSync(this.backup) ? this.fromBackup('no estaba') : null;
    const read = this.read(this.path);
    if ('save' in read) return read.save;
    // Never written over: moved aside as it is, and the game goes on
    // from the backup if there is one. If it can't be moved, keepAside throws and the server stops rather than write over it.
    const aside = this.keepAside('ilegible', this.path, true);
    this.warn(`[save] No se pudo leer ${this.path} (${read.error}): queda en ${aside}.`);
    return existsSync(this.backup) ? this.fromBackup('no se podía leer') : null;
  }

  /** A copy of the save as it is now, before something replaces it (a new campaign); null without a save. */
  keepCopy(label: string): string | null {
    return existsSync(this.path) ? this.keepAside(label) : null;
  }

  private fromBackup(why: string): SaveFile | null {
    const read = this.read(this.backup);
    if ('save' in read) {
      this.warn(`[save] La partida ${why}: se sigue desde la copia anterior (${this.backup}).`);
      return read.save;
    }
    this.warn(`[save] Tampoco se pudo leer la copia anterior (${read.error}): se empieza en el vestíbulo.`);
    return null;
  }

  /** The save in `file`, brought to the current shape (null: nothing to carry on with), or why it can't be read. */
  private read(file: string): { save: SaveFile | null } | { error: string } {
    let data: { version: number } & Record<string, unknown>;
    try {
      data = JSON.parse(readFileSync(file, 'utf8')) as { version: number } & Record<string, unknown>;
    } catch (err) {
      return { error: (err as Error).message };
    }
    if (!data || typeof data !== 'object' || typeof data.version !== 'number') return { error: 'no es una partida de Bastión' };
    // A campaign from before FRESH_RELEASE can't take the new progression (docs/FASE9.md §7, FASE10.md): the file is kept aside
    // as it is and the room starts in the lobby. Checked before migrating, so every old shape of save goes
    // the same way. Outside the try: if it can't be kept aside the server stops, rather than write over it.
    const campaign = data.campaign as { version?: unknown } | null | undefined;
    if (campaign && typeof campaign.version === 'number' && campaignOutdated({ version: campaign.version })) {
      this.setAside = this.keepAside(`antes-${FRESH_RELEASE}`, file);
      this.warn(`[save] Partida de antes de la ${FRESH_RELEASE}: guardada aparte en ${this.setAside}; se empieza en el vestíbulo.`);
      return { save: null };
    }
    try {
      const save = migrate(data);
      if (!save) console.warn(`[save] Partida guardada de una versión anterior (${data.version}); se empieza de cero.`);
      return { save };
    } catch (err) {
      return { error: (err as Error).message };
    }
  }

  /**
   * Keeps `from`, as it is, next to the save as <name>.<label>.json (-2, -3… never over one already there);
   * `move` takes it away instead of copying it. Returns where; throws if it can't.
   */
  private keepAside(label: string, from = this.path, move = false): string {
    for (let n = 1; ; n++) {
      const target = this.beside(`${label}${n > 1 ? `-${n}` : ''}`);
      try {
        if (move) {
          if (existsSync(target)) continue;
          renameSync(from, target);
        } else copyFileSync(from, target, constants.COPYFILE_EXCL);
        return target;
      } catch (err) {
        if ((err as NodeJS.ErrnoException).code === 'EEXIST') continue;
        throw new Error(`No se pudo guardar aparte ${from} (${(err as Error).message}); no se toca.`);
      }
    }
  }

  private beside(label: string): string {
    return join(dirname(this.path), `${basename(this.path, '.json')}.${label}.json`);
  }

  private warn(line: string): void {
    console.warn(line);
    this.log(line);
  }

  /**
   * On the disk before it takes the save's name, so a crash or a power cut never leaves one cut short;
   * the one it replaces stays as the backup.
   */
  save(data: Omit<SaveFile, 'version'>): void {
    mkdirSync(dirname(this.path), { recursive: true });
    const tmp = `${this.path}.tmp`;
    const fd = openSync(tmp, 'w');
    try {
      writeFileSync(fd, JSON.stringify({ version: SAVE_VERSION, ...data }));
      fsyncSync(fd);
    } finally {
      closeSync(fd);
    }
    if (existsSync(this.path)) renameSync(this.path, this.backup);
    renameSync(tmp, this.path);
  }
}
