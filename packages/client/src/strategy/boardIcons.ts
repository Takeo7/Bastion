import type { CellId } from '@bastion/engine';

// The hex board's icons (docs/FASE10.md §6: «cada habilidad su icono propio»),
// drawn by us: 24×24 line drawings in the pen of ui/icons.ts (stroke =
// currentColor, round ends; `.fill` parts filled), built from a few pieces
// (a crosshair, a bullet, a blade, a figure, a heart, a cloud…) and, for each
// skill, what its name says. hexBoard.css colours them by the cell's state.

// ------------------------------------------------------------------ pieces

const f = (n: number) => (Math.round(n * 100) / 100).toString();
const circle = (x: number, y: number, r: number, cls = '') => `<circle${cls ? ` class="${cls}"` : ''} cx="${f(x)}" cy="${f(y)}" r="${f(r)}"/>`;
const path = (d: string, cls = '') => `<path${cls ? ` class="${cls}"` : ''} d="${d}"/>`;
const at = (x: number, y: number, s: number, body: string) => `<g transform="translate(${f(x)} ${f(y)}) scale(${f(s)})">${body}</g>`;
const rot = (deg: number, body: string) => `<g transform="rotate(${deg} 12 12)">${body}</g>`;

/** A crosshair centred at (x, y). */
const crosshair = (x = 12, y = 12, r = 6) => circle(x, y, r) + path(`M${f(x)} ${f(y - r - 3)}v4M${f(x)} ${f(y + r - 1)}v4M${f(x - r - 3)} ${f(y)}h4M${f(x + r - 1)} ${f(y)}h4`) + circle(x, y, 1, 'fill');
/** A bullet standing up, its tip at (x, y), `h` tall. */
const bullet = (x: number, y: number, h = 12) => path(`M${f(x - 2.4)} ${f(y + h)}V${f(y + 4)}Q${f(x)} ${f(y - 1)} ${f(x + 2.4)} ${f(y + 4)}V${f(y + h)}Z`) + path(`M${f(x - 2.4)} ${f(y + h - 3)}h4.8`);
/** A pistol, facing right. */
const pistol = path('M3 8h15l1.5 3H10l-1 4H5.5l1-4H3z') + path('M10 11l.8 5h3l-.8-5');
/** A rifle, facing right. */
const rifle = path('M2 13h4l2-2h11l3 1-3 1H11l-1.5 3H6l1-2H2z') + path('M14 11V9');
/** A short blade from bottom-left to top-right. */
const blade = path('M5 19L17 7l2.5-1.5L18 8 6 20z') + path('M4 16l4 4');
/** A machete. */
const machete = path('M4 20l2-2M6 18L18 6c2-1.5 3.5-.5 2.5 1.5L8 20') + path('M3 17l4 4');
/** A small four-pointed spark (a critical). */
const spark = (x: number, y: number, r = 3.2) => path(`M${f(x)} ${f(y - r)}L${f(x + r * 0.3)} ${f(y - r * 0.3)}L${f(x + r)} ${f(y)}L${f(x + r * 0.3)} ${f(y + r * 0.3)}L${f(x)} ${f(y + r)}L${f(x - r * 0.3)} ${f(y + r * 0.3)}L${f(x - r)} ${f(y)}L${f(x - r * 0.3)} ${f(y - r * 0.3)}Z`, 'fill');
/** A plus, centred at (x, y). */
const plus = (x: number, y: number, r = 2.6) => path(`M${f(x - r)} ${f(y)}h${f(2 * r)}M${f(x)} ${f(y - r)}v${f(2 * r)}`);
/** A person standing, head at (x, y), `s` scale. */
const figure = (x: number, y: number, s = 1) => at(x, y, s, circle(0, 0, 2.2) + path('M0 2.5v7M-4 5h8M0 9.5l-3 5M0 9.5l3 5'));
/** A head in profile, facing right, centred at (x, y). */
const head = (x = 12, y = 11) => path(`M${f(x - 5)} ${f(y + 7)}v-3c-2-1.5-2.5-4-2-6.5C${f(x - 6)} ${f(y - 5)} ${f(x - 2)} ${f(y - 7)} ${f(x + 1)} ${f(y - 6.5)}c3 .5 5 3 5 6l1.5 2.5-1.5.6v2c0 1-1 1.5-2 1.4L${f(x + 1)} ${f(y + 4)}v3`);
/** A heart centred at (x, y). */
const heart = (x = 12, y = 12, s = 1) => at(x, y, s, path('M0 7S-7 3-7-2a3.6 3.6 0 0 1 7-1.5A3.6 3.6 0 0 1 7-2C7 3 0 7 0 7z'));
/** A medical cross. */
const cross = (x = 12, y = 12, r = 6, w = 2.2) => path(`M${f(x - w)} ${f(y - r)}h${f(2 * w)}v${f(r - w)}h${f(r - w)}v${f(2 * w)}h${f(-(r - w))}v${f(r - w)}h${f(-2 * w)}v${f(-(r - w))}h${f(-(r - w))}v${f(-2 * w)}h${f(r - w)}z`);
/** A first-aid box with its cross. */
const kit = (x = 12, y = 13, s = 1) => at(x, y, s, path('M-8-5h16v11h-16z') + path('M-3-5v-2.5h6V-5') + path('M0-2v5M-2.5.5h5'));
/** A shield. */
const shield = (x = 12, y = 12, s = 1) => at(x, y, s, path('M0-8l7 2.5v5C7 4 4 7 0 8.5-4 7-7 4-7-.5v-5z'));
/** An eye, centred at (x, y). */
const eye = (x = 12, y = 12, s = 1) => at(x, y, s, path('M-9 0s3.5-6 9-6 9 6 9 6-3.5 6-9 6-9-6-9-6z') + circle(0, 0, 2.6));
/** A grenade. */
const grenade = (x = 11, y = 14, s = 1) => at(x, y, s, circle(0, 0, 5.5) + path('M-2-5.5V-7.5h4V-5.5M2-7.5l3-2M-3 0h6'));
/** A stone. */
const stone = (x = 12, y = 13, s = 1) => at(x, y, s, path('M-6 3l-1-4 3-4 5-1 4 3 1 5-3 3-6 .5z'));
/** A cloud of smoke. */
const cloud = (x = 12, y = 12, s = 1) => at(x, y, s, path('M-6 4a3.5 3.5 0 0 1-.5-7A5 5 0 0 1 3-5a3.6 3.6 0 0 1 4 3.6A3 3 0 0 1 6.5 4z'));
/** A burst: an explosion's star. */
const burst = (x = 12, y = 12, r = 8) =>
  path(
    Array.from({ length: 16 }, (_, i) => {
      const a = (Math.PI / 8) * i;
      const rr = i % 2 ? r * 0.45 : r;
      return `${i ? 'L' : 'M'}${f(x + rr * Math.cos(a))} ${f(y + rr * Math.sin(a))}`;
    }).join('') + 'Z',
  );
