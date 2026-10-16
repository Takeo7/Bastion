import { z } from 'zod';
import { BUILDS, HEADS, NAME_MAX, NICKNAME_MAX } from './appearance';
import { FACILITIES, REGIONS } from './campaign';
import { ABILITIES, ITEMS, WEAPON_MODS } from './content';
import { CRAFTS } from './progression';
import { BOARD } from './board';
import type { CampaignState } from './campaign';
import type { GameEvent } from './events';
import type { AbilityId, GameState, Place, Slot } from './types';

const vec2 = z.object({ x: z.number().int(), y: z.number().int() });
/** A place (types.ts Pos): without `z`, on the ground (docs/FASE6-PLAN.md). */
const spot = z.object({ x: z.number().int(), y: z.number().int(), z: z.number().int().min(0).max(3).optional() });
const unitId = z.string().min(1).max(16);

export const abilityIdSchema = z.enum(Object.keys(ABILITIES) as [AbilityId, ...AbilityId[]]);

export const commandSchema = z.discriminatedUnion('type', [
  z.object({ type: z.literal('move'), unit: unitId, to: spot }),
  z.object({
    type: z.literal('ability'),
    unit: unitId,
    ability: abilityIdSchema,
    /** Unit target (attacks, support abilities). */
    target: unitId.optional(),
    /** Tile target (explosives). */
    tile: spot.optional(),
  }),
  z.object({ type: z.literal('endTurn'), ready: z.boolean() }),
  /** From a soldier in the bus, ask to evacuate; the partner answers (docs/PRUEBA-2026-10-14.md P1). */
  z.object({ type: z.literal('evac'), unit: unitId }),
  z.object({ type: z.literal('evacAnswer'), accept: z.boolean() }),
  /**
   * The backpack (docs/FASE8.md §C): drop a thing (`index`) or take one from the floor (`id`), free; give one
   * to a partner next to you (`to`), use a medicine on yourself or on them, or equip a found weapon, one action.
   */
  z.object({
    type: z.literal('item'),
    unit: unitId,
    action: z.enum(['drop', 'take', 'give', 'use', 'equip']),
    index: z.number().int().min(0).max(7).optional(),
    id: z.string().min(1).max(16).optional(),
    to: unitId.optional(),
  }),
  /** Hand the command (el mando) to the partner. */
  z.object({ type: z.literal('pass') }),
]);

export type Command = z.infer<typeof commandSchema>;

export const missionKindSchema = z.enum(['elimination', 'recovery', 'sabotage', 'hack', 'rescue', 'scavenge']);
/** Skirmish battlefield: a hand-made map (the district; «la torre», with storeys: docs/FASE6-PLAN.md) or a procedural map of a biome. */
/** 'roadside' and 'industrial': the scenarios (playtest 2026-10-14, #25), in the wilds and in a city. */
export const skirmishMapSchema = z.enum(['plaza', 'city', 'wilds', 'facility', 'torre', 'roadside', 'industrial']);
export type SkirmishMap = z.infer<typeof skirmishMapSchema>;
export const pingKindSchema = z.enum(['point', 'enemy']);
export type PingKind = z.infer<typeof pingKindSchema>;

export const clientMessageSchema = z.discriminatedUnion('t', [
  z.object({ t: z.literal('hello'), name: z.string().trim().min(1).max(20), token: z.string().max(64).optional() }),
  z.object({ t: z.literal('start'), mission: missionKindSchema.optional(), map: skirmishMapSchema.optional(), veterans: z.boolean().optional(), night: z.boolean().optional() }),
  z.object({ t: z.literal('newMission'), mission: missionKindSchema.optional(), map: skirmishMapSchema.optional(), night: z.boolean().optional() }),
  /** Ends the current mission for both players: asked of the partner first, if here (playtest 16/10). */
  z.object({ t: z.literal('abandon') }),
  z.object({ t: z.literal('abandonAnswer'), accept: z.boolean() }),
  /** Ask for a full snapshot (the client detected a gap in the event stream). */
  z.object({ t: z.literal('sync') }),
  z.object({ t: z.literal('cmd'), cmd: commandSchema }),
  z.object({ t: z.literal('presence'), unit: unitId.nullable(), tile: spot.nullable() }),
  /** Marks a tile for the partner ("go here", "this enemy"). */
  z.object({ t: z.literal('ping'), tile: spot, kind: pingKindSchema }),
  /** A move planned without the command, shown to the partner; `tile: null` withdraws it. */
  z.object({ t: z.literal('plan'), unit: unitId, tile: spot.nullable() }),
]);

export type ClientMessage = z.infer<typeof clientMessageSchema>;

