import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { CAMPAIGN_VERSION, migrateCampaign, type CampaignState, type GameState, type MissionKind, type RoomPhase, type SkirmishMap, type Slot } from '@bastion/engine';

/** Bump when the saved state shape changes; older saves are migrated when possible. */
const SAVE_VERSION = 6;

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
  constructor(private readonly path: string) {}

  load(): SaveFile | null {
    if (!existsSync(this.path)) return null;
    try {
      const data = JSON.parse(readFileSync(this.path, 'utf8')) as { version: number } & Record<string, unknown>;
      const save = migrate(data);
      if (!save) console.warn(`[save] Partida guardada de una versión anterior (${data.version}); se empieza de cero.`);
      return save;
    } catch (err) {
      console.warn(`[save] No se pudo leer ${this.path}:`, err);
      return null;
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
