import { z } from 'zod';
import { BUILDS, HEADS, NAME_MAX, NICKNAME_MAX } from './appearance';
import { FACILITIES, REGIONS } from './campaign';
import { ABILITIES, ITEMS, PERKS } from './content';
import type { CampaignState } from './campaign';
import type { GameEvent } from './events';
import type { AbilityId, GameState, Slot, Vec2 } from './types';

const vec2 = z.object({ x: z.number().int(), y: z.number().int() });
const unitId = z.string().min(1).max(16);

export const abilityIdSchema = z.enum(Object.keys(ABILITIES) as [AbilityId, ...AbilityId[]]);

export const commandSchema = z.discriminatedUnion('type', [
  z.object({ type: z.literal('move'), unit: unitId, to: vec2 }),
  z.object({
    type: z.literal('ability'),
    unit: unitId,
    ability: abilityIdSchema,
    /** Unit target (attacks, support abilities). */
    target: unitId.optional(),
    /** Tile target (explosives). */
    tile: vec2.optional(),
  }),
  z.object({ type: z.literal('endTurn'), ready: z.boolean() }),
  /** Hand the command (el mando) to the partner. */
  z.object({ type: z.literal('pass') }),
]);

export type Command = z.infer<typeof commandSchema>;

export const missionKindSchema = z.enum(['elimination', 'recovery', 'sabotage', 'hack', 'rescue']);
/** Skirmish battlefield: the hand-made district or a procedural map of a biome. */
export const skirmishMapSchema = z.enum(['plaza', 'city', 'wilds', 'facility']);
export type SkirmishMap = z.infer<typeof skirmishMapSchema>;
export const pingKindSchema = z.enum(['point', 'enemy']);
export type PingKind = z.infer<typeof pingKindSchema>;

export const clientMessageSchema = z.discriminatedUnion('t', [
  z.object({ t: z.literal('hello'), name: z.string().trim().min(1).max(20), token: z.string().max(64).optional() }),
  z.object({ t: z.literal('start'), mission: missionKindSchema.optional(), map: skirmishMapSchema.optional(), veterans: z.boolean().optional() }),
  z.object({ t: z.literal('newMission'), mission: missionKindSchema.optional(), map: skirmishMapSchema.optional() }),
  /** Ends the current mission for both players. */
  z.object({ t: z.literal('abandon') }),
  /** Ask for a full snapshot (the client detected a gap in the event stream). */
  z.object({ t: z.literal('sync') }),
  z.object({ t: z.literal('cmd'), cmd: commandSchema }),
  z.object({ t: z.literal('presence'), unit: unitId.nullable(), tile: vec2.nullable() }),
  /** Marks a tile for the partner ("go here", "this enemy"). */
  z.object({ t: z.literal('ping'), tile: vec2, kind: pingKindSchema }),
]);

export type ClientMessage = z.infer<typeof clientMessageSchema>;

const classId = z.enum(['assault', 'grenadier', 'sharpshooter', 'specialist']);
const keys = <K extends string>(record: Record<K, unknown>) => Object.keys(record) as [K, ...K[]];
const facilityId = z.enum(keys(FACILITIES));
const regionId = z.enum(keys(REGIONS));
const itemId = z.enum(keys(ITEMS));
const perkId = z.enum(keys(PERKS));
const id = z.string().min(1).max(16);
const color = z.string().regex(/^#[0-9a-f]{6}$/i);
const appearanceSchema = z.object({ primary: color, secondary: color, visor: color, head: z.enum(keys(HEADS)), build: z.enum(keys(BUILDS)) });

export const campaignCommandSchema = z.discriminatedUnion('type', [
  z.object({ type: z.literal('chooseMission'), id }),
  z.object({ type: z.literal('excavate'), slot: id }),
  z.object({ type: z.literal('build'), slot: id, facility: facilityId }),
  z.object({ type: z.literal('contact'), region: regionId }),
  z.object({ type: z.literal('answerEvent'), option: z.union([z.literal(0), z.literal(1)]) }),
  z.object({ type: z.literal('investigate'), id }),
  z.object({ type: z.literal('cancelMission') }),
  z.object({ type: z.literal('squad'), soldier: id, on: z.boolean() }),
  z.object({ type: z.literal('recruit'), cls: classId }),
  z.object({ type: z.literal('promote'), soldier: id, perk: perkId }),
  z.object({ type: z.literal('buy'), item: itemId }),
  z.object({ type: z.literal('equip'), soldier: id, slot: z.enum(['utility', 'ammo']), item: itemId.nullable() }),
  z.object({ type: z.literal('customize'), soldier: id, name: z.string().max(NAME_MAX * 2), nickname: z.string().max(NICKNAME_MAX * 2), appearance: appearanceSchema }),
  z.object({ type: z.literal('advance'), ready: z.boolean() }),
  z.object({ type: z.literal('launch'), ready: z.boolean() }),
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
  /** `hosts`: this machine's network addresses, so the menu can tell the partner where to connect. */
  | { t: 'welcome'; token: string; slot: Slot; hosts?: HostAddress[] }
  | { t: 'room'; players: PlayerInfo[]; phase: RoomPhase; savedCampaign: { day: number; doom: number } | null }
  | { t: 'campaign'; state: CampaignState }
  | { t: 'baseView'; slot: Slot; view: BaseView }
  | { t: 'snapshot'; state: GameState }
  | { t: 'events'; events: GameEvent[]; seq: number; by: Slot | null }
  | { t: 'presence'; slot: Slot; unit: string | null; tile: Vec2 | null }
  | { t: 'ping'; slot: Slot; tile: Vec2; kind: PingKind }
  | { t: 'rejected'; message: string }
  | { t: 'error'; message: string };
