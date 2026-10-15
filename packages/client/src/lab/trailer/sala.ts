import * as THREE from 'three';
import { executeCommand, PLAYER_COLORS, Rng, type Command, type GameState, type PlayerInfo, type Slot } from '@bastion/engine';
import { tileToWorld } from '../../game/coords';
import { Game } from '../../game/game';
import { setCombatRate } from '../../game/tween';
import type { Renderer } from '../../game/renderer';
import type { Net } from '../../net';
import { cameraPath, type Key, type V3 } from './cine';
import type { Shot } from './types';

// The trailer's combat (docs/TRAILER.md, shots 5 and 7): a battle of the real
// game, played in the page. Gameplay sets each one up (tools/trailer/tomas.ts:
// .dev-snapshots/trailer/partidas/tomas/<id>.json, a mission's state and the
// orders that make the action); here a room of its own does what the
// server's does: each order through the engine (executeCommand, all with the
// one Rng of the plan's seed, in order), its events to the Game, which plays
// them as in a battle. The camera is a cinema one (cine.ts), not the game's;
// its HUD, the selection and the move overlays are out of the picture.

/** One of Gameplay's plans. */
export interface Plan {
  id: string;
  what: string;
  state: GameState;
  orders: { at: number; slot: Slot; cmd: Command }[];
  connected: Slot[];
  focus: { x: number; y: number; z?: number; level?: number };
  seed: number;
}

const PLANS = import.meta.glob<{ default: Plan }>('../../../../../.dev-snapshots/trailer/partidas/tomas/*.json');

export async function loadPlan(id: string): Promise<Plan> {
  const module = PLANS[`../../../../../.dev-snapshots/trailer/partidas/tomas/${id}.json`];
  if (!module) throw new Error(`Falta el plano «${id}» de Gameplay (.dev-snapshots/trailer/partidas/tomas/${id}.json: npx tsx packages/client/tools/trailer/tomas.ts).`);
  return (await module()).default;
}

const PLAYERS: PlayerInfo[] = [
  { slot: 0, name: 'Ana', color: PLAYER_COLORS[0], connected: true },
  { slot: 1, name: 'Arturo', color: PLAYER_COLORS[1], connected: true },
];

/** What of the Game this needs (private there: the trailer reaches past the language's walls, not the page's). */
interface GameInside {
  renderer: Renderer;
  rig: { update(dt: number): void };
  units: {
    setSelected(id: string | null): void;
    forceVisible(id: string): void;
    raiseWeapon(id: string, target: THREE.Vector3): Promise<void>;
    face(id: string, toward: THREE.Vector3): void;
    views: Map<string, { ring: THREE.Object3D }>;
    flashScale: number;
  };
  view: GameState;
  overlays?: { group: THREE.Object3D };
  veiled: Promise<void> | null;
  fx: { impactGlow: number };
  root: HTMLElement;
}

/** A battle of the real game in `stage`, the plan's orders played at their times. */
export class LocalBattle {
  readonly game: Game;
  private readonly inside: GameInside;
  private readonly state: GameState;
  private readonly rng: Rng;
  private next = 0;
  /** Every one of the dead in sight, whatever the squad sees (the one left behind at the evacuation sees for nobody). */
  reveal = false;

  constructor(stage: HTMLElement, readonly plan: Plan) {
    this.state = structuredClone(plan.state);
    this.rng = new Rng(plan.seed);
    // The page has none of the game's stylesheets: the battle fills the stage (before it is built, so its renderer starts at that size),
    // and its HUD is out of the way (it would not be captured anyway: HTML).
    if (!document.getElementById('cine-style')) {
      const css = '.game.cine, #escena > .game { position: absolute; inset: 0; } .game.cine > :not(canvas) { visibility: hidden !important; }';
      document.head.append(Object.assign(document.createElement('style'), { id: 'cine-style', textContent: css }));
    }
    // The game's own orders (none: nobody clicks) would go the same way as the plan's.
    const net = { send: (m: { t: string; cmd?: Command }) => m.t === 'cmd' && m.cmd && this.run(0, m.cmd) } as unknown as Net;
    this.game = new Game(stage, net, 0, PLAYERS, structuredClone(plan.state));
    this.inside = this.game as unknown as GameInside;
    // The cinema camera only: the game's rig keeps its aims to itself.
    this.inside.rig.update = () => {};
    this.inside.renderer.closeUp = true;
    this.inside.root.classList.add('cine');
    // A hit's flash and its impact's glow, close up and lit by the bloom, turned the whole body flat pink: the blood and
    // sparks stay, the glow is small and the body keeps its colours.
    this.inside.units.flashScale = 0;
    this.inside.fx.impactGlow = 0.3;
    // No selection, no owners' rings, no move overlays: the battle as a film shows it.
    this.inside.renderer.onFrame(() => {
      this.inside.units.setSelected(null);
      for (const v of this.inside.units.views.values()) v.ring.visible = false;
      if (this.reveal) for (const u of this.inside.view.units) if (u.team !== 'xcom' && u.alive) this.inside.units.forceVisible(u.id);
      if (this.inside.overlays) this.inside.overlays.group.visible = false;
    });
  }

  get renderer(): Renderer {
    return this.inside.renderer;
  }

  /** Unit `id` turned towards `target`. */
  face(id: string, target: THREE.Vector3): void {
    this.inside.units.face(id, target);
  }

