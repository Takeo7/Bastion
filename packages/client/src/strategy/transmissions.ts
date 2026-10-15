// "Transmisiones": the story's short radio conversations (engine/src/transmissions.ts),
// played in a panel docked over the base screens. It never blocks: it types each
// line, moves on by itself, advances with a click and closes with ✕ or Esc.
//
// Each browser remembers which scenes it has played, per campaign, and plays at
// most one per "moment" (back at the base, or after a scan); the rest wait for
// the next moment, and every unlocked scene stays in the archive. No radio plays
// in the middle of a mission (playtest 15/10 #8).
import {
  nextScene,
  RECAP,
  recapLines,
  sceneLines,
  SCENES,
  SPEAKERS,
  unlockedScenes,
  type CampaignState,
  type Scene,
  type SceneId,
  type SceneLine,
  type Slot,
  type SpeakerId,
} from '@bastion/engine';
import { sound } from '../audio/bank';
import { audio } from '../audio/engine';
import { morse } from '../audio/radio';
import { storageKey } from '../net';
import { append, clear, h } from '../ui/dom';
import { portraitSrc } from './portraits';
import './transmissions.css';

/** The speaker's bust (strategy/portraits.ts); their initials on a card if it can't be rendered. */
export function portrait(speaker: SpeakerId): HTMLElement {
  try {
    return h('div.radio-portrait.bust', { 'data-speaker': speaker, title: SPEAKERS[speaker].name }, h('img', { src: portraitSrc(speaker), alt: SPEAKERS[speaker].name }));
  } catch {
    const initials = speaker === 'coro' ? '∿' : SPEAKERS[speaker].name.replace(/«.*»|,/g, '').split(' ').filter((w) => /^[A-ZÁÉÍÓÚ]/.test(w) && w !== 'Dra.').slice(0, 2).map((w) => w[0]).join('');
    return h('div.radio-portrait', { 'data-speaker': speaker }, h('span', {}, initials));
  }
}

// ------------------------------------------------------------------ sound

/**
 * Radio sound on the game's radio bus (audio/): it follows the volumes and the
 * mute, and holds the music down while someone talks. Static when the channel
 * opens and when the voice changes, a key click every few letters, a few
 * letters of Morse as the operator comes on the air, distant bells under the
 * Congregación's voice (no one is at a keyboard there), and a click when the channel closes.
 */
class RadioSound {
  private release: (() => void) | null = null;
  /** Morse taps only as she comes on the air: on her first line of each transmission. */
  private tapped = false;

  open(): void {
    sound('radio.static');
    this.tapped = false;
    this.release ??= audio.holdDuck(0.4);
  }

  static_(): void {
    sound('radio.static');
  }

  key(): void {
    sound('radio.key', { spread: 0.15 });
  }

  under(who: string): void {
    if (who === 'coro') sound('radio.bells', { gain: 0.35 });
    else if (who === 'morse' && !this.tapped) {
      this.tapped = true;
      morse();
    }
  }

  close(click: boolean): void {
    if (click) sound('radio.close');
    this.release?.();
    this.release = null;
  }
}

// ------------------------------------------------------------------ panel

/** Plays one scene at a time in a panel docked over the screen. */
export class RadioPanel {
  readonly el = h('div.radio', { role: 'log', 'aria-live': 'polite' });
  private scene: Scene | null = null;
  private lines: SceneLine[] = [];
  private index = 0;
  private shown = 0;
  private timer = 0;
  private wait = 0;
  private text = '';
  private readonly sound = new RadioSound();
  private readonly textEl = h('p.radio-text');
  private onEnd: (() => void) | null = null;
  private readonly onKey = (e: KeyboardEvent) => {
    if (!this.active) return;
    if (e.key === 'Escape') {
      // Taken: the base screen's Esc (back to the Bastión) leaves it alone.
      e.preventDefault();
      e.stopPropagation();
      this.close();
    } else if ((e.key === 'Enter' || e.key === ' ') && !(e.target as HTMLElement | null)?.closest?.('input, textarea, button')) {
      e.preventDefault();
      this.advance();
    }
  };

