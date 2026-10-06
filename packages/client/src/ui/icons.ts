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
const fallback = svg('<path d="M12 3l9 9-9 9-9-9z"/>');

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
  runAndGun: dash,
  rapidFire: burst,
  suppression: waves,
  chainShot: chain,
  rupture: cracked,
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
  smoke: cloud,
  flashbang: star,
  firstAid: medkit,
};

/** An ability's icon, ready to append. */
export function abilityIcon(id: AbilityId): SVGElement {
  const t = document.createElement('template');
  t.innerHTML = ICONS[id] ?? fallback;
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
