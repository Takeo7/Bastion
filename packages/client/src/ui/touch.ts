import { TOUCH_TEXT } from '@bastion/engine';
import { autoQuality, graphics, onGraphics, setGraphics } from '../settings';
import { h } from './dom';

// Playing on a phone's browser, held sideways (docs/FASE11.md §1–3): whether
// the touch controls are on, as the attribute data-touch on <html> that the
// interface's CSS reads (and the battle's input asks, isTouch); the notice to
// turn a phone held upright; full screen, locked sideways where the browser
// lets it; and, for the hover's tooltips (title), a long press that shows them.

/** By the screen: a finger and no mouse. A laptop with a touch screen keeps its mouse; the setting can force it. */
function screenIsTouch(): boolean {
  return matchMedia('(pointer: coarse)').matches && matchMedia('(hover: none)').matches;
}

/** Whether the touch controls are on now (the setting «Controles táctiles», or the screen). */
export function isTouch(): boolean {
  const mode = graphics().touch;
  return mode === 'on' || (mode === 'auto' && screenIsTouch());
}

let installed = false;

/** Once, at start: data-touch kept up to date, the turn notice and the long press's tooltips. */
export function installTouch(): void {
  if (installed) return;
  installed = true;
  const apply = () => {
    const on = isTouch();
    document.documentElement.dataset.touch = on ? '1' : '0';
    turnNotice(on);
    mobileEffects(on);
    applyFit();
  };
  onGraphics(apply);
  for (const q of ['(pointer: coarse)', '(hover: none)', '(orientation: portrait)']) matchMedia(q).addEventListener('change', apply);
  window.addEventListener('resize', apply);
  installPeek();
}

// ------------------------------------------------------------------ effects

/** Set when touch mode itself picked Arte's «Móvil» effects, so leaving touch gives them back. */
const MOBILE_FX_KEY = 'bastion.mobileEffects';

/**
 * Touch mode picks the effects «Móvil» (Arte, docs/FASE11.md §5) unless the
 * player chose effects themselves; leaving touch mode puts back the default
 * it replaced, if they are still the ones it put.
 */
function mobileEffects(touch: boolean): void {
  try {
    const ours = localStorage.getItem(MOBILE_FX_KEY) === '1';
    if (touch && !ours && !autoQuality().effectsChosen && graphics().effects !== 'mobile') {
      localStorage.setItem(MOBILE_FX_KEY, '1');
      setGraphics({ effects: 'mobile' }, false);
    } else if (!touch && ours) {
      localStorage.removeItem(MOBILE_FX_KEY);
      if (graphics().effects === 'mobile') setGraphics({ effects: 'high' }, false);
    } else if (touch && ours && graphics().effects !== 'mobile') {
      // Chosen otherwise since: theirs from now on.
      localStorage.removeItem(MOBILE_FX_KEY);
    }
  } catch {
    /* storage unavailable: no automatic effects */
  }
}

// ------------------------------------------------------------------ size

/**
 * How much the interface shrinks on a small screen (Arturo, 17/10: «la UI está
 * muy grande»): 1 on a desktop or a tablet, less as the window gets lower or
 * narrower, smoothly, down to 0.76; on a phone sideways about 0.79 (740 × 360)
 * to 0.86 (an iPhone Air, 912 × 420). The panels zoom by it times the player's
 * «Tamaño de la interfaz» (--ui-zoom, styles.css), and a finger's targets
 * divide by it (--tap), so they still measure 42 px on the glass.
 */
export function uiFit(w = window.innerWidth, h = window.innerHeight): number {
  return Math.min(1, Math.max(0.76, Math.min(0.33 + h / 790, 0.33 + w / 1500)));
}

function applyFit(): void {
  const fit = uiFit();
  const root = document.documentElement.style;
  root.setProperty('--ui-fit', fit.toFixed(3));
  root.setProperty('--ui-zoom', (fit * graphics().uiScale).toFixed(3));
}

// ------------------------------------------------------------------ upright

let notice: HTMLElement | null = null;

/** A phone held upright: a notice over everything, «Gira el móvil», until it is turned. */
function turnNotice(touch: boolean): void {
  const upright = touch && window.innerHeight > window.innerWidth;
  if (!upright) {
    notice?.remove();
    notice = null;
    return;
  }
  if (notice) return;
  notice = h('div.turn-phone', { role: 'alert' }, h('span.turn-phone-icon', { 'aria-hidden': 'true' }), h('b', {}, TOUCH_TEXT.rotate.title), h('p', {}, TOUCH_TEXT.rotate.line));
  document.body.append(notice);
}

