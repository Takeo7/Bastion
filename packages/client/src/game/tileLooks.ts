// How the move's preview looks on the board: the range of one action and of
// the dash, the path, the pointer's ring and the plans (playtest 18/10 #14:
// «parecen infantiles: los colores, el grosor, que no son como una luz»).
// Arturo asked for five proposals to choose from, and chose (18/10) «Degradado»
// with «Tiza»'s outlines, in bone and rust («Hueso y óxido»): that one is the
// default now. Until he confirms it
// playing, the others stay behind a hidden key in the battle (F8, game.ts),
// and the choice is kept in this browser. overlays.ts draws whichever is on.

export type TileLookId =
  | 'clasico'
  | 'luz'
  | 'relleno'
  | 'tiza'
  | 'puntos'
  | 'degradado'
  | 'degradadoTiza'
  | 'huesoOxido'
  | 'salviaAmbar'
  | 'lonaOcre'
  | 'militarSenal'
  | 'linternaBrasa';

export interface TileLook {
  id: TileLookId;
  /** Its name and what it is, for the proposals' sheet and the key's notice. */
  name: string;
  line: string;
  /**
   * The range's contours, one action's and the dash's: their width (px), colours,
   * how far past 1 the core goes for the bloom (glow), a soft halo under them
   * (light; a chalk stroke's is a dark shadow, for the snow), a hand-drawn
   * stroke (chalk) and the dash's contour dashed or not.
   */
  edges: { width: number; walk: number; dash: number; glow: number; halo: boolean; chalk: boolean; dashDashed: boolean; opacity?: number; shadeWidth?: number } | null;
  /** The tiles themselves: a flat wash, or bright at the range's edge and fading inwards. */
  fill: { kind: 'flat' | 'fade'; walk: number; dash: number; alpha: number } | null;
  /** A mark in the middle of each tile in reach: filled for one action, hollow for the dash. */
  dots: { radius: number; walk: number; dash: number } | null;
  /** The tiles' squares round the pointer. */
  near: boolean;
  /** A soft dark stroke under the edges and the path: a light line still reads on snow and on sand. */
  shade: boolean;
  /** The path: colours (walking, dashing), width, glow, a dark casing or a halo under it, dotted (marks like the dots) or chalk. */
  path: { color: number; dash: number; width: number; glow: number; casing: boolean; halo: boolean; dotted: boolean; chalk: boolean };
  /** The pointer's ring: inner and outer radius (tiles). */
  ring: [number, number];
  /** Its colour on a tile one action away and on one for the dash (game.ts); the old cyan and yellow if missing. */
  ringColors?: [number, number];
  /** A plan's line: width, a halo under it, its dashes (`solid`: none), and the ring on its end (inner and outer radius). */
  plan: { width: number; halo: boolean; dashSize: number; gapSize: number; ring: [number, number]; solid?: boolean };
  /**
   * Planning without the command: one colour for every player instead of one's own dimmed (playtest
   * 18/10 #31: Ana's orange looked like the rust of the dash). Missing: one's own colour, dimmed.
   */
  planning?: number;
}

