import type { AbilityId } from '@bastion/engine';

// Line icons for the ability cards (24×24, stroke = currentColor), drawn to
// read like pen sketches on an index card.

const svg = (body: string) =>
  `<svg class="ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${body}</svg>`;

const crosshair = svg('<circle cx="12" cy="12" r="7"/><path d="M12 2v5M12 17v5M2 12h5M17 12h5"/><circle cx="12" cy="12" r="1.2" fill="currentColor"/>');
const pistol = svg('<path d="M3 8h15l1 3H9l-1 3H5l1-3H3z"/><path d="M9 11l1 6h3l-1-6"/>');
const blade = svg('<path d="M5 19L19 5"/><path d="M15 5h4v4"/><path d="M4 16l4 4"/>');
const bolt = svg('<path d="M13 2L5 13h6l-1 9 8-11h-6z"/>');
const eye = svg('<path d="M2 12s3.6-6 10-6 10 6 10 6-3.6 6-10 6S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>');
const shield = svg('<path d="M12 3l8 3v6c0 4.6-3.4 8-8 9-4.6-1-8-4.4-8-9V6z"/><path d="M8 12h8"/>');
const reload = svg('<path d="M20 11a8 8 0 0 0-14-4.6L4 8"/><path d="M4 3v5h5"/><path d="M4 13a8 8 0 0 0 14 4.6l2-1.6"/><path d="M20 21v-5h-5"/>');
const grenade = svg('<circle cx="11" cy="14" r="6"/><path d="M11 8V6h4l2 2M13 6l-1-3h3"/><path d="M8 13h6"/>');
const arc = svg('<path d="M3 20c2-9 9-14 18-14"/><circle cx="18" cy="16" r="3"/><path d="M3 20h4"/>');
const rocket = svg('<path d="M5 19l3-3M14 4c3 0 6 3 6 6l-7 7-6-6z"/><path d="M8 11l-3 1 2 2M13 16l-1 3-2-2"/>');
const medkit = svg('<rect x="3" y="7" width="18" height="13" rx="2"/><path d="M9 7V4h6v3M12 10v7M8.5 13.5h7"/>');
const shieldPlus = svg('<path d="M12 3l8 3v6c0 4.6-3.4 8-8 9-4.6-1-8-4.4-8-9V6z"/><path d="M12 9v6M9 12h6"/>');
const evac = svg('<path d="M12 3v12M7 8l5-5 5 5"/><path d="M4 15v5h16v-5"/>');
const chip = svg('<rect x="6" y="6" width="12" height="12" rx="1"/><path d="M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4"/>');
const dash = svg('<path d="M4 6l6 6-6 6M12 6l6 6-6 6"/>');
const burst = svg('<path d="M3 8h9M3 12h13M3 16h9"/><circle cx="19" cy="12" r="2"/>');
const waves = svg('<path d="M3 8c3-3 6 3 9 0s6 3 9 0M3 16c3-3 6 3 9 0s6 3 9 0"/>');
const chain = svg('<path d="M10 14a4 4 0 0 1 0-5.6l2-2a4 4 0 0 1 5.6 5.6l-1.4 1.4"/><path d="M14 10a4 4 0 0 1 0 5.6l-2 2a4 4 0 0 1-5.6-5.6l1.4-1.4"/>');
const cracked = svg('<path d="M12 3l8 3v6c0 4.6-3.4 8-8 9-4.6-1-8-4.4-8-9V6z"/><path d="M12 7l-2 4 3 2-2 4"/>');
const aimedEye = svg('<circle cx="12" cy="12" r="8"/><path d="M6 12s2.5-3 6-3 6 3 6 3-2.5 3-6 3-6-3-6-3z"/><circle cx="12" cy="12" r="1.2" fill="currentColor"/>');
const targets = svg('<circle cx="6" cy="7" r="2.5"/><circle cx="18" cy="7" r="2.5"/><circle cx="12" cy="17" r="2.5"/><path d="M8 8l2 7M16 8l-2 7"/>');
const cone = svg('<path d="M3 12l17-7v14z"/><circle cx="3" cy="12" r="1.5" fill="currentColor"/>');
const zapCircle = svg('<circle cx="12" cy="12" r="9"/><path d="M13 6l-4 7h4l-1 5 4-7h-4z"/>');
const heartPlus = svg('<path d="M12 20s-8-4.6-8-10a4.5 4.5 0 0 1 8-2.8A4.5 4.5 0 0 1 20 10c0 5.4-8 10-8 10z"/><path d="M12 10v5M9.5 12.5h5"/>');
const heartWaves = svg('<path d="M12 20s-8-4.6-8-10a4.5 4.5 0 0 1 8-2.8A4.5 4.5 0 0 1 20 10c0 5.4-8 10-8 10z"/><path d="M7 11h3l1-2 2 4 1-2h3"/>');
const claw = svg('<path d="M6 4c-1 6 0 12 4 16M11 3c-1 6 0 12 4 17M16 4c-1 5 0 10 3 14"/>');
const spiral = svg('<path d="M12 12a2 2 0 1 1 2 2 4 4 0 1 1-4-4 6 6 0 1 1 6 6 8 8 0 0 1-8-8"/>');
const skull = svg(
  '<path d="M12 3a7 7 0 0 0-7 7c0 2.5 1.3 4 3 5v3h8v-3c1.7-1 3-2.5 3-5a7 7 0 0 0-7-7z"/><circle cx="9" cy="10" r="1.5"/><circle cx="15" cy="10" r="1.5"/><path d="M10 18v3M14 18v3"/>',
);
const cloud = svg('<path d="M7 18a4 4 0 0 1-.5-8A5.5 5.5 0 0 1 17 8.5a3.5 3.5 0 0 1 1 6.9V18z"/><path d="M9 21h8"/>');
const star = svg('<path d="M12 2v5M12 17v5M2 12h5M17 12h5M5 5l3.5 3.5M15.5 15.5L19 19M19 5l-3.5 3.5M8.5 15.5L5 19"/>');
// Fase 9's talents: a blade swung all round (Hacerse sitio), a wrench on a barrel (Apaños).
const sweepIcon = svg('<path d="M12 12L20 4"/><path d="M17 4h3v3"/><path d="M4.5 9a8 8 0 0 0 10.5 10.5" stroke-dasharray="2.2 2"/><circle cx="12" cy="12" r="1.2" fill="currentColor"/>');
const wrench = svg('<path d="M14.5 5.5a4 4 0 0 0 4.9 4.9L21 12l-9 9-3-3 9-9z"/><path d="M3 9h7M3 13h4"/>');
const fallback = svg('<path d="M12 3l9 9-9 9-9-9z"/>');

