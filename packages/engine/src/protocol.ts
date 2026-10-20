import { z } from 'zod';
import { BUILDS, HEADS, NAME_MAX, NICKNAME_MAX } from './appearance';
import { PIECES, type PieceId } from './base';
import { REGIONS } from './campaign';
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
  /** Real time (docs/TIEMPO-REAL.md): a soldier's standing order: go to `to`, attack `target` until down, or (neither) stand. */
  z.object({ type: z.literal('rtOrder'), unit: unitId, to: spot.optional(), target: unitId.optional(), ability: abilityIdSchema.optional(), tile: spot.optional() }),
  /** Real time: stop the clock or let it go on, for everyone. */
  z.object({ type: z.literal('rtPause'), paused: z.boolean() }),
  /** Real time: the clock very slow while an ability is being aimed (Arturo, 20/10), or back to its pace. */
  z.object({ type: z.literal('rtSlow'), slow: z.boolean() }),
]);

export type Command = z.infer<typeof commandSchema>;

export const missionKindSchema = z.enum(['elimination', 'recovery', 'sabotage', 'hack', 'rescue', 'scavenge']);
/** Skirmish battlefield: a hand-made map (the district; «la torre», with storeys: docs/FASE6-PLAN.md) or a procedural map of a biome. */
/** 'roadside' and 'industrial': the scenarios (playtest 2026-10-14, #25), in the wilds and in a city. 'bastion': the base under siege (docs/FASE12.md). */
export const skirmishMapSchema = z.enum(['plaza', 'city', 'wilds', 'facility', 'torre', 'roadside', 'industrial', 'bastion']);
export type SkirmishMap = z.infer<typeof skirmishMapSchema>;
export const pingKindSchema = z.enum(['point', 'enemy']);
export type PingKind = z.infer<typeof pingKindSchema>;

export const clientMessageSchema = z.discriminatedUnion('t', [
  z.object({ t: z.literal('hello'), name: z.string().trim().min(1).max(20), token: z.string().max(64).optional() }),
  /** `realtime`: played in real time instead of by turns (docs/TIEMPO-REAL.md: a prototype, skirmish only). */
  z.object({ t: z.literal('start'), mission: missionKindSchema.optional(), map: skirmishMapSchema.optional(), veterans: z.boolean().optional(), night: z.boolean().optional(), realtime: z.boolean().optional() }),
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
  /**
   * The game went out of sight (its window minimized or behind another, the laptop asleep) or came back: while away, a
   * dropped socket keeps its turn and the command longer. Always answered with `room`, which the client
   * takes as proof the socket survived the sleep.
   */
  z.object({ t: z.literal('away'), away: z.boolean() }),
]);

export type ClientMessage = z.infer<typeof clientMessageSchema>;

const keys = <K extends string>(record: Record<K, unknown>) => Object.keys(record) as [K, ...K[]];
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

/** A tile of the base's map (docs/FASE12.md §11). */
const tileCoord = z.number().int().min(0).max(255);

export const campaignCommandSchema = z.discriminatedUnion('type', [
  /** The base (docs/FASE12.md §11): put a piece somewhere, turned; fell trees; mend, raise a level, set again or take down a piece. */
  z.object({ type: z.literal('place'), piece: z.enum(keys(PIECES)), x: tileCoord, y: tileCoord, rot: z.number().int().min(0).max(3) }),
  z.object({ type: z.literal('fell'), tiles: z.array(z.object({ x: tileCoord, y: tileCoord })).min(1).max(400) }),
  z.object({ type: z.literal('mend'), piece: id }),
  z.object({ type: z.literal('raise'), piece: id }),
  z.object({ type: z.literal('placeLine'), piece: z.enum(keys(PIECES)), cells: z.array(z.object({ x: tileCoord, y: tileCoord })).min(1).max(60), rot: z.number().int().min(0).max(3) }),
  z.object({ type: z.literal('levelUp') }),
  z.object({ type: z.literal('post'), soldier: id, at: z.object({ x: tileCoord, y: tileCoord }).nullable() }),
  z.object({ type: z.literal('rearm'), piece: id }),
  z.object({ type: z.literal('dismantle'), piece: id }),
  z.object({ type: z.literal('chooseMission'), id }),
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
  /**
   * Building on the base (docs/FASE12.md §11.7): the piece in hand and where it points, for the partner to see;
   * null once it is put down or the build mode closed. Passed on as it comes, never kept.
   */
  z.object({ t: z.literal('baseCursor'), piece: z.enum(keys(PIECES)).nullable(), x: tileCoord, y: tileCoord, rot: z.number().int().min(0).max(3) }),
]);

export type CampaignMessage = z.infer<typeof campaignMessageSchema>;

export interface PlayerInfo {
  slot: Slot;
  name: string;
  color: string;
  connected: boolean;
  /** Their game out of sight (the `away` message): back soon, their turn waits. */
  away?: boolean;
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
      /**
       * `missions`: missions flown (time goes by them, docs/FASE12.md §11.6); `day`, the clock inside; `inMission`, the
       * name of a mission left for the menu half played, which «Continuar campaña» goes back to (playtest 19/10, #7).
       */
      savedCampaign: { day: number; missions: number; doom: number; level: number; inMission?: string } | null;
      /** A campaign from before Fase 9 the host put aside on starting (docs/FASE9.md §7): the file's name. */
      setAside?: string;
    }
  | { t: 'campaign'; state: CampaignState }
  | { t: 'baseView'; slot: Slot; view: BaseView }
  /** The partner's piece in hand on the base, and where (null: nothing in hand). */
  | { t: 'baseCursor'; slot: Slot; piece: PieceId | null; x: number; y: number; rot: number }
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
  /** The server is shutting down (the host quit the app, or stopped it): the game is saved; don't wait for it to come back. */
  | { t: 'closing' }
  | { t: 'error'; message: string };

export type DeadlineKind = 'proposal' | 'evac' | 'abandon';