export const TILE_LOOKS: readonly TileLook[] = [
  {
    id: 'clasico',
    name: 'Clásico',
    line: 'Las líneas de ahora: bordes azul y amarillo, camino blanco con funda oscura.',
    edges: { width: 2.5, walk: 0x6cc4ff, dash: 0xffc93a, glow: 1, halo: false, chalk: false, dashDashed: true },
    fill: null,
    dots: null,
    near: true,
    shade: false,
    path: { color: 0xeafaff, dash: 0xffd34d, width: 4, glow: 1, casing: true, halo: false, dotted: false, chalk: false },
    ring: [0.3, 0.42],
    plan: { width: 3, halo: false, dashSize: 0.28, gapSize: 0.18, ring: [0.3, 0.44] },
  },
  {
    id: 'luz',
    name: 'Luz',
    line: 'Líneas finas que brillan, con un halo suave; colores apagados.',
    edges: { width: 1.4, walk: 0x8fd8e8, dash: 0xe0b060, glow: 1.6, halo: true, chalk: false, dashDashed: true },
    fill: null,
    dots: null,
    near: true,
    shade: true,
    path: { color: 0xe4f2f4, dash: 0xf0cf86, width: 2.4, glow: 1.6, casing: false, halo: true, dotted: false, chalk: false },
    ring: [0.33, 0.41],
    plan: { width: 1.8, halo: true, dashSize: 0.24, gapSize: 0.18, ring: [0.33, 0.41] },
  },
  {
    id: 'relleno',
    name: 'Relleno',
    line: 'Casillas rellenas muy suaves, sin borde, como en XCOM 2; el camino, una línea fina.',
    edges: null,
    fill: { kind: 'flat', walk: 0x5cc8e8, dash: 0xeaa640, alpha: 0.21 },
    dots: null,
    near: false,
    shade: true,
    path: { color: 0xeef8fa, dash: 0xf6d690, width: 2, glow: 1.4, casing: false, halo: true, dotted: false, chalk: false },
    ring: [0.36, 0.44],
    plan: { width: 1.8, halo: true, dashSize: 0.24, gapSize: 0.18, ring: [0.36, 0.44] },
  },
  {
    id: 'tiza',
    name: 'Tiza',
    line: 'Solo el contorno, como un trazo de tiza en el suelo, sin brillo; el camino, a trazos.',
    edges: { width: 2.6, walk: 0xeee6d4, dash: 0xd2a45c, glow: 1, halo: true, chalk: true, dashDashed: true },
    fill: null,
    dots: null,
    near: false,
    shade: true,
    path: { color: 0xf2ecdc, dash: 0xe2bc78, width: 2.6, glow: 1, casing: false, halo: false, dotted: false, chalk: true },
    ring: [0.32, 0.4],
    plan: { width: 2.2, halo: false, dashSize: 0.2, gapSize: 0.14, ring: [0.32, 0.4] },
  },
  {
    id: 'puntos',
    name: 'Puntos',
    line: 'Una marca en el centro de cada casilla, sin bordes; el camino, punteado.',
    edges: null,
    fill: null,
    dots: { radius: 0.085, walk: 0x8fd8e8, dash: 0xe0b060 },
    near: false,
    shade: false,
    path: { color: 0xeef8fa, dash: 0xf6d690, width: 4.5, glow: 1.5, casing: false, halo: false, dotted: true, chalk: false },
    ring: [0.3, 0.38],
    plan: { width: 3.5, halo: false, dashSize: 0.04, gapSize: 0.22, ring: [0.3, 0.38] },
  },
  {
    id: 'degradado',
    name: 'Degradado',
    line: 'El borde luce y se apaga hacia dentro, como en Demeo o Dead Season.',
    edges: { width: 1.2, walk: 0x8fd8e8, dash: 0xe0b060, glow: 1.4, halo: false, chalk: false, dashDashed: true },
    fill: { kind: 'fade', walk: 0x5cc8e8, dash: 0xe8b85a, alpha: 0.42 },
    dots: null,
    near: false,
    shade: true,
    path: { color: 0xe4f2f4, dash: 0xf0cf86, width: 2.2, glow: 1.5, casing: false, halo: true, dotted: false, chalk: false },
    ring: [0.33, 0.41],
    plan: { width: 1.8, halo: true, dashSize: 0.24, gapSize: 0.18, ring: [0.33, 0.41] },
  },
  {
    // Arturo's choice (18/10): «Degradado»'s fill and path, with «Tiza»'s outlines on top.
    id: 'degradadoTiza',
    name: 'Degradado con tiza',
    line: 'El relleno luce en el borde y se apaga hacia dentro; encima, el contorno a tiza, a trazos al correr.',
    edges: { width: 2.6, walk: 0xeee6d4, dash: 0xd2a45c, glow: 1, halo: true, chalk: true, dashDashed: true },
    fill: { kind: 'fade', walk: 0x5cc8e8, dash: 0xe8b85a, alpha: 0.42 },
    dots: null,
    near: false,
    shade: true,
    path: { color: 0xe4f2f4, dash: 0xf0cf86, width: 2.2, glow: 1.5, casing: false, halo: true, dotted: false, chalk: false },
    ring: [0.33, 0.41],
    plan: { width: 1.8, halo: true, dashSize: 0.24, gapSize: 0.18, ring: [0.33, 0.41] },
  },
  // Arturo, 18/10: the blue is «demasiado sci-fi». Five colours for «Degradado con tiza», survival-like,
  // each a pair (one action, light; the dash, darker, and dashed), clear of what means something already:
  // the players' purple and orange, the enemies' red, the objective's green, the covers' cyan and amber.
  clean(chalkFade('huesoOxido', 'Hueso y óxido', 'Hueso a una acción; óxido al correr.', { walk: 0xcdb486, dash: 0x9a4a2c, chalkWalk: 0xf3ecdc, chalkDash: 0xc4704a, pathWalk: 0xf6f0e2, pathDash: 0xe2a684 })),
  chalkFade('salviaAmbar', 'Salvia y ámbar', 'Verde salvia apagado a una acción; ámbar viejo al correr.', { walk: 0x7fa276, dash: 0xb8862e, chalkWalk: 0xdde9d6, chalkDash: 0xd4a454, pathWalk: 0xeaf3e4, pathDash: 0xefcf8c }),
  chalkFade('lonaOcre', 'Lona y ocre', 'Caqui de lona a una acción; ocre tostado al correr.', { walk: 0xb3a06a, dash: 0x8a5e26, chalkWalk: 0xefe6ca, chalkDash: 0xb48644, pathWalk: 0xf6efdc, pathDash: 0xdcb684 }),
  chalkFade('militarSenal', 'Verde militar y amarillo señal', 'Oliva a una acción; amarillo de chaleco al correr (el naranja chocaba con el de Ana).', { walk: 0x6f7e36, dash: 0xd2cc38, chalkWalk: 0xc9d1a0, chalkDash: 0xe4de6a, pathWalk: 0xe6ebcf, pathDash: 0xf0ec9a }),
  chalkFade('linternaBrasa', 'Luz de linterna', 'Luz cálida de linterna a una acción; brasa al correr.', { walk: 0xf0d282, dash: 0xbe5a2c, chalkWalk: 0xfff7e2, chalkDash: 0xe0845a, pathWalk: 0xfffaee, pathDash: 0xf4b48c }),
];