// Velmar's songs: the same pen, with the Lengua's marks (sound arcs, light, dashed bodies).
const aegis = svg('<path d="M12 6l6 2.2v4.6c0 3.4-2.6 6-6 6.8-3.4-.8-6-3.4-6-6.8V8.2z"/><path d="M7 3.5a7 7 0 0 1 10 0M4.5 1.8a10.5 10.5 0 0 1 15 0"/>');
const echoStep = svg('<circle cx="6" cy="17" r="3" stroke-dasharray="2 2.2"/><circle cx="18" cy="7" r="3"/><path d="M8.5 14.5c2-4 4.5-6 7-6.5"/><path d="M13 7.5l2.6.5-.9 2.4"/>');
const layOnHands = svg('<path d="M7 21v-6l-2-3a1.4 1.4 0 0 1 2.4-1.4L9 13V6a1.3 1.3 0 0 1 2.6 0v5-6.5a1.3 1.3 0 0 1 2.6 0V11V6a1.3 1.3 0 0 1 2.6 0v8c0 4-2.5 7-5 7z"/><path d="M19 3v4M17 5h4"/>');
const oath = svg('<circle cx="12" cy="12" r="9"/><path d="M12 5v12M9 8h6M12 17l-1.5 2.5h3z"/>');
const songBolt = svg('<path d="M3 21l11-11"/><path d="M14 10l-1.5 4.5L21 3l-11.5 8.5z"/><path d="M6 13l-2-1M9 16l-1 2"/>');
const silenceIcon = svg('<path d="M3 19a9 9 0 0 1 18 0z"/><path d="M9 12l6 5M15 12l-6 5"/>');
const fireballIcon = svg('<circle cx="15" cy="9" r="5"/><path d="M11 13c-3 2-5 4-7 7M10 8.5c-3 1-5 3-6.5 5.5M14.5 14c-1 3-3 5-5.5 6.5"/>');
const storm = svg('<path d="M7 13a4 4 0 0 1-.4-8A5.5 5.5 0 0 1 17 4.5a3.5 3.5 0 0 1 .5 7"/><path d="M10 13l-2 4h3l-2 4M16 12l-2 3.5h3l-1.5 3.5"/>');
const mark = svg('<path d="M12 3l9 9-9 9-9-9z"/><circle cx="12" cy="12" r="3"/><path d="M12 7v2M12 15v2M7 12h2M15 12h2"/>');
const spectral = svg('<path d="M3 12h16"/><path d="M15.5 8l4 4-4 4"/><path d="M10 4v16" stroke-dasharray="2.2 2.2"/>');
const shadowStep = svg('<path d="M3 20h6v-5h6v-5h6"/><circle cx="6" cy="11" r="2.4" stroke-dasharray="2 2"/><circle cx="18" cy="5" r="2.4"/>');
const rain = svg('<path d="M5 3l5 9M11 3l5 9M17 3l5 9"/><path d="M8 12h2.5V9.5M14 12h2.5V9.5M20 12h2.5V9.5"/><path d="M3 20h18"/>');
const counter = svg('<path d="M3 9c2-3 4 3 6 0s4 3 6 0 4 3 6 0"/><path d="M3 15c2 3 4-3 6 0s4-3 6 0 4-3 6 0" stroke-dasharray="2.2 2"/>');
const hymnIcon = svg('<path d="M9 18V6l10-2v12"/><circle cx="7" cy="18" r="2"/><circle cx="17" cy="16" r="2"/><path d="M9 9.5l10-2"/>');
const lastVerseIcon = svg('<path d="M3 19h18"/><path d="M7 19a5 5 0 0 1 10 0"/><path d="M12 4v4M5.6 7.6l2.1 2.1M18.4 7.6l-2.1 2.1"/>');
const magnifier = svg('<circle cx="10" cy="10" r="6"/><path d="M14.5 14.5L20 20"/>');
// Doors (playtest 2026-10-13): one swung open on its frame with light through the gap, one shut with its
// panels and handle. The theme lays its filled silhouettes over these (public/ui/mixta/icons).
const doorOpenIcon = svg('<path d="M3 22h18M5.5 22V2.5h13V22"/><path d="M5.5 2.5l7 2.4V22.6l-7-1.2z"/><path d="M15.5 6.5v14" stroke-dasharray="1.6 2"/><circle cx="10.4" cy="13" r="0.9" fill="currentColor"/>');
const doorShutIcon = svg('<path d="M3 22h18"/><rect x="5.5" y="2.5" width="13" height="19.5" rx="0.5"/><path d="M8.5 5.5h5v6h-5zM8.5 14h5v5h-5z"/><circle cx="16" cy="12.8" r="0.9" fill="currentColor"/>');

