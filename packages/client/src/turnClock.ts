import type { Slot, TurnClock } from '@bastion/engine';

// The squad's turn clock (Arturo, 22/10): the host keeps each player's time (room.ts checkTurnClock) and says how much
// is left whenever it starts, stops or changes; each screen counts the running one down on its own clock. Shown as a
// bar that drains, never in seconds.

let clocks: [TurnClock, TurnClock] | null = null;
/** When the last word came, on this machine's clock (performance.now). */
let heard = 0;

/** The host's word (the turnClock message); null outside the squad's turn. */
export function setTurnClock(next: [TurnClock, TurnClock] | null): void {
  clocks = next;
  heard = performance.now();
}

/**
 * `slot`'s clock right now: the share of their turn's time still left (0–1), and whether it is running; null when
 * there is none (the enemy's turn, a mission over, a player with no soldiers).
 */
export function turnClockOf(slot: Slot): { left: number; running: boolean } | null {
  const c = clocks?.[slot];
  if (!c || c.total <= 0) return null;
  const ms = c.running ? Math.max(0, c.left - (performance.now() - heard)) : c.left;
  return { left: ms / c.total, running: c.running };
}
