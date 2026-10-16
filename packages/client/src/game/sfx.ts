import * as THREE from 'three';
import { TEMPLATES, type Alarm, type Biome, type GameState, type Unit, type WeaponDef } from '@bastion/engine';
import { ambience } from '../audio/ambience';
import { loopBuffer, sound } from '../audio/bank';
import { SOUNDS } from '../audio/sounds';
import { audio, type Loop } from '../audio/engine';
import { music, type Piece } from '../audio/music';
import { tileToWorld } from './coords';

/** The sabotage's alarm, as the engine says which it is (s.mission.alarm, chosen by the campaign) and Arte's model shows. */
const SIRENS: Record<Alarm, string> = { police: 'siren.police', ambulance: 'siren.ambulance', factory: 'siren.alarm' };
/** Without that (a skirmish, an older save): by the map, a police car in town, an ambulance out of it, a factory's motor siren. */
const SIRENS_BY_MAP: Record<Biome, string> = { city: 'siren.police', wilds: 'siren.ambulance', facility: 'siren.alarm' };
/** Seconds a siren takes to fall silent: a motor runs down (and its light with it); the electric ones cut out. */
const SIREN_STOP: Record<string, number> = { 'siren.alarm': 2.5 };
/** The siren's level right beside it: steady under the fight, not over it. */
const SIREN_LEVEL = 0.45;

/** The siren sounding now: which, where, its loop once sound can play, and the timer that places it. */
interface Wail {
  name: string;
  at: THREE.Vector3;
  loop: Loop | null;
  timer: number;
}

/**
 * Combat sound: what game.ts calls when it animates an event. Each sound is
 * placed where it happens (stereo by its place on screen, quieter far from
 * where the camera looks), guns sound like their class, tier and side, and
 * the music and ambience follow the mission (biome, concealment, contact,
 * the enemy turn). The sounds themselves are made in audio/sounds.ts.
 */
export class Sfx {
  private camera: THREE.Camera | null = null;
  /** Out of town the ground is earth, not asphalt or concrete (where a stone lands, stoneLand). */
  private softGround = false;
  private focus: () => THREE.Vector3 = () => new THREE.Vector3();
  private readonly ndc = new THREE.Vector3();

  /** The camera that hears, and where it looks (for distance). */
  attach(camera: THREE.Camera, focus: () => THREE.Vector3): void {
    this.camera = camera;
    this.focus = focus;
  }

  get muted(): boolean {
    return audio.muted;
  }

  toggleMute(): boolean {
    return audio.toggleMute();
  }

  /** Stereo position and level of a sound at `at`: panned by screen position, quieter with distance and off screen. */
  private place(at?: THREE.Vector3, gain = 1): { pan: number; gain: number } {
    if (!at || !this.camera) return { pan: 0, gain };
    this.ndc.copy(at).project(this.camera);
    const pan = THREE.MathUtils.clamp(this.ndc.x * 0.7, -0.75, 0.75);
    const dist = at.distanceTo(this.focus());
    let g = THREE.MathUtils.clamp(1.15 - dist / 30, 0.35, 1);
    if (Math.abs(this.ndc.x) > 1 || Math.abs(this.ndc.y) > 1 || this.ndc.z > 1) g *= 0.7;
    return { pan, gain: g * gain };
  }

  private at(name: string, at?: THREE.Vector3, gain = 1, extra: { delay?: number; spread?: number; rate?: number } = {}): void {
    sound(name, { ...this.place(at, gain), ...extra });
  }

  /** The first of these sounds that exists (newer recipes fall back to what is there). */
  private pick(...names: string[]): string {
    return names.find((n) => n in SOUNDS) ?? names[names.length - 1]!;
  }

  // ------------------------------------------------------------ weapons

  /** One round (bursts call this per round): the gun's own voice. */
  shot(u: Unit, weapon: WeaponDef, at?: THREE.Vector3): void {
    this.at(gunSound(u, weapon), at, 1, { spread: 0.05 });
  }

  /** Where a round lands: flesh, armour, a machine, or a miss (whizzing past, chipping cover). */
  impact(kind: 'flesh' | 'armor' | 'robot' | 'miss', at?: THREE.Vector3, crit = false): void {
    this.at(`impact.${kind}`, at);
    if (kind === 'miss' && Math.random() < 0.6) this.at('impact.cover', at, 0.8, { delay: 0.05 });
    if (crit) this.at('impact.crit', at);
  }

  /** The kind of flesh a target is, for its impacts. */
  body(u: Unit): 'flesh' | 'robot' {
    return TEMPLATES[u.template].robotic ? 'robot' : 'flesh';
  }

  melee(kind: 'swing' | 'sword' | 'stun' | 'claw', at?: THREE.Vector3): void {
    this.at(`melee.${kind}`, at);
  }

  // ------------------------------------------------------------ explosives

  throw(at?: THREE.Vector3): void {
    this.at('grenade.throw', at);
  }

  /** A stone (docs/FASE10.md §4) leaving the hand: a short swish. game.ts calls it as units.tossStone resolves. */
  stoneThrow(at?: THREE.Vector3): void {
    this.at('stone.throw', at, 1, { spread: 0.08 });
  }