  /** `combat`: the warnings' card in battle, placed by `.radio.combat`; Esc is left to the battle's menu. */
  constructor(variant?: 'combat') {
    if (variant) this.el.classList.add(variant);
    this.el.addEventListener('click', (e) => {
      if ((e.target as HTMLElement).closest('.radio-close, .radio-next')) return;
      this.advance();
    });
    if (!variant) window.addEventListener('keydown', this.onKey);
  }

  get active(): boolean {
    return this.lines.length > 0;
  }

  /** `vars` fills the placeholders ({saludo}); `onEnd` runs when it finishes or is closed. */
  play(scene: Scene, lines: SceneLine[], vars: Record<string, string>, onEnd?: () => void): void {
    this.stop();
    const fill = (text: string) => text.replace(/\{(\w+)\}/g, (m, k: string) => vars[k] ?? m);
    this.lines = lines.map((l) => ({ ...l, text: fill(l.text) }));
    this.scene = scene;
    this.index = 0;
    this.onEnd = onEnd ?? null;
    this.el.dataset.scene = scene.id;
    this.el.classList.add('show');
    this.sound.open();
    this.showLine();
  }

  /** `click`: the channel closing is heard (not when a mission ending takes the panel away). */
  close(click = true): void {
    this.sound.close(click && this.active);
    this.stop();
    this.lines = [];
    this.scene = null;
    this.el.classList.remove('show');
    const end = this.onEnd;
    this.onEnd = null;
    end?.();
  }

  /** Stops it for good and takes it off the page. */
  dispose(): void {
    this.onEnd = null;
    this.close(false);
    window.removeEventListener('keydown', this.onKey);
    this.el.remove();
  }

  private stop(): void {
    window.clearInterval(this.timer);
    window.clearTimeout(this.wait);
  }

  private showLine(): void {
    const scene = this.scene!;
    const line = this.lines[this.index]!;
    const speaker = SPEAKERS[line.who];
    const many = this.lines.length > 1;
    this.text = line.text;
    this.shown = 0;
    clear(this.el);
    this.el.classList.remove('ready');
    this.textEl.textContent = '';
    this.textEl.classList.toggle('coro', line.who === 'coro');
    append(
      this.el,
      portrait(line.who),
      h(
        'div.radio-body',
        {},
        h(
          'div.radio-head',
          {},
          h('span.radio-channel', {}, `Transmisión · ${scene.channel}`),
          h('button.radio-close', { title: 'Cerrar (Esc)', 'aria-label': 'Cerrar la transmisión', onclick: () => this.close() }, '✕'),
        ),
        h('div.radio-who', {}, h('b', {}, speaker.name), h('small', {}, speaker.role)),
        this.textEl,
        line.rule ? h('p.radio-rule', {}, line.rule) : null,
        h(
          'div.radio-foot',
          {},
          h('small', {}, many ? `${scene.title} · ${this.index + 1}/${this.lines.length}` : scene.title),
          // The button always moves on, even while the line is still being typed; a click elsewhere on the card finishes the line first.
          h('button.radio-next', { onclick: () => this.next() }, this.index + 1 < this.lines.length ? 'Seguir ▸' : many ? 'Fin ▸' : 'Entendido ▸'),
        ),
      ),
    );
    if (this.index > 0) this.sound.static_();
    this.sound.under(line.who);
    this.timer = window.setInterval(() => this.type(), 26);
  }

  private type(): void {
    this.shown++;
    this.textEl.textContent = this.text.slice(0, this.shown);
    if (this.shown % 3 === 0 && this.text[this.shown - 1] !== ' ') this.sound.key();
    if (this.shown >= this.text.length) this.typed();
  }

