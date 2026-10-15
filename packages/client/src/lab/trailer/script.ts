// When each shot plays in the trailer (docs/TRAILER.md, «Guion»), in seconds.
// Plain data with no imports: the page reads it, and so does the montage
// (tools/trailer/montage.ts) under Node. Arte puts the ids of the shots
// (shots/<id>.ts); a shot of UI's alone (the title, the Bastión's screens, the
// logo) is a shot too.

export interface Placement {
  shot: string;
  from: number;
  to: number;
}

/** The trailer's length, in seconds. */
export const LENGTH = 75;

/** Provisional ids until the shots exist; the times are the script's. */
export const SCRIPT: Placement[] = [
  // 1–3: the empty street, the horde, Atalaya.
  { shot: 'calle', from: 0, to: 6 },
  { shot: 'horda', from: 6, to: 13 },
  { shot: 'atalaya', from: 13, to: 20 },
  // 4: the music's hit and the title.
  { shot: 'titulo', from: 20, to: 24 },
  // 5: the combat montage, one action per shot, cut on the music's accents (every 2 s, stronger every 4 s). Each of
  // UI's three lines falls on its own: «Cada disparo se oye.» (26–29.6) on the reaction shot and the grenade,
  // «Registrad lo que podáis.» (32–35.6) on the door and the search, «Volved vivos.» (38–41.6) on the window.
  { shot: 'machete', from: 24, to: 26 },
  { shot: 'vigilancia', from: 26, to: 28 },
  { shot: 'granada', from: 28, to: 30 },
  { shot: 'noche', from: 30, to: 32 },
  { shot: 'puerta', from: 32, to: 34 },
  { shot: 'registrar', from: 34, to: 36 },
  { shot: 'chillon', from: 36, to: 38 },
  { shot: 'ventana', from: 38, to: 40 },
  { shot: 'area', from: 40, to: 42 },
  { shot: 'poligono', from: 42, to: 44 },
  // 6: the campaign.
  { shot: 'valle', from: 44, to: 48 },
  { shot: 'pantallas', from: 48, to: 56 },
  // 7: the climax at the evacuation.
  { shot: 'evacuacion', from: 56, to: 66 },
  // 8: the logo.
  { shot: 'logo', from: 66, to: 75 },
];

/** Where a shot sits in the trailer, if it does. */
export function placement(shot: string): Placement | undefined {
  return SCRIPT.find((p) => p.shot === shot);
}