  /**
   * Where the stone comes down, as fx.stone resolves: a dry knock with its bounces on hard ground, a thud
   * on earth (out of town), or a blunt blow on whoever it `struck`. It is what draws the dead: it carries.
   */
  stoneLand(at?: THREE.Vector3, struck = false): void {
    this.at(struck ? 'stone.hit' : this.softGround ? 'stone.thud' : 'stone.land', at);
  }

  launch(at?: THREE.Vector3): void {
    this.at('launch', at);
  }

  land(at?: THREE.Vector3): void {
    this.at('grenade.land', at);
  }

  missile(at?: THREE.Vector3): void {
    this.at('missile', at);
  }

  explosion(at?: THREE.Vector3, kind: 'frag' | 'flash' | 'smoke' = 'frag'): void {
    if (kind === 'smoke') return this.at('smoke', at);
    this.at(kind === 'flash' ? 'explosion.flash' : 'explosion', at);
    audio.duck(0.55, 0.6, 1.8);
  }

  // ------------------------------------------------------------ moving and acting

  step(u: Unit, at?: THREE.Vector3): void {
    const t = u.template;
    this.at(t === 'mec' ? 'step.mec' : t === 'zombie' ? 'step.drag' : 'step', at, t === 'sectoid' || t === 'xenoid' ? 0.6 : 1, { spread: 0.1 });
  }

  reload(at?: THREE.Vector3): void {
    this.at('reload', at);
  }

  /** Registrar: rummaging through a piece of furniture, a boot or crates. */
  search(at?: THREE.Vector3): void {
    this.at('search', at);
  }

  /** Something turned up while searching. */
  found(at?: THREE.Vector3): void {
    this.at('found', at);
  }

  overwatch(at?: THREE.Vector3): void {
    this.at('overwatch', at);
  }

  hunker(at?: THREE.Vector3): void {
    this.at('hunker', at);
  }

  door(at?: THREE.Vector3): void {
    this.at('door', at);
  }

  glass(at?: THREE.Vector3): void {
    this.at('impact.glass', at);
  }

  drone(at?: THREE.Vector3): void {
    this.at('drone', at);
  }

  heal(at?: THREE.Vector3): void {
    this.at('heal', at);
  }

  shield(at?: THREE.Vector3): void {
    this.at('shield', at);
  }

  stun(at?: THREE.Vector3): void {
    this.at('melee.stun', at);
  }

  hack(at?: THREE.Vector3): void {
    this.at('hack', at);
  }

  hacked(success: boolean, at?: THREE.Vector3): void {
    this.at(success ? 'hack.ok' : 'alarm', at);
  }

  psi(at?: THREE.Vector3): void {
    this.at('psi', at);
  }

  beam(at?: THREE.Vector3): void {
    this.at('beam', at);
  }

  pods(at?: THREE.Vector3): void {
    this.at('pods', at);
  }

  /** The dead arriving: a low moan or two, and feet dragging. */
  moan(at?: THREE.Vector3, voices = 1): void {
    const moan = this.pick('zombie.moan', 'death.zombie');
    for (let i = 0; i < Math.min(3, voices); i++) this.at(moan, at, 0.75, { delay: i * 0.35, rate: moan === 'death.zombie' ? 0.72 : 1, spread: 0.08 });
    for (let i = 0; i < 3; i++) this.at('step.drag', at, 0.6, { delay: 0.2 + i * 0.4 });
  }

  /** Raiders arriving: a pickup's engine and brakes at the edge if there is the sound for it, else boots running in. */
  vehicle(at?: THREE.Vector3): void {
    if ('truck.engine' in SOUNDS) {
      this.at('truck.engine', at);
      return;
    }
    for (let i = 0; i < 6; i++) this.at('step', at, 0.7, { delay: i * 0.17 });
  }

  flare(at?: THREE.Vector3): void {
    this.at('flare', at);
  }

  data(): void {
    sound('data');
  }

  rescue(): void {
    sound('rescue');
  }

  death(u: Unit, at?: THREE.Vector3): void {
    // The siren falls silent the moment it is destroyed, as its lights go out (Arte's models).
    if (u.objective && this.wail) this.silence(SIREN_STOP[this.wail.name] ?? 0);
    const t = u.template;
    const name = TEMPLATES[t].robotic ? 'death.robot' : t === 'sectoid' ? 'death.sectoid' : t === 'xenoid' ? 'death.runner' : t === 'zombie' ? 'death.zombie' : u.team === 'alien' ? 'death.advent' : 'death.human';
    this.at(name, at);
  }

  /** A song of Velmar's Canto (audio/sounds.ts, 'song.*'), at a place, after `delay` seconds. */
  song(name: string, at?: THREE.Vector3, delay = 0): void {
    this.at(`song.${name}`, at, 1, { delay });
  }

  // ------------------------------------------------------------ moments

  /** ¡Contacto!: the stinger, and the music turns tense. */
  contact(): void {
    sound('contact');
    audio.duck(0.6, 0.8, 2);
    this.mood('tense');
  }