const classId = z.enum(['assault', 'grenadier', 'sharpshooter', 'specialist']);
const keys = <K extends string>(record: Record<K, unknown>) => Object.keys(record) as [K, ...K[]];
const facilityId = z.enum(keys(FACILITIES));
const regionId = z.enum(keys(REGIONS));
const itemId = z.enum(keys(ITEMS));
/** Fase 9's talents (skills.ts SKILL_TALENTS). */
const cellId = z.enum(BOARD.map((c) => c.id) as [string, ...string[]]);
const id = z.string().min(1).max(16);
const color = z.string().regex(/^#[0-9a-f]{6}$/i);
// The pieces are optional (appearance.ts): older clients and saves send none.
const appearanceSchema = z.object({
  primary: color,
  secondary: color,
  visor: color,
  head: z.enum(keys(HEADS)),
  build: z.enum(keys(BUILDS)),
  sex: z.enum(['f', 'm']).optional(),
  helmet: color.optional(),
  torso: color.optional(),
  arms: color.optional(),
  legs: color.optional(),
  character: z.string().regex(/^[a-z][a-zA-Z0-9]{0,23}$/).optional(),
});

export const campaignCommandSchema = z.discriminatedUnion('type', [
  z.object({ type: z.literal('chooseMission'), id }),
  z.object({ type: z.literal('clear'), plot: id }),
  z.object({ type: z.literal('build'), plot: id, facility: facilityId }),
  z.object({ type: z.literal('upgrade'), plot: id }),
  z.object({ type: z.literal('contact'), region: regionId }),
  z.object({ type: z.literal('answerEvent'), option: z.union([z.literal(0), z.literal(1)]) }),
  z.object({ type: z.literal('investigate'), id }),
  z.object({ type: z.literal('cancelMission') }),
  z.object({ type: z.literal('squad'), soldier: id, on: z.boolean() }),
  z.object({ type: z.literal('recruit'), squad: z.boolean().optional() }),
  /** «Guardar» in the tree (docs/FASE9.md §2): the whole list of talents picked. */
  z.object({ type: z.literal('unlock'), soldier: id, cell: cellId }),
  /** The Armería (docs/FASE9.md §5): mend a weapon; mount or take off an improvement. */
  z.object({ type: z.literal('repair'), weapon: id }),
  z.object({ type: z.literal('mod'), weapon: id, mod: z.enum(keys(WEAPON_MODS)), on: z.boolean() }),
  z.object({ type: z.literal('buy'), item: itemId }),
  z.object({ type: z.literal('equip'), soldier: id, slot: z.enum(['utility', 'ammo']), item: itemId.nullable() }),
  z.object({ type: z.literal('customize'), soldier: id, name: z.string().max(NAME_MAX * 2), nickname: z.string().max(NICKNAME_MAX * 2), appearance: appearanceSchema }),
  z.object({ type: z.literal('advance'), ready: z.boolean() }),
  z.object({ type: z.literal('launch'), ready: z.boolean() }),
  z.object({ type: z.literal('treat'), soldier: id, ready: z.boolean() }),
  z.object({ type: z.literal('medicate'), soldier: id }),
  z.object({ type: z.literal('craft'), project: z.enum(keys(CRAFTS)) }),
  z.object({ type: z.literal('assignWeapon'), weapon: id, soldier: id.nullable() }),
  z.object({ type: z.literal('vote'), accept: z.boolean() }),
  z.object({ type: z.literal('withdraw') }),
]);

/** Screens of the strategy layer, XCOM 2 style (shown to the partner). */
export const baseViewSchema = z.enum(['hub', 'geoscape', 'research', 'engineering', 'barracks', 'facilities', 'squad', 'debrief']);
export type BaseView = z.infer<typeof baseViewSchema>;

/** Messages used while in the campaign base (validated separately from the tactical ones). */
export const campaignMessageSchema = z.discriminatedUnion('t', [
  z.object({ t: z.literal('newCampaign') }),
  z.object({ t: z.literal('continueCampaign') }),
  z.object({ t: z.literal('ccmd'), cmd: campaignCommandSchema }),
  z.object({ t: z.literal('returnToBase') }),
  z.object({ t: z.literal('leaveCampaign') }),
  /** Back to the main menu, for both players: from the base, or after a finished skirmish. */
  z.object({ t: z.literal('backToMenu') }),
  /** Which base screen the player is looking at (shown to the partner). */
  z.object({ t: z.literal('baseView'), view: baseViewSchema }),
]);

export type CampaignMessage = z.infer<typeof campaignMessageSchema>;

export interface PlayerInfo {
  slot: Slot;
  name: string;
  color: string;
  connected: boolean;
}

export type RoomPhase = 'lobby' | 'base' | 'mission';

/** A network address of the host: same local network, or Tailscale (100.64.0.0/10). */
export interface HostAddress {
  address: string;
  kind: 'lan' | 'tailscale';
}

export type ServerMessage =
  /** `hosts`: this machine's network addresses, so the menu can tell the partner where to connect; `httpsPort`: where the game is also served over https. */
  | { t: 'welcome'; token: string; slot: Slot; hosts?: HostAddress[]; httpsPort?: number }
  | {
      t: 'room';
      players: PlayerInfo[];
      phase: RoomPhase;
      savedCampaign: { day: number; doom: number; level: number } | null;
      /** A campaign from before Fase 9 the host put aside on starting (docs/FASE9.md §7): the file's name. */
      setAside?: string;
    }
  | { t: 'campaign'; state: CampaignState }
  | { t: 'baseView'; slot: Slot; view: BaseView }
  | { t: 'snapshot'; state: GameState }
  | { t: 'events'; events: GameEvent[]; seq: number; by: Slot | null }
  | { t: 'presence'; slot: Slot; unit: string | null; tile: Place | null }
  | { t: 'ping'; slot: Slot; tile: Place; kind: PingKind }
  | { t: 'plan'; slot: Slot; unit: string; tile: Place | null }
  | { t: 'rejected'; message: string }
  /**
   * A joint decision waits for the partner (playtest 2026-10-14, #28): a base
   * proposal or a call to evacuate goes ahead on its own after `ms` more
   * milliseconds, counted on the client's own clock; `ms: null`, it was answered.
   */
  | { t: 'deadline'; kind: DeadlineKind; ms: number | null }
  | { t: 'error'; message: string };

export type DeadlineKind = 'proposal' | 'evac' | 'abandon';
