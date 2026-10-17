import { QUIT_TEXT, quitGuestWarning, quitHostWarning } from '@bastion/engine';
import { desktop } from '../desktop';

// Leaving the game for good (playtest 17/10: «no hay botón de salir del
// juego»), only in the desktop app (Red's bridge, desktop.ts), and the
// question asked first, in Lore's words (QUIT_TEXT), the same in the battle's
// menu (hud.ts) and the base's (strategy/screen.ts). In a browser there is
// nothing to close: no button.

/** Whether this window can close the game (the desktop app). */
export function canQuit(): boolean {
  return desktop() !== null;
}

/**
 * The question before leaving: its title, that the game is saved (a mission
 * half played, `mission`), and what it costs the partner while connected
 * (`partner`): the host's leaving disconnects them; the guest's, mid-mission,
 * leaves its soldiers standing while the host plays on. Its two answers.
 */
export function quitQuestion(partner: string | null, mission: boolean): { title: string; text: string; warning: string | null; yes: string; no: string } {
  const hosting = desktop()?.hosting;
  return {
    title: QUIT_TEXT.title,
    text: mission ? QUIT_TEXT.savedMission : QUIT_TEXT.saved,
    warning: !partner ? null : hosting === true ? quitHostWarning(partner) : hosting === false && mission ? quitGuestWarning(partner) : null,
    yes: QUIT_TEXT.accept,
    no: QUIT_TEXT.refuse,
  };
}

/** The menus' entry. */
export const QUIT_BUTTON = QUIT_TEXT.button;

/** Closes the game: the app stops its server with it, everything already saved (Red's side). */
export function quitGame(): void {
  desktop()?.quit();
}
