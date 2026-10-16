import { CORE, type CellId } from '@bastion/engine';
import { ATLAS_CELL, ATLAS_COLS, ATLAS_ROWS, BOARD_ICON_INDEX } from './boardIconIndex';

// The hex board's icons (Arturo, 16/10 #28: Synty's interface icons, not drawn
// ones): one Synty INTERFACE sprite per cell, as silhouettes in two atlases
// (tools/ui/synty-board-icons.py), grey for an unlocked cell and dark for a
// selected one, and the core's in the accent. Each cell carries both; its state
// shows one (hexBoard.css). Atlases, not 169 images tinted by a filter: the
// wave repaints the board every frame it moves.

const BASE = '/ui/synty/board';

export type IconTint = 'grey' | 'dark' | 'core';

function place(id: CellId): { col: number; row: number } {
  const i = BOARD_ICON_INDEX[id] ?? 0;
  return { col: i % ATLAS_COLS, row: Math.floor(i / ATLAS_COLS) };
}

/** A cell's icon inside the board's SVG, `size` board units across, centred on 0,0. */
export function boardIconSvg(id: CellId, size: number): string {
  const half = (size / 2).toFixed(1);
  const side = size.toFixed(1);
  if (id === CORE) return `<image class="hx-ico core" href="${BASE}/core.webp" x="-${half}" y="-${half}" width="${side}" height="${side}"/>`;
  const { col, row } = place(id);
  const box = `${col * ATLAS_CELL} ${row * ATLAS_CELL} ${ATLAS_CELL} ${ATLAS_CELL}`;
  return (['grey', 'dark'] as const)
    .map(
      (tint) =>
        `<svg class="hx-ico ${tint}" x="-${half}" y="-${half}" width="${side}" height="${side}" viewBox="${box}"><image href="${BASE}/${tint}.webp" width="${ATLAS_COLS * ATLAS_CELL}" height="${ATLAS_ROWS * ATLAS_CELL}"/></svg>`,
    )
    .join('');
}

/** A cell's icon as an HTML element's background (the card beside the board, the lab's sheet), whatever its size. */
export function boardIconStyle(id: CellId, tint: IconTint): string {
  if (id === CORE || tint === 'core') return `background: url('${BASE}/core.webp') center / contain no-repeat`;
  const { col, row } = place(id);
  const x = (col / (ATLAS_COLS - 1)) * 100;
  const y = (row / (ATLAS_ROWS - 1)) * 100;
  return `background: url('${BASE}/${tint}.webp') ${x.toFixed(3)}% ${y.toFixed(3)}% / ${ATLAS_COLS * 100}% ${ATLAS_ROWS * 100}% no-repeat`;
}
