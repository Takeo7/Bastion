import * as THREE from 'three';
import { TEMPLATES, type Biome, type Unit, type WeaponDef } from '@bastion/engine';
import { ambience } from '../audio/ambience';
import { sound } from '../audio/bank';
import { audio } from '../audio/engine';
import { music, type Piece } from '../audio/music';

/**
 * Combat sound: what game.ts calls when it animates an event. Each sound is
 * placed where it happens (stereo by its place on screen, quieter far from
 * where the camera looks), guns sound like their class, tier and side, and
 * the music and ambience follow the mission (biome, concealment, contact,
 * the enemy turn). The sounds themselves are made in audio/sounds.ts.
 */
export class Sfx {
  private camera: THREE.Camera | null = null;
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

  private at(name: string, at?: THREE.Vector3, gain = 1, extra: { delay?: number; spread?: number } = {}): void {
    sound(name, { ...this.place(at, gain), ...extra });
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
    const t = u.template;
    const name = TEMPLATES[t].robotic ? 'death.robot' : t === 'sectoid' || t === 'xenoid' ? 'death.sectoid' : t === 'zombie' ? 'death.zombie' : u.team === 'alien' ? 'death.advent' : 'death.human';
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
    music.play(null, 0.6);
    sound(victory ? 'victory' : 'defeat', { delay: 0.3 });
  }

  // ------------------------------------------------------------ music and ambience

  /** The ambience of the map and the music for how things stand. */
  start(biome: Biome, contact: boolean): void {
    ambience.play(biome);
    this.mood(contact ? 'tense' : 'calm');
  }

  mood(piece: Piece): void {
    if (music.current !== piece) music.play(piece, piece === 'alien' ? 1.5 : 3);
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