/** Rings spreading out (a blast, a sound). */
const rings = (x = 12, y = 12, n = 3, step = 3, from = 2) => Array.from({ length: n }, (_, i) => circle(x, y, from + i * step)).join('');
/** An arc thrown from bottom-left, dashed, landing at (x, y). */
const arc = (x = 19, y = 17) => path(`M3 19Q${f((3 + x) / 2)} 1 ${f(x)} ${f(y)}`, 'dash');
/** A wrench. */
const wrench = (x = 12, y = 12, s = 1) => at(x, y, s, path('M3.5-7.5a4 4 0 0 0 4 5.5L-5 10.5-7.5 8 5-4.5a4 4 0 0 0-1.5-3z'));
/** A gear. */
const gear = (x = 12, y = 12, r = 6) =>
  path(
    Array.from({ length: 16 }, (_, i) => {
      const a = (Math.PI / 8) * i + Math.PI / 16;
      const rr = i % 4 < 2 ? r + 1.6 : r;
      return `${i ? 'L' : 'M'}${f(x + rr * Math.cos(a))} ${f(y + rr * Math.sin(a))}`;
    }).join('') + 'Z',
  ) + circle(x, y, r * 0.38);
/** A magnifier. */
const lens = (x = 10, y = 10, r = 5.5) => circle(x, y, r) + path(`M${f(x + r * 0.72)} ${f(y + r * 0.72)}L20.5 20.5`);
/** A hand open, palm up-ish. */
const hand = (x = 12, y = 12, s = 1) => at(x, y, s, path('M-6 8v-6l-2-3a1.4 1.4 0 0 1 2.4-1.4L-4 0v-7a1.3 1.3 0 0 1 2.6 0v5-6.5a1.3 1.3 0 0 1 2.6 0V-3v-4.5a1.3 1.3 0 0 1 2.6 0V2c0 4-2.5 6-5 6z'));
/** A fist. */
const fist = (x = 12, y = 12, s = 1) => at(x, y, s, path('M-6-2a2 2 0 0 1 4 0v-2a2 2 0 0 1 4 0 2 2 0 0 1 4 0v8c0 3-2.5 5-5.5 5H-2c-2.5 0-4-2-4-4z') + path('M-6 0h-1.5a1.5 1.5 0 0 0 0 3H-6'));
/** A wall of bricks. */
const wall = (x = 12, y = 14, w = 16, rowsN = 3) => {
  const hh = 3.4;
  let d = `M${f(x - w / 2)} ${f(y - (rowsN * hh) / 2)}h${f(w)}v${f(rowsN * hh)}h${f(-w)}z`;
  for (let i = 1; i < rowsN; i++) d += `M${f(x - w / 2)} ${f(y - (rowsN * hh) / 2 + i * hh)}h${f(w)}`;
  for (let i = 0; i < rowsN; i++) {
    const off = i % 2 ? w / 4 : w / 2;
    d += `M${f(x - w / 2 + off)} ${f(y - (rowsN * hh) / 2 + i * hh)}v${f(hh)}`;
  }
  return path(d);
};
/** A chevron up (a rank, more), or `n` stacked. */
const chevrons = (x: number, y: number, n = 1) => Array.from({ length: n }, (_, i) => path(`M${f(x - 3)} ${f(y + 2 + i * 3)}l3-3 3 3`)).join('');
/** A lightning bolt. */
const bolt = (x = 12, y = 12, s = 1) => at(x, y, s, path('M1-8l-6 9h5l-1 7 6-9H0z'));
/** A drop. */
const drop = (x = 12, y = 12, s = 1) => at(x, y, s, path('M0-7C3-2 5 1 5 3.5a5 5 0 0 1-10 0C-5 1-3-2 0-7z'));
/** A snowflake. */
const snow = (x = 12, y = 12, r = 6) => path(`M${f(x)} ${f(y - r)}v${f(2 * r)}M${f(x - r * 0.87)} ${f(y - r / 2)}l${f(r * 1.73)} ${f(r)}M${f(x - r * 0.87)} ${f(y + r / 2)}l${f(r * 1.73)} ${f(-r)}`);
/** Speed lines to the left of (x, y). */
const speed = (x: number, y: number) => path(`M${f(x - 8)} ${f(y - 3)}h5M${f(x - 10)} ${f(y)}h6M${f(x - 8)} ${f(y + 3)}h5`);
/** A running person. */
const runner = (x = 13, y = 5, s = 1) => at(x, y, s, circle(1.5, 0, 2) + path('M0 3l-1.5 6 4 2.5L1 17M-1.5 9l-4 3.5M0 3.5l4 2 2.5-1.5M-1 4.5L-5 6'));
/** A crouching person. */
const croucher = (x = 12, y = 7) => at(x, y, 1, circle(-1, 0, 2.2) + path('M0 3l3 4-1 5h4M3 7L-3 9l-1 4M0 3.5l-4 2'));
/** A tower. */
const tower = (x = 12, y = 12) => path(`M${f(x - 4)} 21l1-11h6l1 11zM${f(x - 5)} 10h10V6.5h-2V8h-2V6.5h-2V8H${f(x - 3)}V6.5H${f(x - 5)}z`);
/** A roof (a building's top) with its edge. */
const roof = (y = 15) => path(`M3 ${f(y)}l9-6 9 6M5 ${f(y - 1.2)}V21h14v-${f(21 - y + 1.2)}`);
/** A clock's face. */
const clock = (x = 12, y = 12, r = 7) => circle(x, y, r) + path(`M${f(x)} ${f(y - r + 2.5)}V${f(y)}l3 2`);
/** Two people side by side. */
const pair = (x = 12, y = 6) => figure(x - 4.5, y, 0.85) + figure(x + 4.5, y, 0.85);
/** A skull. */
const skull = (x = 12, y = 11) => path(`M${f(x)} ${f(y - 8)}a7 7 0 0 0-7 7c0 2.5 1.3 4 3 5v3h8v-3c1.7-1 3-2.5 3-5a7 7 0 0 0-7-7z`) + circle(x - 2.7, y, 1.4, 'fill') + circle(x + 2.7, y, 1.4, 'fill');
/** A chain of two links. */
const chain = (x = 12, y = 12) => at(x, y, 1, path('M-2 2a3.6 3.6 0 0 1 0-5l2-2a3.6 3.6 0 0 1 5 5L3.6 1.5') + path('M2-2a3.6 3.6 0 0 1 0 5l-2 2a3.6 3.6 0 0 1-5-5L-3.6-1.5'));
/** Sound waves to the right of (x, y). */
const waves = (x: number, y: number) => path(`M${f(x)} ${f(y - 3)}a4 4 0 0 1 0 6M${f(x + 3)} ${f(y - 6)}a8 8 0 0 1 0 12`);
/** A flag on a pole. */
const flag = (x = 7, y = 3) => path(`M${f(x)} 21V${f(y)}M${f(x)} ${f(y + 1)}h10l-2.5 3.5L${f(x + 10)} ${f(y + 8)}H${f(x)}`);
/** A box with a lid (a crate, a case). */
const caseBox = (x = 12, y = 13, w = 16, hh = 10) => path(`M${f(x - w / 2)} ${f(y - hh / 2)}h${f(w)}v${f(hh)}h${f(-w)}z`) + path(`M${f(x - 3)} ${f(y - hh / 2)}v-2.5h6v2.5`);
/** A bed. */
const bed = path('M3 18V8M3 14h18v4M21 18v-4M5 14v-3h6v3') + circle(7, 10, 0);
/** A tent. */
const tent = path('M2 20L12 4l10 16zM12 20l-3-6h6z');
/** An arm flexed. */
const arm = path('M4 20c0-4 1-8 4-10l3-5c1-1.5 3-1 3 1l-1 3c3-1 6 1 6 4.5S16 20 12 20z');
/** A boot. */
const boot = path('M7 3v11l-3 3v3h16v-2c0-2-3-3.5-6-4l-1-11z') + path('M7 9h6');
/** Lungs. */
const lungsPath = path('M12 3v8M12 9l-3 3M12 9l3 3') + path('M9 7C6 7 4 11 4 16c0 3 2 4 4 3l1-1V9zM15 7c3 0 5 4 5 9 0 3-2 4-4 3l-1-1V9z');