  /** The whole line is on paper: it waits for the reader (a click, Intro or Espacio), however long they take. */
  private typed(): void {
    window.clearInterval(this.timer);
    this.textEl.textContent = this.text;
    this.shown = this.text.length;
    this.el.classList.add('ready');
  }

  /** A click finishes the line being typed, or goes to the next one. */
  private advance(): void {
    if (!this.active) return;
    if (this.shown < this.text.length) this.typed();
    else this.next();
  }

  private next(): void {
    this.stop();
    if (++this.index >= this.lines.length) this.close();
    else this.showLine();
  }
}

// ------------------------------------------------------------- controller

/** Decides when each scene plays, and remembers the ones already played. */
export class Transmissions {
  readonly panel = new RadioPanel();
  private key = '';
  private seen = new Set<SceneId>();
  private lastDay: number | null = null;
  private moment = false;
  /** This browser opens a campaign already under way: it gets the recap first. */
  private recap = false;

  /** `vars()`: values for the placeholders, such as the commanders' names; `slot`: this player's seat. */
  constructor(
    private readonly vars: () => Record<string, string>,
    private readonly slot: Slot,
  ) {}

  get element(): HTMLElement {
    return this.panel.el;
  }

  /**
   * Call on every campaign update. A new day (a scan, a mission flown) or the
   * first look at a campaign is a moment; `hold` (the debrief) keeps it for later.
   */
  update(c: CampaignState, hold = false): void {
    this.load(c);
    if (this.lastDay !== c.day) {
      this.lastDay = c.day;
      this.moment = true;
    }
    if (hold || this.panel.active) return;
    if (this.recap) {
      this.recap = false;
      this.moment = false;
      this.panel.play(RECAP, recapLines(c, this.slot), this.vars());
      return;
    }
    const scene = nextScene(c, this.seen, this.moment);
    this.moment = false;
    if (scene) this.start(c, scene);
  }

  /** Unlocked scenes for the archive, in story order; `fresh` ones have not played yet. */
  archive(c: CampaignState): { scene: Scene; fresh: boolean }[] {
    this.load(c);
    return unlockedScenes(c).map((scene) => ({ scene, fresh: !this.seen.has(scene.id) }));
  }

  replay(c: CampaignState, id: SceneId, onEnd?: () => void): void {
    const scene = SCENES.find((s) => s.id === id);
    if (scene) this.start(c, scene, onEnd);
  }

  private start(c: CampaignState, scene: Scene, onEnd?: () => void): void {
    this.seen.add(scene.id);
    this.save();
    this.panel.play(scene, sceneLines(scene, c), this.vars(), onEnd);
  }

  /**
   * Per campaign. The first time this browser sees one, what already happened
   * counts as heard; in its first days the welcome still plays, later a recap
   * of where things stand plays instead.
   */
  private load(c: CampaignState): void {
    const key = storageKey(`scenes.${c.uid ?? c.soldiers.slice(0, 2).map((s) => s.name).join('|')}`);
    if (key === this.key) return;
    this.key = key;
    this.lastDay = null;
    let stored: SceneId[] | null = null;
    try {
      stored = JSON.parse(localStorage.getItem(key) ?? 'null') as SceneId[] | null;
    } catch {
      /* storage unavailable: start afresh */
    }
    const underWay = c.day > 5;
    this.seen = new Set(stored ?? unlockedScenes(c).map((s) => s.id));
    this.recap = !stored && underWay;
    if (!stored) this.save();
  }

  private save(): void {
    try {
      localStorage.setItem(this.key, JSON.stringify([...this.seen]));
    } catch {
      /* private mode: the scenes may play again after a reload */
    }
  }
}

/** "Comandantes Ana y Luis" / "Comandante Ana", for {saludo}. */
export function greeting(names: string[]): string {
  const list = names.filter(Boolean);
  if (!list.length) return 'Comandantes';
  if (list.length === 1) return `Comandante ${list[0]}`;
  return `Comandantes ${list.slice(0, -1).join(', ')} y ${list[list.length - 1]}`;
}