  /** The soldier `id` up and aiming at `target` (a still: the cover). */
  aim(id: string, target: THREE.Vector3): Promise<void> {
    return this.inside.units.raiseWeapon(id, target);
  }

  /** Waits for the battle to be ready to play (the game's loading veil: models in, shaders compiled). */
  async ready(): Promise<void> {
    await this.inside.veiled;
    await new Promise((r) => setTimeout(r, 300));
  }

  /** Plays every order due by `t` (seconds into the shot). */
  tick(t: number): void {
    const orders = this.plan.orders;
    while (this.next < orders.length && orders[this.next]!.at <= t) {
      const o = orders[this.next++]!;
      this.run(o.slot, o.cmd);
    }
  }

  private run(slot: Slot, cmd: Command): void {
    const result = executeCommand(this.state, cmd, { slot, connected: this.plan.connected, rng: this.rng });
    if (!result.ok) {
      console.warn(`[sala] ${this.plan.id}: orden rechazada (${result.error})`, cmd);
      return;
    }
    // The end of the mission is not played: the game would put its outcome over the picture (Gameplay: «no pintes la pantalla final»).
    const events = result.events.filter((e) => e.t !== 'missionEnded');
    this.game.onEvents(events, this.state.seq - (result.events.length - events.length), slot);
  }

  /** Where the plan's action is, in the world, `height` above its floor. */
  focus(height = 0): THREE.Vector3 {
    const f = this.plan.focus;
    return tileToWorld({ x: f.x, y: f.y, z: f.z ?? f.level ?? 0 }, height);
  }

  dispose(): void {
    this.game.destroy();
  }
}

/** A point `distance` from `centre` on the ground, at `yaw` degrees (0 to the south, +z; 90 to the east, +x), `height` up. */
export function around(centre: THREE.Vector3, yaw: number, distance: number, height: number): V3 {
  const a = THREE.MathUtils.degToRad(yaw);
  return [centre.x + Math.sin(a) * distance, centre.y + height, centre.z + Math.cos(a) * distance];
}

/** A tile's middle in the world (on its floor), as cine.ts's keys take it. */
export function spot(x: number, y: number, height = 0): THREE.Vector3 {
  return tileToWorld({ x, y }, height);
}

/** From `from` towards `to`, seen from `back` behind `from` and `side` to its right, `height` up: over a shoulder. */
export function behind(from: THREE.Vector3, to: THREE.Vector3, back: number, side: number, height: number): V3 {
  const dir = to.clone().sub(from).setY(0).normalize();
  const right = new THREE.Vector3(-dir.z, 0, dir.x);
  return from.clone().addScaledVector(dir, -back).addScaledVector(right, side).setY(from.y + height).toArray() as unknown as V3;
}

/** A point as cine.ts's keys take it, `height` above it. */
export const v3 = (v: THREE.Vector3, height = 0): V3 => [v.x, v.y + height, v.z];

/** Where a unit of the plan stands as it begins (in the world, on its floor). */
export function unitSpot(battle: LocalBattle, id: string): THREE.Vector3 {
  const u = battle.plan.state.units.find((x) => x.id === id);
  if (!u) throw new Error(`No hay unidad «${id}» en el plano ${battle.plan.id}.`);
  return tileToWorld(u.pos);
}

export interface CombatOptions {
  /**
   * Seconds of the plan played before the shot begins (during its setup, in
   * real time; the take starts with the action under way): the plan's times
   * are its own, the shot's `t` is `lead` later in it.
   */
  lead?: number;
  /** The plan's orders at other times (by their index), so that more of it fits a short shot: the same orders, in the same order. */
  times?: number[];
  /** The dead always in sight (LocalBattle.reveal). */
  reveal?: boolean;
  /** The battle's speed by the plan's time (the game's «Velocidad del combate»: 1 normal, 1 / 0.6 fast), to fit what matters at its own pace. */
  rate?: (planTime: number) => number;
}

/**
 * A shot of the combat: Gameplay's plan `id` played in the real game, the
 * camera through the keys `camera` gives (the plan's focus and the battle to
 * place them by).
 */
export function combatShot(id: string, duration: number, camera: (focus: THREE.Vector3, battle: LocalBattle) => Key[], options: CombatOptions = {}): Shot {
  let battle: LocalBattle | null = null;
  let move: ((t: number, c: THREE.PerspectiveCamera) => void) | null = null;
  const lead = options.lead ?? 0;
  return {
    id,
    duration,
    async setup(stage) {
      const plan = await loadPlan(id);
      if (options.times) plan.orders = plan.orders.map((o, i) => ({ ...o, at: options.times![i] ?? o.at }));
      battle = new LocalBattle(stage, plan);
      battle.reveal = !!options.reveal;
      (window as unknown as { __sala: LocalBattle }).__sala = battle;
      await battle.ready();
      move = cameraPath(camera(battle.focus(), battle));
      move(0, battle.renderer.camera);
      // The lead, played as it comes (the clock runs free during setup).
      const start = performance.now();
      while ((performance.now() - start) / 1000 < lead) {
        battle.tick((performance.now() - start) / 1000);
        await new Promise((r) => requestAnimationFrame(r));
      }
      battle.tick(lead);
    },
    frame(t) {
      if (options.rate) setCombatRate(options.rate(lead + t));
      battle?.tick(lead + t);
      if (battle && move) move(t, battle.renderer.camera);
    },
    teardown() {
      setCombatRate(1);
      battle?.dispose();
    },
  };
}