  /** Concealment broken. */
  reveal(): void {
    sound('reveal');
    this.mood('tense');
  }

  /** A turn begins: our chirp or their swell; the enemy turn has its own music. */
  turn(alien: boolean, contact: boolean): void {
    sound(alien ? 'turn.alien' : 'turn.xcom');
    this.mood(alien ? 'alien' : contact ? 'tense' : 'calm');
  }

  command(): void {
    sound('command');
  }

  /** Something to notice (an alert ability, reaction fire, a warning). */
  alert(): void {
    sound('overwatch', { gain: 0.8 });
  }

  /** The mission is over: the music stops for the stinger. */
  outcome(victory: boolean): void {
    this.silence(0.5);
    music.play(null, 0.6);
    sound(victory ? 'victory' : 'defeat', { delay: 0.3 });
  }

  // ------------------------------------------------------------ music and ambience

  /** The ambience of the map and the music for how things stand. */
  start(biome: Biome, contact: boolean): void {
    this.softGround = biome === 'wilds';
    ambience.play(biome);
    this.mood(contact ? 'tense' : 'calm');
  }

  mood(piece: Piece): void {
    if (music.current !== piece) music.play(piece, piece === 'alien' ? 1.5 : 3);
  }

  // ------------------------------------------------------------ the siren

  private wail: Wail | null = null;

  /**
   * The sabotage's siren (playtest 15/10 #14): looping from where it stands,
   * heard across the map, louder near and panned by its place on screen,
   * until it is silenced (its death(), outcome(), or `null` when the mission
   * is left). `s`: game.ts's view of the mission, the live object (it is
   * watched for the siren's death); without a live siren, it stops.
   */
  siren(s: GameState | null): void {
    const relay = s && s.mission.kind === 'sabotage' && !s.outcome ? s.units.find((u) => u.id === s.mission.objectiveUnit && u.alive) : undefined;
    if (!s || !relay) return this.silence(0);
    const name = s.mission.alarm ? SIRENS[s.mission.alarm] : SIRENS_BY_MAP[s.biome];
    const at = tileToWorld(relay.pos, 1);
    if (this.wail?.name === name && this.wail.at.equals(at)) return;
    this.silence(0);
    const wail: Wail = { name, at, loop: null, timer: 0 };
    this.wail = wail;
    let buffer: AudioBuffer | null = null;
    void loopBuffer(name).then((b) => (buffer = b));
    // Placed ten times a second as the camera moves; it starts once sound can play (after the first click) and is rendered.
    // `s` is game.ts's view, which takes each event as it is played: the siren also falls silent when it dies out of
    // sight (death() is only called for what the squad sees), as Arte's lights do then.
    const tick = () => {
      if (this.wail !== wail) return;
      if (!relay.alive || s.outcome) return this.silence(SIREN_STOP[name] ?? 0);
      const { level, pan, muffle } = this.far(at);
      if (!wail.loop && buffer) wail.loop = audio.loop(buffer, { bus: 'sfx', gain: level * SIREN_LEVEL, reverb: 0.3 });
      wail.loop?.set(level * SIREN_LEVEL, pan, muffle);
      wail.timer = window.setTimeout(tick, 100);
    };
    tick();
  }

  /** The siren stops: running down over `windDown` seconds, or at once. */
  private silence(windDown: number): void {
    const wail = this.wail;
    if (!wail) return;
    this.wail = null;
    window.clearTimeout(wail.timer);
    wail.loop?.stop(windDown);
  }

  /** Like place(), for a sound meant to carry across the whole map: gentler with distance, losing its highs far away. */
  private far(at: THREE.Vector3): { level: number; pan: number; muffle: number } {
    if (!this.camera) return { level: 0.5, pan: 0, muffle: 0.5 };
    this.ndc.copy(at).project(this.camera);
    const pan = THREE.MathUtils.clamp(this.ndc.x * 0.7, -0.75, 0.75);
    const dist = at.distanceTo(this.focus());
    const offScreen = Math.abs(this.ndc.x) > 1 || Math.abs(this.ndc.y) > 1 || this.ndc.z > 1;
    const level = THREE.MathUtils.clamp(1.1 - dist / 40, 0.18, 1) * (offScreen ? 0.8 : 1);
    return { level, pan, muffle: THREE.MathUtils.clamp(dist / 50, 0, 1) };
  }
}

/** The sound of a unit's gun: soldiers by gun and tier, aliens by their weapon. */
function gunSound(u: Unit, weapon: WeaponDef): string {
  switch (weapon.id) {
    case 'rifle':
    case 'shotgun':
    case 'sniper':
    case 'cannon':
    case 'pistol':
      return `shot.${weapon.id}.${Math.min(3, Math.max(1, u.weaponTier ?? 1))}`;
    case 'mecCannon':
      return 'shot.mec';
    case 'sectoidPistol':
      return 'shot.sectoid';
    case 'xenoPistol':
      return 'shot.xenoid';
    default:
      return 'shot.advent';
  }
}
