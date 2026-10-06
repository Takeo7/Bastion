import type { Family } from './stylekit';
import { cinematic, comic, current, diorama, holo, ink, pixel, polygon, voxel } from './styles';
import { anime, bloques, cuaderno, psx, tebeo, vhs, voxel99 } from './styles90';
import { ciudadGris, linternas, niebla } from './stylesDoggy';

// Lab menu: the families the user liked first, each with its 90s variants,
// then a surprise, the gritty early-2000s side request, then the parked
// styles of the first round.

export const STYLES = [voxel, voxel99, bloques, ink, tebeo, cuaderno, comic, vhs, anime, psx, ciudadGris, niebla, linternas, current, cinematic, polygon, diorama, pixel, holo];

export const FAMILIES: { id: Family; label: string; open: boolean }[] = [
  { id: 'voxel', label: 'Vóxel', open: true },
  { id: 'tinta', label: 'Tinta', open: true },
  { id: 'comic', label: 'Cómic', open: true },
  { id: 'sorpresa', label: 'Sorpresa', open: true },
  { id: 'doggy', label: 'Retro sucio (tipo Pizza Doggy)', open: true },
  { id: 'aparcado', label: 'Aparcados (primera ronda)', open: false },
];
