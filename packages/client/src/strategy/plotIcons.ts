import type { FacilityId, FixedBuildingId, Terrain } from '@bastion/engine';

// Line icons for the plots of the compound (24×24, stroke = currentColor),
// in the same pen-sketch hand as the ability cards (ui/icons.ts).

const svg = (body: string) =>
  `<svg class="ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${body}</svg>`;

const ICONS: Record<FacilityId | FixedBuildingId | Exclude<Terrain, 'clear'> | 'work', string> = {
  // The bunker and its three buildings.
  command: svg('<path d="M3 20h18M5 20V11l7-5 7 5v9"/><path d="M12 10l1.2 2.4 2.6.4-1.9 1.8.5 2.6-2.4-1.3-2.4 1.3.5-2.6-1.9-1.8 2.6-.4z"/>'),
  barracks: svg('<path d="M4 15a8 8 0 0 1 16 0"/><path d="M3 15h18v2H3z"/><path d="M12 7V4"/>'),
  research: svg('<path d="M4 6h6l2 2h8v11H4z"/><path d="M8 12h8M8 15h5"/>'),
  engineering: svg('<path d="M14.5 5.5a4 4 0 0 0 4.9 4.9l-9 9a2.1 2.1 0 0 1-3-3z"/><path d="M14.5 5.5l2-2 4 4-2 2"/>'),
  // Facilities.
  workshop: svg('<circle cx="12" cy="12" r="3"/><path d="M12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2.1 2.1M16.3 16.3l2.1 2.1M5.6 18.4l2.1-2.1M16.3 7.7l2.1-2.1"/>'),
  infirmary: svg('<rect x="4" y="4" width="16" height="16" rx="2"/><path d="M12 8v8M8 12h8"/>'),
  quarters: svg('<path d="M3 18V8M3 14h18v4M21 18v-6a2 2 0 0 0-2-2h-8v4"/><circle cx="7" cy="11" r="1.6"/>'),
  training: svg('<path d="M6 8v8M18 8v8M3 10v4M21 10v4M6 12h12"/>'),
  range: svg('<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="4"/><circle cx="12" cy="12" r="0.8" fill="currentColor"/>'),
  crew: svg('<path d="M4 15a8 8 0 0 1 16 0z"/><path d="M12 7v3M3 18h18"/>'),
  simulation: svg('<rect x="3" y="4" width="18" height="12" rx="1"/><path d="M8 20h8M12 16v4M7 12l3-3 3 2 4-4"/>'),
  garage: svg('<path d="M3 16V9h11v7M14 11h4l3 3v2h-7"/><circle cx="7" cy="17" r="2"/><circle cx="17" cy="17" r="2"/>'),
  listening: svg('<path d="M12 12v9M8 21h8"/><path d="M8.5 8.5a5 5 0 0 1 7 0M6 6a8.5 8.5 0 0 1 12 0"/><circle cx="12" cy="11" r="1.2" fill="currentColor"/>'),
  echoes: svg('<path d="M9 3v8a3 3 0 0 0 6 0V3"/><path d="M12 14v7"/><path d="M5 8c-1.5 2-1.5 4 0 6M19 8c1.5 2 1.5 4 0 6"/>'),
  memorial: svg('<path d="M12 3v18M7 8h10"/><path d="M4 21h16"/>'),
  // Placeholder until UX/UI draws it (docs/FASE4.md): a sprout over a bed.
  garden: svg('<path d="M3 20h18M5 20v-3h14v3"/><path d="M12 17V9"/><path d="M12 12c-3 0-5-2-5-5 3 0 5 2 5 5zM12 10c0-3 2-5 5-5 0 3-2 5-5 5z"/>'),
  relay: svg('<path d="M12 9l-5 12M12 9l5 12M9 16h6"/><circle cx="12" cy="7" r="2"/><path d="M7 4a7 7 0 0 0 0 6M17 4a7 7 0 0 1 0 6"/>'),
  // Placeholder until UX/UI draws it (playtest 2026-10-15, #13): a door barred from outside.
  quarantine: svg('<rect x="5" y="3" width="14" height="18" rx="1"/><path d="M3 12h18"/><circle cx="15" cy="8" r="0.8" fill="currentColor"/>'),
  // Terrain still to clear.
  rocks: svg('<path d="M3 19l5-9 3 4 3-6 7 11z"/>'),
  forest: svg('<path d="M12 3l-5 8h3l-4 6h12l-4-6h3z"/><path d="M12 17v4"/>'),
  ruins: svg('<path d="M4 20V9l3-2v5l3-1V6l4 2v4l3-1 3 2v8"/><path d="M3 20h18"/>'),
  // A crew at work.
  work: svg('<path d="M14 6l4 4-9 9H5v-4z"/><path d="M13 7l2-2a2.1 2.1 0 0 1 3 3l-2 2"/>'),
};

/** The plot's mark in the facilities grid: its building, its terrain, or the works on it. */
export function plotIcon(kind: keyof typeof ICONS): HTMLElement {
  const wrap = document.createElement('span');
  wrap.className = 'plot-ico';
  // Which mark it is, so an interface theme can draw its own (ui/themes/).
  wrap.dataset.plot = kind;
  wrap.innerHTML = ICONS[kind];
  return wrap;
}