const ICONS: Record<AbilityId, string> = {
  shoot: crosshair,
  pistol,
  slash: blade,
  stunLance: bolt,
  overwatch: eye,
  hunker: shield,
  reload,
  grenade,
  launch: arc,
  missiles: rocket,
  medkit,
  aid: shieldPlus,
  evac,
  hack: chip,
  search: magnifier,
  searchRoom: magnifier,
  openDoor: doorOpenIcon,
  closeDoor: doorShutIcon,
  runAndGun: dash,
  rapidFire: burst,
  suppression: waves,
  chainShot: chain,
  rupture: cracked,
  elbowRoom: sweepIcon,
  patchUp: wrench,
  aimedShot: aimedEye,
  lightningHands: bolt,
  faceoff: targets,
  killZone: cone,
  combatProtocol: zapCircle,
  revival: heartPlus,
  restoration: heartWaves,
  discharge: star,
  claw,
  mindspin: spiral,
  reanimate: skull,
  burst: star,
  smoke: cloud,
  flashbang: star,
  firstAid: medkit,
  aegis,
  echoStep,
  layOnHands,
  velmarOath: oath,
  bolt: songBolt,
  silence: silenceIcon,
  fireball: fireballIcon,
  velmarStorm: storm,
  huntersMark: mark,
  spectralArrow: spectral,
  shadowStep,
  lightRain: rain,
  counterSong: counter,
  hymn: hymnIcon,
  lastVerse: lastVerseIcon,
};

/** An ability's icon, ready to append. */
/** An ability's line icon; the bar's own actions (hud.ts BarActionId) borrow one that fits. */
export function abilityIcon(id: AbilityId | 'evac' | 'evacAccept' | 'evacRefuse'): SVGElement {
  const t = document.createElement('template');
  t.innerHTML = (id === 'evac' || id === 'evacAccept' ? ICONS.hunker : id === 'evacRefuse' ? ICONS.overwatch : ICONS[id]) ?? fallback;
  return t.content.firstElementChild as SVGElement;
}

// A chevron drawn by hand: two pen passes that do not quite overlap, the
// second lighter, with a small overshoot where the pen turned.
const arrowLeft =
  '<svg class="ico arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
  '<path d="M15.6 4.4C13.1 7.2 10.4 9.9 7.6 12.1c2.9 2.2 5.6 4.9 8.3 7.7" stroke-width="2.6"/>' +
  '<path d="M16.2 5.3C13.6 7.9 11 10.3 8.4 12.4c2.6 1.8 5 4.2 7.3 6.7" stroke-width="1.3" opacity="0.55"/>' +
  '<path d="M7.6 12.1l-1 .3" stroke-width="1.6" opacity="0.7"/>' +
  '</svg>';

/** A hand-drawn ‹ or › for buttons (24×24, stroke = currentColor). */
export function arrowIcon(direction: 'left' | 'right'): SVGElement {
  const t = document.createElement('template');
  t.innerHTML = arrowLeft;
  const el = t.content.firstElementChild as SVGElement;
  if (direction === 'right') el.style.transform = 'scaleX(-1)';
  return el;
}
