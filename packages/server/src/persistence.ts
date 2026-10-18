import { constants, copyFileSync, existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
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

  /** `log`: a line for the host's red.log (index.ts). */
  constructor(
    private readonly path: string,
    private readonly log: (line: string) => void = () => {},
  ) {}

  load(): SaveFile | null {
    if (!existsSync(this.path)) return null;
    let data: { version: number } & Record<string, unknown>;
    try {
      data = JSON.parse(readFileSync(this.path, 'utf8')) as { version: number } & Record<string, unknown>;
    } catch (err) {
      console.warn(`[save] No se pudo leer ${this.path}:`, err);
      return null;
    }
    // A campaign from before FRESH_RELEASE can't take the new progression (docs/FASE9.md §7, FASE10.md): the file is kept aside
    // as it is and the room starts in the lobby. Checked before migrating, so every old shape of save goes
    // the same way. Outside the try: if it can't be kept aside the server stops, rather than write over it.
    const campaign = data.campaign as { version?: unknown } | null | undefined;
    if (campaign && typeof campaign.version === 'number' && campaignOutdated({ version: campaign.version })) {
      this.setAside = this.keepAside();
      const line = `[save] Partida de antes de la ${FRESH_RELEASE}: guardada aparte en ${this.setAside}; se empieza en el vestíbulo.`;
      console.warn(line);
      this.log(line);
      return null;
    }
    try {
      const save = migrate(data);
      if (!save) console.warn(`[save] Partida guardada de una versión anterior (${data.version}); se empieza de cero.`);
      return save;
    } catch (err) {
      console.warn(`[save] No se pudo leer ${this.path}:`, err);
      return null;
    }
  }

  /** Copies the save, as it is, next to it as <name>.antes-<FRESH_RELEASE>.json (-2, -3… never over one already there); returns where. */
  private keepAside(): string {
    const stem = join(dirname(this.path), basename(this.path, '.json'));
    for (let n = 1; ; n++) {
      const target = `${stem}.antes-${FRESH_RELEASE}${n > 1 ? `-${n}` : ''}.json`;
      try {
        copyFileSync(this.path, target, constants.COPYFILE_EXCL);
        return target;
      } catch (err) {
        if ((err as NodeJS.ErrnoException).code === 'EEXIST') continue;
        throw new Error(`No se pudo guardar aparte la partida de antes de la ${FRESH_RELEASE} (${(err as Error).message}); no se toca ${this.path}.`);
      }
    }
  }

  /** Atomic write: a crash mid-write never leaves a corrupt save. */
  save(data: Omit<SaveFile, 'version'>): void {
    mkdirSync(dirname(this.path), { recursive: true });
    const tmp = `${this.path}.tmp`;
    writeFileSync(tmp, JSON.stringify({ version: SAVE_VERSION, ...data }));
    renameSync(tmp, this.path);
  }
}
