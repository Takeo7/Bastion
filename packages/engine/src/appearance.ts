import { PLAYER_COLORS } from './content';
import type { Rng } from './rng';
import type { Appearance, BuildId, ClassId, HeadId, Slot } from './types';

export const HEADS: Record<HeadId, { id: HeadId; name: string }> = {
  helmet: { id: 'helmet', name: 'Casco táctico' },
  hood: { id: 'hood', name: 'Capucha' },
  beret: { id: 'beret', name: 'Boina' },
  cap: { id: 'cap', name: 'Gorra' },
  mask: { id: 'mask', name: 'Máscara' },
  bare: { id: 'bare', name: 'A cara descubierta' },
};

export const BUILDS: Record<BuildId, { id: BuildId; name: string }> = {
  slim: { id: 'slim', name: 'Delgada' },
  normal: { id: 'normal', name: 'Normal' },
  heavy: { id: 'heavy', name: 'Robusta' },
};

export const HEAD_IDS = Object.keys(HEADS) as HeadId[];
export const BUILD_IDS = Object.keys(BUILDS) as BuildId[];

/** Armour swatches offered in the barracks (any `#rrggbb` is accepted). */
export const ARMOR_COLORS = [
  '#3fa9ff', '#ffa630', '#56605a', '#2f3a44', '#6b7d5a', '#8a7a5c',
  '#c9ccd1', '#1d2124', '#8f2f2f', '#2f6b4f', '#5a3f7a', '#c9a13a',
];

export const VISOR_COLORS = ['#9fe3ff', '#7dffb0', '#ffd27a', '#ff6a5a', '#d79fff', '#ffffff'];

export const NAME_MAX = 20;
export const NICKNAME_MAX = 16;

const HEX = /^#[0-9a-f]{6}$/i;

export function isAppearance(a: Appearance): boolean {
  return HEX.test(a.primary) && HEX.test(a.secondary) && HEX.test(a.visor) && a.head in HEADS && a.build in BUILDS;
}

const toLinear = (c: number) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
const toSrgb = (c: number) => (c <= 0.0031308 ? c * 12.92 : 1.055 * c ** (1 / 2.4) - 0.055);

/** Mixes two `#rrggbb` colours in linear space, like THREE.Color.lerp; `t` = 0 is `a`, 1 is `b`. */
function mix(a: string, b: string, t: number): string {
  const channel = (hex: string, i: number) => toLinear(parseInt(hex.slice(1 + i * 2, 3 + i * 2), 16) / 255);
  const out = [0, 1, 2].map((i) => Math.round(toSrgb(channel(a, i) + (channel(b, i) - channel(a, i)) * t) * 255));
  return '#' + out.map((v) => v.toString(16).padStart(2, '0')).join('');
}

/** The look soldiers had before customization: their player's colour, class head and build. */
export function defaultAppearance(cls: ClassId, owner: Slot): Appearance {
  const accent = PLAYER_COLORS[owner];
  return {
    primary: mix(accent, '#56605a', 0.55),
    secondary: accent,
    visor: '#9fe3ff',
    head: cls === 'sharpshooter' ? 'hood' : 'helmet',
    build: cls === 'grenadier' ? 'heavy' : 'normal',
  };
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
