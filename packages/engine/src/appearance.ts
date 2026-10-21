import { PLAYER_COLORS } from './content';
import type { Rng } from './rng';
import type { Appearance, ArmorPart, BuildId, HeadId, Slot } from './types';

/**
 * Head pieces. `paint` is what the headgear is painted with until it gets a
 * colour of its own (`helmet`): the armour (`primary`), the trim
 * (`secondary`), or nothing (a mask, a bare head).
 */
export const HEADS: Record<HeadId, { id: HeadId; name: string; paint: 'armour' | 'trim' | null }> = {
  helmet: { id: 'helmet', name: 'Casco táctico', paint: 'armour' },
  hood: { id: 'hood', name: 'Capucha', paint: 'trim' },
  beret: { id: 'beret', name: 'Boina', paint: 'trim' },
  cap: { id: 'cap', name: 'Gorra', paint: 'trim' },
  mask: { id: 'mask', name: 'Máscara', paint: null },
  bare: { id: 'bare', name: 'A cara descubierta', paint: null },
};

/** Armour pieces with a colour of their own (barracks), head to feet. */
export const ARMOR_PARTS: Record<ArmorPart, { id: ArmorPart; name: string }> = {
  helmet: { id: 'helmet', name: 'Casco' },
  torso: { id: 'torso', name: 'Torso' },
  arms: { id: 'arms', name: 'Brazos' },
  legs: { id: 'legs', name: 'Piernas' },
};

export const BUILDS: Record<BuildId, { id: BuildId; name: string }> = {
  slim: { id: 'slim', name: 'Delgada' },
  normal: { id: 'normal', name: 'Normal' },
  heavy: { id: 'heavy', name: 'Robusta' },
};

export const HEAD_IDS = Object.keys(HEADS) as HeadId[];
export const BUILD_IDS = Object.keys(BUILDS) as BuildId[];
export const ARMOR_PART_IDS = Object.keys(ARMOR_PARTS) as ArmorPart[];

/** Armour swatches offered in the barracks (any `#rrggbb` is accepted). */
export const ARMOR_COLORS = [
  '#a678ff', '#ffa630', '#56605a', '#2f3a44', '#6b7d5a', '#8a7a5c',
  '#c9ccd1', '#1d2124', '#8f2f2f', '#2f6b4f', '#3fa9ff', '#c9a13a',
];

export const VISOR_COLORS = ['#9fe3ff', '#7dffb0', '#ffd27a', '#ff6a5a', '#d79fff', '#ffffff'];

export const NAME_MAX = 20;
export const NICKNAME_MAX = 16;

const HEX = /^#[0-9a-f]{6}$/i;

/** A survivor model's id: the list lives in the client (Synty), the engine only checks its shape. */
export const CHARACTER_ID = /^[a-z][a-zA-Z0-9]{0,23}$/;

export function isAppearance(a: Appearance): boolean {
  return (
    HEX.test(a.primary) &&
    HEX.test(a.secondary) &&
    HEX.test(a.visor) &&
    HEAD_IDS.includes(a.head) &&
    BUILD_IDS.includes(a.build) &&
    (a.sex === undefined || a.sex === 'f' || a.sex === 'm') &&
    ARMOR_PART_IDS.every((part) => a[part] === undefined || HEX.test(a[part])) &&
    (a.character === undefined || CHARACTER_ID.test(a.character))
  );
}

/**
 * The colour a piece is painted in: its own, or else what it wore before
 * pieces could be painted — the armour colour, or the trim for a beret, cap
 * or hood.
 */
export function partColor(a: Appearance, part: ArmorPart): string {
  return a[part] ?? (part === 'helmet' && HEADS[a.head].paint === 'trim' ? a.secondary : a.primary);
}

/**
 * A clean copy to store: the known fields only, in the order defaultAppearance
 * writes them, and a piece only where it differs from what it would follow,
 * so an unchanged look still compares equal to the default one.
 */
export function normalizeAppearance(a: Appearance): Appearance {
  const out: Appearance = { primary: a.primary, secondary: a.secondary, visor: a.visor, head: a.head, build: a.build };
  if (a.sex) out.sex = a.sex;
  for (const part of ARMOR_PART_IDS) {
    const own = a[part];
    if (own !== undefined && own.toLowerCase() !== partColor(out, part).toLowerCase()) out[part] = own;
  }
  if (a.character) out.character = a.character;
  return out;
}

const toLinear = (c: number) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
const toSrgb = (c: number) => (c <= 0.0031308 ? c * 12.92 : 1.055 * c ** (1 / 2.4) - 0.055);

/** Mixes two `#rrggbb` colours in linear space, like THREE.Color.lerp; `t` = 0 is `a`, 1 is `b`. */
function mix(a: string, b: string, t: number): string {
  const channel = (hex: string, i: number) => toLinear(parseInt(hex.slice(1 + i * 2, 3 + i * 2), 16) / 255);
  const out = [0, 1, 2].map((i) => Math.round(toSrgb(channel(a, i) + (channel(b, i) - channel(a, i)) * t) * 255));
  return '#' + out.map((v) => v.toString(16).padStart(2, '0')).join('');
}

/** What a soldier starts with on the head (docs/SIDEKICK.md): mostly bare or a cap, now and then a hood, a beret, a helmet or a mask. */
const START_HEADS: HeadId[] = ['bare', 'bare', 'bare', 'cap', 'cap', 'hood', 'beret', 'helmet', 'mask'];

/** FNV-1a: the same number for the same soldier, everywhere. */
function hashOf(s: string): number {
  let h = 2166136261;
  for (let i = 0; i < s.length; i++) h = Math.imul(h ^ s.charCodeAt(i), 16777619);
  return h >>> 0;
}

/**
 * The look soldiers have before customization: their player's colour (and their sex, when known). No classes any
 * more (docs/FASE9.md). With `seed` (the soldier's id), its own head and build, so that a squad of Sidekick figures
 * isn't eight helmets alike (docs/SIDEKICK.md); without, the look of before, all helmets of a normal build, which
 * migrateCampaign recognises.
 */
export function defaultAppearance(owner: Slot, accent = PLAYER_COLORS[owner], sex?: 'f' | 'm', seed?: string): Appearance {
  const h = seed === undefined ? null : hashOf(seed);
  const out: Appearance = {
    primary: mix(accent, '#56605a', 0.55),
    secondary: accent,
    visor: '#9fe3ff',
    head: h === null ? 'helmet' : START_HEADS[h % START_HEADS.length]!,
    build: h === null ? 'normal' : BUILD_IDS[(h >>> 8) % BUILD_IDS.length]!,
  };
  if (sex) out.sex = sex;
  return out;
}

/** A random look, like XCOM's "randomize" button. */
export function randomAppearance(rng: Rng): Appearance {
  return {
    primary: rng.pick(ARMOR_COLORS),
    secondary: rng.pick(ARMOR_COLORS),
    visor: rng.pick(VISOR_COLORS),
    head: rng.pick(HEAD_IDS),
    build: rng.pick(BUILD_IDS),
  };
}

/** "Clara «Halcón» Bosch": the nickname goes after the first name, XCOM-style. */
export function displayName(name: string, nickname?: string): string {
  if (!nickname) return name;
  const space = name.indexOf(' ');
  return space < 0 ? `${name} «${nickname}»` : `${name.slice(0, space)} «${nickname}» ${name.slice(space + 1)}`;
}