// ------------------------------------------------------------------ the icons

const ICONS: Record<CellId, string> = {
  core: path('M12 2l8.7 5v10L12 22l-8.7-5V7z') + circle(12, 12, 3.2, 'fill') + path('M12 5.5v3M12 15.5v3M6.2 8.8l2.6 1.5M15.2 13.7l2.6 1.5M6.2 15.2l2.6-1.5M15.2 10.3l2.6-1.5'),

  // ---- Disparo
  steadyHand: crosshair() + path('M3 21h18', 'dash'),
  lightTrigger: path('M6 20c0-5 2-8 6-8M9 12a5 5 0 0 1 7 1v4') + circle(15.5, 18, 1.5) + spark(18, 6),
  sidearm: pistol + path('M18.5 9.5h3', 'dash'),
  aimedShot: head(10, 12) + circle(15, 9, 3.2) + path('M15 4.3v2M15 11.7v2M10.3 9h2M17.7 9h2'),
  closeIn: crosshair(12, 12, 4) + path('M2 12h3M22 12h-3M4 9l-2 3 2 3M20 9l2 3-2 3'),
  fineTrigger: at(0, 3, 1, pistol) + spark(19, 4),
  rapidFire: bullet(6, 6, 13) + bullet(12, 4, 15) + bullet(18, 6, 13),
  flanker: figure(15, 5, 1.05) + path('M2 12h7M6 9l3 3-3 3'),
  rooftop: roof(11) + crosshair(12, 17, 2.6),
  coldBlood: snow(12, 12, 8) + circle(12, 12, 2),
  chainShot: chain(9, 15) + bullet(18, 2, 10),
  hollowPoint: bullet(10, 6, 14) + path('M9 8.5c.5-.5 1.5-.5 2 0') + spark(18, 7, 3.6),
  bigBore: circle(12, 12, 8) + circle(12, 12, 3.6) + circle(12, 12, 1, 'fill'),
  sentryCrit: eye(11, 13, 0.85) + spark(19, 5),
  deathFromAbove: path('M12 2v10M8 8l4 4 4-4') + crosshair(12, 18, 3),
  lightningHands: hand(9, 13, 0.9) + bolt(18, 9, 0.75),
  apRounds: path('M15 3v18', 'dash') + bullet(7, 8, 9).replace('', '') + rot(90, bullet(12, 2, 9)),
  shredder: shield(12, 12, 1.1) + path('M9 6l2 5-2 3 3 4'),
  serenity: crosshair(12, 10, 5) + path('M3 20c2-2 4-2 6 0s4 2 6 0 4-2 6 0'),
  coveringFire: shield(8, 12, 0.85) + path('M15 8h6M16 12h6M15 16h6'),
  noEscape: crosshair(12, 12, 4) + path('M3 3l4 4M21 3l-4 4M3 21l4-4M21 21l-4-4'),
  killZone: eye(6, 12, 0.6) + path('M10 12L21 5M10 12h11M10 12l11 7') + path('M18 5.5a14 14 0 0 1 0 13', 'dash'),
  faceoff: path('M3 12h4M17 12h4') + head(7, 10).replace('', '') + circle(18, 17, 2) + circle(18, 6, 2) + path('M10 12l6-6M10 12l6 5'),
  rupture: circle(12, 12, 8) + circle(12, 12, 4) + path('M12 2l-2 6 3 3-2 5 2 6'),
  suppression: path('M2 8c3-3 5 3 8 0s5 3 8 0M2 16c3-3 5 3 8 0s5 3 8 0') + bullet(21, 9, 6),
  marker: crosshair(10, 14, 5) + path('M16 2v8M16 2.5h5l-1.5 2 1.5 2H16'),
  heavyHand: fist(11, 12, 1) + path('M19 5l2-2M20 9h2M17 3V1'),
  eagleEye: path('M2 12s4-6 10-6 10 6 10 6-4 6-10 6S2 12 2 12z') + circle(12, 12, 3) + path('M6 7l4-3 4 1 4-2'),

  // ---- Cuerpo a cuerpo
  grip: blade + path('M3 12c2 1 3 3 2 5'),
  edge: blade + spark(7, 6, 3.6),
  sidestep: figure(15, 5, 1) + path('M8 6c-4 3-4 9 0 12M8 18l-3 0 1-3', ''),
  elbowRoom: circle(12, 12, 8, 'dash') + path('M12 12L19 5M16 5h3v3'),
  bladeCrit: blade + spark(18, 17, 3.6),
  closeRanks: shield(7, 12, 0.85) + shield(17, 12, 0.85),
  implacable: blade + path('M18 21a6 6 0 0 1-6-6M12 15l-2 2M12 15l2 2'),
  heavyBlow: fist(11, 11, 1) + path('M3 20h4M17 20h4M12 20v2'),
  parry: croucher(11, 6) + path('M16 4l5 5', 'dash'),
  grit: path('M5 14h14M5 14c0-4 3-7 7-7s7 3 7 7v2c0 2-2 4-7 4s-7-2-7-4zM9 14v3M12 14v4M15 14v3'),
  bladeMaster: machete,
  earBlow: path('M14 21c3-1 5-4 5-8a7 7 0 0 0-14 0c0 2 1 3 2 4l1 2c.5 1.5 2 2 3 2') + path('M10 13a2 2 0 0 1 4 0c0 1.5-2 2-2 3') + path('M2 5l3 2M5 2l2 3'),
  butcher: path('M5 5h11v8H5z') + path('M16 7l4 1v3l-4 1M8 13v7') + circle(8, 8, 1, 'fill'),
  bladeWall: path('M4 21V8l2-4 2 4v13M10 21V8l2-4 2 4v13M16 21V8l2-4 2 4v13'),
  backstab: figure(9, 5, 1) + path('M21 9l-6 6M18 6l3 3', ''),
  steelNerves: head(11, 12) + path('M8 9l2 2-2 2 2 2'),
  cleanCut: path('M4 20L20 4', '') + path('M4 20l2-5 4 4z') + spark(18, 15, 3),
  waist: path('M8 3c4 4-4 8 0 12s0 6 0 6M16 3c-4 4 4 8 0 12s0 6 0 6'),
  onGuard: shield(12, 13, 0.95) + eye(12, 13, 0.4),
  hardHead: path('M4 15a8 8 0 0 1 16 0zM2 15h20M12 7v-2') + path('M8 18h8'),
  sweep: path('M4 8a10 10 0 0 0 16 0') + path('M4 8l-1 4M20 8l1 4M12 21v-6M9 18l3 3 3-3'),
  secondWind: heart(10, 12, 1) + path('M16 6c2 0 3-1 4-2M16 10h5M17 14c2 0 3 1 4 2'),
  cleaver: path('M14 3l6 6-8 8-6-6z') + path('M8 13l-5 6 2 2 6-5'),
  slippery: drop(9, 12, 1) + path('M15 6c4 2 4 8 0 12', 'dash') + path('M19 6c2 2 2 8 0 12', 'dash'),
  duelist: path('M4 4l16 16M20 4L4 20') + path('M4 8l4-4M16 4l4 4'),
  rampart: path('M3 21V9h3v3h3V9h3v3h3V9h3v3h3V9h0v12z') + path('M3 15h18'),
  bladeDance: path('M12 21V5l2-3 0 3v16') + path('M6 9c3-3 9-3 12 0M6 15c3 3 9 3 12 0'),
  killerInstinct: path('M2 12s4-6 10-6 10 6 10 6-4 6-10 6S2 12 2 12z') + path('M12 7c-1.5 2-1.5 8 0 10 1.5-2 1.5-8 0-10z', 'fill'),

  // ---- Lanzar
  looseArm: arc(20, 18) + path('M3 19l2-3'),
  sharpStone: path('M5 18l3-11 4 4 5-6 2 13z'),
  curtain: cloud(8, 9, 0.75) + cloud(16, 15, 0.75) + path('M3 20h18'),
  salvo: grenade(7, 14, 0.75) + grenade(16, 14, 0.75) + path('M10 4l2 2 2-2', ''),
  bigCharge: grenade(10, 14, 1.05) + plus(19, 5),
  decoy: stone(8, 15, 0.8) + waves(14, 15),
  goodArm: arm,
  shrapnel: burst(12, 12, 4) + path('M4 4l2 2M20 4l-2 2M4 20l2-2M20 20l-2-2M12 1v3M12 20v3M1 12h3M20 12h3'),
  dazzle: spark(12, 12, 8) + circle(12, 12, 2),
  overhand: path('M4 20V9a5 5 0 0 1 9-3') + arc(21, 14),
  safeBlast: shield(12, 13, 1) + burst(12, 13, 3.5),
  blastWave: burst(12, 12, 4) + path('M5 5a10 10 0 0 0 0 14M19 5a10 10 0 0 1 0 14', 'dash'),
  bandolier: path('M3 4l18 16') + grenade(8, 9, 0.5) + grenade(13, 13.5, 0.5) + grenade(18, 18, 0.5),
  boulder: stone(12, 13, 1.45),
  demolition: path('M5 9h4v12H5zM10 9h4v12h-4zM15 9h4v12h-4z') + path('M7 9V6c0-2 2-3 3-3s2 1 3 2', 'dash') + spark(15, 4, 2.6),
  thrower: path('M2 20Q12-6 22 20', 'dash') + grenade(21, 18, 0.45),
  smokeEyes: cloud(12, 12, 1.2) + eye(12, 12, 0.42),
  doubleCharge: grenade(8, 12, 0.8) + grenade(16, 15, 0.8),
  crater: path('M2 16c4 0 4-4 10-4s6 4 10 4') + path('M6 16c2 2 10 2 12 0') + path('M8 8l1-3M12 7V3M16 8l-1-3'),
  fog: path('M3 8h12M6 12h15M3 16h14M8 20h10'),
  stoning: stone(16, 15, 0.8) + arc(14, 10),
  blaster: path('M8 21V12h8v9zM12 12V5M8 5h8') + path('M16 16l5-3', 'dash'),
  shockwave: burst(6, 12, 3.4) + path('M11 6a8 8 0 0 1 0 12M15 4a11 11 0 0 1 0 16M19 2a14 14 0 0 1 0 20'),
  cartridge: caseBox(12, 14, 16, 10) + grenade(8, 14, 0.38) + grenade(12, 14, 0.38) + grenade(16, 14, 0.38),
  flares: path('M10 21l4-12') + spark(15, 6, 4) + path('M18 2l1 1M21 7h1'),
  lure: stone(7, 17, 0.6) + path('M14 4v8M12 6h4') + waves(15, 15),
  ironArm: arm + path('M8 14h5M7 17h6'),
  sling: path('M5 3v6a4 4 0 0 0 8 0V3') + path('M9 13l7 6') + stone(18, 20, 0.45),

  // ---- Curar
  bandage: path('M4 14L14 4l6 6-10 10z') + path('M9 9l6 6M11 8l1 1M8 11l1 1M13 12l1 1M10 14l1 1'),
  calmVoice: head(9, 12) + path('M17 9c1.5 1 1.5 5 0 6M20 7c3 2 3 8 0 10'),
  revival: heart(12, 12, 1.05) + bolt(12, 11, 0.55),
  bridge: hand(6, 13, 0.7) + hand(18, 13, 0.7) + path('M8 12h8', 'dash'),
  bigKit: kit(12, 13, 1.12),
  firstAid: cross(12, 12, 8, 2.6),
  lightHands: hand(9, 13, 0.85) + circle(18, 6, 3) + path('M18 3v6M15 6h6'),
  suture: path('M4 20c5-1 11-7 15-15') + circle(19.5, 4.5, 1.2) + path('M7 13l3 3M10 10l3 3M13 7l3 3'),
  bedside: bed + cross(18, 6, 3, 1.1),
  toughSkin: path('M4 6c3 2 13 2 16 0v10c-3 2-13 2-16 0z') + path('M8 9v6M12 9.5v6M16 9v6', 'dash'),
  restoration: heart(12, 12, 0.9) + path('M4 8a9 9 0 0 1 15-3M20 16a9 9 0 0 1-15 3M19 2v3h-3M5 22v-3h3'),
  surgeon: path('M4 20L15 9l2 2L6 22z') + path('M15 9l4-6 2 1-4 7'),
  lullaby: path('M15 4a7 7 0 1 0 5 12A8 8 0 0 1 15 4z') + path('M5 6v6M3 9h4', ''),
  spares: kit(8, 10, 0.65) + kit(15, 16, 0.65),
  triage: path('M5 4h11l4 4v12H5z') + cross(12.5, 13, 4, 1.4) + circle(16, 7, 1),
  rounds: path('M6 3h12v18H6z') + path('M9 7h6M9 11h6M9 15h4') + path('M9 3V2h6v1'),
  fieldMedic: tent + cross(12, 4.5, 2.4, 0.9),
  ironBlood: drop(12, 13, 1.15) + path('M9 15h6M9 12h6'),
  cheer: hand(12, 13, 1) + path('M5 4l2 2M19 4l-2 2M12 1v3'),
  courage: heart(12, 13, 1) + chevrons(12, 3, 1),
  kitBag: path('M4 9h16v11H4z') + path('M9 9V6h6v3') + cross(12, 14.5, 3.2, 1.1),
  miracles: hand(12, 14, 0.9) + spark(18, 5, 3) + spark(6, 4, 2),
  presence: figure(12, 6, 0.95) + circle(12, 12, 10, 'dash'),
  vigor: heart(10, 13, 1) + chevrons(19, 6, 2),
  serenityMind: head(11, 12) + path('M8 9c1-1 2 1 3 0s2 1 3 0'),
  warKit: kit(12, 14, 1) + path('M4 6l16 0', 'dash'),
  fieldHospital: tent + plus(18, 6, 2.2) + plus(6, 6, 2.2),
  goodHand: hand(10, 13, 1) + heart(18, 6, 0.5),

  // ---- Investigar
  tinker: path('M4 20l9-9M13 11l2-2 4-1 1 1-1 4-2 2zM6 15l3 3'),
  lookout: eye(12, 14, 0.8) + chevrons(12, 3, 1),
  quiet: path('M12 4v9M9 8v6a3 3 0 0 0 6 0V8') + path('M3 3l18 18'),
  patchUp: wrench(10, 13, 0.85) + at(10, 3, 0.45, rifle),
  deft: path('M6 20v-8M9 20V6M12 20V4M15 20V6M18 20v-6') + spark(20, 4, 2.4),
  crossfire: path('M3 6l18 12M3 18L21 6') + figure(12, 8, 0.6),
  care: path('M5 10h10v10H5z') + path('M15 13l5-4v2l-4 4M9 10V7h3') + drop(19, 17, 0.4),
  sideBySide: pair(12, 5),
  parapet: path('M2 20h20') + path('M3 20c0-3 2-4 4-4s4 1 4 4M9 20c0-3 2-4 4-4s4 1 4 4M15 20c0-3 2-4 4-4s3 1 3 4') + path('M6 16c0-3 2-4 4-4s4 1 4 4M12 16c0-3 2-4 4-4s4 1 4 4'),
  picklock: path('M3 13h11l2-2h3M14 13v3M17 11V8') + circle(5, 13, 0),
  morePatch: wrench(9, 14, 0.85) + plus(18, 6),
  binoculars: circle(7, 15, 4) + circle(17, 15, 4) + path('M9 11V6h6v5M11 14h2'),
  coolHead: head(10, 12) + snow(19, 5, 3),
  shoulder: path('M2 20c0-5 3-8 6-8h1c3 0 5 3 5 8M10 20c0-5 3-8 6-8h1c3 0 5 3 5 8') + circle(8.5, 7, 3) + circle(16.5, 7, 3),
  pointOut: path('M3 14h9V10c0-1 1-2 2-2h7') + path('M7 14v6h6v-6') + path('M21 8l-2-2M21 8l-2 2'),
  engineer: gear(9, 13, 5) + wrench(17, 7, 0.55),
  magnifier: lens(10, 10, 6) + plus(10, 10, 2.4),
  weakSpot: shield(12, 12, 1.05) + crosshair(12, 12, 2.4),
  temperance: path('M12 3v18M7 21h10M4 7h16') + path('M4 7l-2 6h4zM20 7l-2 6h4z'),
  lynx: path('M2 12s4-6 10-6 10 6 10 6-4 6-10 6S2 12 2 12z') + path('M12 8c-1 1.5-1 6.5 0 8 1-1.5 1-6.5 0-8z', 'fill') + path('M5 5l2 2M19 5l-2 2'),
  bodge: circle(12, 12, 7) + circle(12, 12, 3) + path('M19 12h3v4h-6'),
  mastermind: gear(12, 12, 7) + path('M12 9v3l2 1.5'),
  inseparable: circle(9, 12, 5) + circle(15, 12, 5),
  watchman: shield(8, 13, 0.8) + figure(17, 6, 0.8),
  anatomy: path('M12 3v5M8 8h8M9 11h6M9.5 14h5M10 17h4') + circle(12, 3, 0) + path('M12 8v11'),
  watchtower: tower(12, 12) + path('M3 4l3 2M21 4l-3 2'),
  shadow: figure(10, 5, 1) + path('M14 21l6-3M14 17l7-3', 'dash'),
  ingenuity: path('M9 17h6M10 20h4M12 3a6 6 0 0 0-3.5 11V17h7v-3A6 6 0 0 0 12 3z') + path('M12 7v4'),

  // ---- Fitness
  stamina: heart(8, 12, 0.8) + path('M13 12h3l1.5-4 2 8 1.5-4h2'),
  crouch: croucher(12, 7),
  quickReflex: bolt(9, 12, 1) + path('M15 8h6M16 12h6M15 16h6'),
  sprinter: boot + speed(5, 18),
  lungs: lungsPath,
  dodgeFire: figure(9, 5, 1) + path('M15 4l3 3-3 3 3 3-3 3', ''),
  runAndGun: runner(10, 4, 0.95) + bullet(20, 7, 8),
  sturdy: path('M8 3h8M8 21h8M9 3v18M15 3v18') + path('M9 9h6M9 15h6'),
  temper: path('M12 3c3 4 6 7 6 11a6 6 0 0 1-12 0c0-3 2-5 3-7 0 3 2 4 3 4 0-3-1-5 0-8z'),
  fixedPost: path('M12 3v18M8 21h8M9 6h6') + eye(12, 12, 0.35),
  shadowstep: path('M6 18c1-1 1-3 0-4s-2 1-1 3zM11 13c1-1 1-3 0-4s-2 1-1 3z') + path('M16 8c1-1 1-3 0-4s-2 1-1 3zM20 4', 'dash'),
  leathery: path('M5 4h14v16H5z') + path('M5 8h2M5 12h2M5 16h2M17 8h2M17 12h2M17 16h2') + path('M9 9l6 6M15 9l-6 6', 'dash'),
  eel: path('M3 12c3-5 5 5 8 0s5 5 8 0l2-1') + circle(19, 11, 0.8, 'fill'),
  vest: path('M7 3l-3 4v14h6l2-3 2 3h6V7l-3-4-3 3h-4z') + path('M12 9v9'),
  dugIn: path('M2 10h5v8h10v-8h5') + path('M2 21h20') + croucher(12, 6).replace('', ''),
  marathon: path('M3 21c4-6 8-2 10-8s5-6 8-9') + flag(15, 2).replace('', ''),
  ironBody: path('M8 4h8l2 5-2 12H8L6 9z') + path('M8 9h8M9 14h6'),
  poise: figure(12, 3, 1) + path('M3 21h18M7 18l-2 3M17 18l2 3'),
  feline: path('M5 4l3 4h8l3-4v9a7 7 0 0 1-14 0z') + circle(9.5, 12, 1, 'fill') + circle(14.5, 12, 1, 'fill') + path('M12 15v1'),
  ironWill: fist(12, 13, 1) + path('M4 4h16', 'dash'),
  agile: path('M4 20c4-1 6-4 8-8s5-7 9-7') + runner(12, 3, 0.6),
  titan: arm + path('M14 4l2-2M18 6l3-1'),
  longRunner: runner(15, 4, 0.9) + path('M2 20h6M3 16h5', 'dash'),
  rock: path('M2 20l4-9 4 2 3-7 5 6 4 8z'),
  untouchable: figure(12, 4, 1) + path('M3 6l3 3M21 6l-3 3M3 18l3-2M21 18l-3-2'),
  bulwark: shield(12, 12, 1.25),
  lastStand: flag(8, 3) + path('M4 21h12'),
  tireless: path('M6 12a3 3 0 1 1 6 0 3 3 0 1 0 6 0 3 3 0 1 0-6 0 3 3 0 1 1-6 0z'),
};

/** A cell's icon, as the inside of a 24×24 SVG; the core's mark for anything unknown. */
export function boardIconSvg(id: CellId): string {
  return ICONS[id] ?? ICONS.core!;
}

/** The ids that have their own drawing (for the lab's check that none is missing). */
export const DRAWN = Object.keys(ICONS);
