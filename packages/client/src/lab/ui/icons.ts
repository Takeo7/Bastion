// Line icons for the UI mock-ups (24×24, stroke = currentColor, so every
// theme colours them with its own text colour).

const svg = (body: string) =>
  `<svg class="ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${body}</svg>`;

export const ICONS = {
  shoot: svg('<circle cx="12" cy="12" r="7"/><path d="M12 2v5M12 17v5M2 12h5M17 12h5"/><circle cx="12" cy="12" r="1.2" fill="currentColor"/>'),
  overwatch: svg('<path d="M2 12s3.6-6 10-6 10 6 10 6-3.6 6-10 6S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>'),
  grenade: svg('<circle cx="11" cy="14" r="6"/><path d="M11 8V6h4l2 2M13 6l-1-3h3"/><path d="M8 13h6"/>'),
  hunker: svg('<path d="M12 3l8 3v6c0 4.6-3.4 8-8 9-4.6-1-8-4.4-8-9V6z"/><path d="M8 12h8"/>'),
  reload: svg('<path d="M20 11a8 8 0 0 0-14-4.6L4 8"/><path d="M4 3v5h5"/><path d="M4 13a8 8 0 0 0 14 4.6l2-1.6"/><path d="M20 21v-5h-5"/>'),
  pistol: svg('<path d="M3 8h15l1 3H9l-1 3H5l1-3H3z"/><path d="M9 11l1 6h3l-1-6"/>'),
  slash: svg('<path d="M5 19L19 5"/><path d="M15 5h4v4"/><path d="M4 16l4 4"/>'),
  medkit: svg('<rect x="3" y="7" width="18" height="13" rx="2"/><path d="M9 7V4h6v3M12 10v7M8.5 13.5h7"/>'),
  supplies: svg('<rect x="4" y="7" width="16" height="13" rx="1"/><path d="M4 11h16M10 7v4M14 7v4"/>'),
  intel: svg('<path d="M12 3l8 9-8 9-8-9z"/><path d="M12 8l4 4-4 4-4-4z"/>'),
  alloys: svg('<path d="M3 17l4-9h10l4 9z"/><path d="M7 8l2 9M17 8l-2 9"/>'),
  coverFull: svg('<path d="M12 3l8 3v6c0 4.6-3.4 8-8 9-4.6-1-8-4.4-8-9V6z" fill="currentColor" stroke="none"/>'),
  coverHalf: svg('<path d="M12 3l8 3v6c0 4.6-3.4 8-8 9-4.6-1-8-4.4-8-9V6z"/><path d="M12 3v18" /><path d="M4 6l8-3v18c-4.6-1-8-4.4-8-9z" fill="currentColor" stroke="none"/>'),
  alien: svg('<path d="M12 3c4.4 0 7 3 7 7 0 5-4 10-7 11-3-1-7-6-7-11 0-4 2.6-7 7-7z"/><path d="M8 11c1.5 0 3 .8 3 2.2M16 11c-1.5 0-3 .8-3 2.2"/>'),
  scan: svg('<circle cx="12" cy="12" r="9"/><path d="M12 12L18 6"/><circle cx="12" cy="12" r="4"/>'),
};

export type IconId = keyof typeof ICONS;