// ------------------------------------------------------------------ full screen

/** Whether the browser can go full screen here (not on an iPhone's Safari, which has no API for pages). */
export function canFullscreen(): boolean {
  return !!document.documentElement.requestFullscreen;
}

export function isFullscreen(): boolean {
  return !!document.fullscreenElement;
}

/** Full screen and back; going in, it locks the screen sideways where the browser allows it. */
export async function toggleFullscreen(): Promise<void> {
  try {
    if (document.fullscreenElement) {
      await document.exitFullscreen();
      return;
    }
    await document.documentElement.requestFullscreen({ navigationUI: 'hide' });
    const orientation = screen.orientation as ScreenOrientation & { lock?: (o: string) => Promise<void> };
    await orientation.lock?.('landscape').catch(() => undefined);
  } catch {
    /* refused (not a gesture, or not allowed): it stays as it is */
  }
}

/** The full-screen button's words now. */
export function fullscreenLabel(): string {
  return isFullscreen() ? TOUCH_TEXT.fullscreenOff : TOUCH_TEXT.fullscreen;
}

// ------------------------------------------------------------------ long press

/** How long a finger rests on something for its tooltip (or its card) to show. */
export const LONG_PRESS_MS = 450;
/** How far a finger may wander and still be a tap or a press, not a drag (px). */
export const TAP_SLOP = 10;

let bubble: HTMLElement | null = null;
let bubbleTimer = 0;
/** The click a long press would send on lifting the finger is swallowed. */
let swallowClick = 0;

function hideBubble(): void {
  bubble?.remove();
  bubble = null;
  window.clearTimeout(bubbleTimer);
}

/**
 * A long press on anything with a tooltip (title) shows it in a bubble by the
 * finger, and the tap it would have been does nothing: on a phone there is
 * no hovering to show it. An ability's button shows its card instead
 * (.peek, styles.css). The battle's canvas has its own (game.ts).
 */
function installPeek(): void {
  let press: { id: number; x: number; y: number; timer: number } | null = null;
  document.addEventListener(
    'pointerdown',
    (e) => {
      hideBubble();
      document.querySelectorAll('.peek').forEach((el) => el.classList.remove('peek'));
      if (e.pointerType !== 'touch' || !isTouch()) return;
      const target = e.target as Element | null;
      if (!target || target.closest('canvas, input, textarea')) return;
      const card = target.closest<HTMLElement>('.ability');
      const titled = target.closest<HTMLElement>('[title]');
      if (!card && !titled?.title) return;
      const x = e.clientX;
      const y = e.clientY;
      press = {
        id: e.pointerId,
        x,
        y,
        timer: window.setTimeout(() => {
          press = null;
          swallowClick = performance.now();
          if (card) {
            card.classList.add('peek');
            return;
          }
          bubble = h('div.touch-bubble', { role: 'tooltip' }, titled!.title);
          document.body.append(bubble);
          // Over the finger, kept on the screen.
          const r = bubble.getBoundingClientRect();
          bubble.style.left = `${Math.min(window.innerWidth - r.width - 8, Math.max(8, x - r.width / 2))}px`;
          bubble.style.top = `${Math.max(8, y - r.height - 28)}px`;
          bubbleTimer = window.setTimeout(hideBubble, 5000);
        }, LONG_PRESS_MS),
      };
    },
    { capture: true },
  );
  const lift = (e: PointerEvent) => {
    if (!press || press.id !== e.pointerId) return;
    window.clearTimeout(press.timer);
    press = null;
  };
  document.addEventListener(
    'pointermove',
    (e) => {
      if (press && press.id === e.pointerId && Math.hypot(e.clientX - press.x, e.clientY - press.y) > TAP_SLOP) lift(e);
    },
    { capture: true },
  );
  document.addEventListener('pointerup', lift, { capture: true });
  document.addEventListener('pointercancel', lift, { capture: true });
  // The card shown by a press goes when the finger lifts; the click it would have sent, nowhere.
  document.addEventListener(
    'pointerup',
    () => {
      if (performance.now() - swallowClick < 2000) window.setTimeout(() => document.querySelectorAll('.ability.peek').forEach((el) => el.classList.remove('peek')), 1200);
    },
    { capture: true },
  );
  document.addEventListener(
    'click',
    (e) => {
      if (performance.now() - swallowClick > 1500) return;
      swallowClick = 0;
      e.preventDefault();
      e.stopPropagation();
    },
    { capture: true },
  );
  // Long presses must not open the browser's own menu or select text.
  document.addEventListener('contextmenu', (e) => {
    if (isTouch()) e.preventDefault();
  });
}