/**
 * Arturo's look made cleaner (playtest 18/10 #31): no dashed line anywhere, the
 * dash told by its colour and brightness; a plain stroke instead of the
 * chalk's two that wobble (they read as a dashed line with its gaps closed);
 * all of it fainter; and a cold blue to plan with, apart from the rust.
 */
function clean(look: TileLook): TileLook {
  return {
    ...look,
    edges: look.edges && { ...look.edges, width: 1.8, halo: false, chalk: false, dashDashed: false, opacity: 0.75, shadeWidth: 2.2 },
    fill: look.fill && { ...look.fill, alpha: 0.45 },
    path: { ...look.path, width: 2, glow: 1.15, halo: false },
    plan: { ...look.plan, width: 1.8, halo: false, solid: true },
    planning: 0x7fb2d4,
  };
}

/** «Degradado con tiza» in another pair of colours (Arturo, 18/10). */
function chalkFade(
  id: TileLookId,
  name: string,
  line: string,
  c: { walk: number; dash: number; chalkWalk: number; chalkDash: number; pathWalk: number; pathDash: number },
): TileLook {
  return {
    id,
    name,
    line,
    edges: { width: 2.6, walk: c.chalkWalk, dash: c.chalkDash, glow: 1, halo: true, chalk: true, dashDashed: true },
    // Earth colours tell less from the ground than the blue did: a stronger fill.
    fill: { kind: 'fade', walk: c.walk, dash: c.dash, alpha: 0.58 },
    dots: null,
    near: false,
    shade: true,
    path: { color: c.pathWalk, dash: c.pathDash, width: 2.2, glow: 1.5, casing: false, halo: true, dotted: false, chalk: false },
    ring: [0.33, 0.41],
    ringColors: [c.pathWalk, c.pathDash],
    plan: { width: 1.8, halo: true, dashSize: 0.24, gapSize: 0.18, ring: [0.33, 0.41] },
  };
}

/** The look for everyone until one is picked with F8. */
const DEFAULT: TileLookId = 'huesoOxido';

/** A new key with each choice (18/10: the look, then its colours): whatever was tried with F8 before goes, and everyone starts on DEFAULT. */
const KEY = 'bastion.tileLook3';

/** The look chosen in this browser (F8 in the battle), DEFAULT otherwise. */
export function tileLook(): TileLook {
  const fallback = TILE_LOOKS.find((l) => l.id === DEFAULT)!;
  try {
    const id = localStorage.getItem(KEY);
    return TILE_LOOKS.find((l) => l.id === id) ?? fallback;
  } catch {
    return fallback;
  }
}

/** The next look, kept for the next time. */
export function cycleTileLook(): TileLook {
  const now = tileLook();
  const next = TILE_LOOKS[(TILE_LOOKS.indexOf(now) + 1) % TILE_LOOKS.length]!;
  try {
    localStorage.setItem(KEY, next.id);
  } catch {
    /* storage unavailable: it holds until the page closes */
  }
  return next;
}
