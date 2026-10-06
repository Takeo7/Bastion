// "Transmisiones": the story's short radio conversations (engine/src/transmissions.ts),
// played in a panel docked over the base screens. It never blocks: it types each
// line, moves on by itself, advances with a click and closes with ✕ or Esc.
//
// Each browser remembers which scenes it has played, per campaign, and plays at
// most one per "moment" (back at the base, or after a scan); the rest wait for
// the next moment, and every unlocked scene stays in the archive.
import {
  nextScene,
  sceneLines,
  SCENES,
  SPEAKERS,
  unlockedScenes,
  type CampaignState,
  type Scene,
  type SceneId,
  type SceneLine,
  type SpeakerId,
} from '@bastion/engine';
import { storageKey } from '../net';
import { append, clear, h } from '../ui/dom';
import { portraitSrc } from './portraits';
import './transmissions.css';

/** The speaker's voxel bust (strategy/portraits.ts); their initials on a card if it can't be rendered. */
export function portrait(speaker: SpeakerId): HTMLElement {
  try {
    return h('div.radio-portrait.voxel', { 'data-speaker': speaker, title: SPEAKERS[speaker].name }, h('img', { src: portraitSrc(speaker), alt: SPEAKERS[speaker].name }));
  } catch {
    const initials = speaker === 'coro' ? '∿' : SPEAKERS[speaker].name.replace(/«.*»|,/g, '').split(' ').filter((w) => /^[A-ZÁÉÍÓÚ]/.test(w) && w !== 'Dra.').slice(0, 2).map((w) => w[0]).join('');
    return h('div.radio-portrait', { 'data-speaker': speaker }, h('span', {}, initials));
  }
}

// ------------------------------------------------------------------ sound

/** Radio static and key clicks, synthesised; quiet, and silent when the game is muted. */
class RadioSound {
  private ctx: AudioContext | null = null;
  private noise: AudioBuffer | null = null;

  private ready(): AudioContext | null {
    try {
      if (localStorage.getItem(storageKey('muted')) === '1') return null;
    } catch {
      /* storage unavailable: play */
    }
    if (document.hidden) return null;
    if (!this.ctx) {
      this.ctx = new AudioContext();
      this.noise = this.ctx.createBuffer(1, this.ctx.sampleRate, this.ctx.sampleRate);
      const data = this.noise.getChannelData(0);
      for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
    }
    if (this.ctx.state === 'suspended') void this.ctx.resume();
    return this.ctx;
  }

  private burst(duration: number, type: BiquadFilterType, freq: number, gain: number): void {
    const ctx = this.ready();
    if (!ctx || !this.noise) return;
    const src = ctx.createBufferSource();
    src.buffer = this.noise;
    const filter = ctx.createBiquadFilter();
    filter.type = type;
    filter.frequency.value = freq;
    const g = ctx.createGain();
    const t = ctx.currentTime;
    g.gain.setValueAtTime(gain, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + duration);
    src.connect(filter).connect(g).connect(ctx.destination);
    src.start(t, Math.random() * 0.5, duration);
  }

  /** The squelch of a channel opening, or of another voice coming in. */
  static_(): void {
    this.burst(0.28, 'bandpass', 1800, 0.12);
  }

  key(): void {
    this.burst(0.018, 'highpass', 3200, 0.05);
  }
}

// ------------------------------------------------------------------ panel

/** Plays one scene at a time in a panel docked over the screen. */
export class RadioPanel {
  readonly el = h('div.radio', { role: 'log', 'aria-live': 'polite' });
  private lines: SceneLine[] = [];
  private index = 0;
  private shown = 0;
  private timer = 0;
  private wait = 0;
  private text = '';
  private readonly sound = new RadioSound();
  private readonly textEl = h('p.radio-text');
  private onEnd: (() => void) | null = null;

  constructor() {
    this.el.addEventListener('click', (e) => {
      if ((e.target as HTMLElement).closest('.radio-close')) return;
      this.advance();
    });
    window.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && this.active) {
        e.stopPropagation();
        this.close();
      }
    });
  }

  get active(): boolean {
    return this.lines.length > 0;
  }

  /** `vars` fills the placeholders ({saludo}); `onEnd` runs when it finishes or is closed. */
  play(scene: Scene, lines: SceneLine[], vars: Record<string, string>, onEnd?: () => void): void {
    this.stop();
    this.lines = lines.map((l) => ({ ...l, text: l.text.replace(/\{(\w+)\}/g, (m, k: string) => vars[k] ?? m) }));
    this.index = 0;
    this.onEnd = onEnd ?? null;
    this.el.dataset.scene = scene.id;
    this.el.classList.add('show');
    this.sound.static_();
    this.showLine(scene);
  }

  close(): void {
    this.stop();
    this.lines = [];
    this.el.classList.remove('show');
    const end = this.onEnd;
    this.onEnd = null;
    end?.();
  }

  private stop(): void {
    window.clearInterval(this.timer);
    window.clearTimeout(this.wait);
  }

  private showLine(scene: Scene): void {
    const line = this.lines[this.index]!;
    const speaker = SPEAKERS[line.who];
    this.text = line.text;
    this.shown = 0;
    clear(this.el);
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
        h('div.radio-foot', {}, h('small', {}, `${scene.title} · ${this.index + 1}/${this.lines.length}`), h('span.radio-next', {}, this.index + 1 < this.lines.length ? 'Seguir ▸' : 'Fin ▸')),
      ),
    );
    if (this.index > 0) this.sound.static_();
    this.timer = window.setInterval(() => this.type(scene), 26);
  }

  private type(scene: Scene): void {
    this.shown++;
    this.textEl.textContent = this.text.slice(0, this.shown);
    if (this.shown % 3 === 0 && this.text[this.shown - 1] !== ' ') this.sound.key();
    if (this.shown >= this.text.length) this.typed(scene);
  }

  /** The whole line is on paper: it moves on by itself after a reading pause. */
  private typed(scene: Scene): void {
    window.clearInterval(this.timer);
    this.textEl.textContent = this.text;
    this.shown = this.text.length;
    this.wait = window.setTimeout(() => this.next(scene), 1600 + this.text.length * 38);
  }

  /** A click finishes the line being typed, or goes to the next one. */
  private advance(): void {
    if (!this.active) return;
    const scene = SCENES.find((s) => s.id === this.el.dataset.scene)!;
    if (this.shown < this.text.length) this.typed(scene);
    else this.next(scene);
  }

  private next(scene: Scene): void {
    this.stop();
    if (++this.index >= this.lines.length) this.close();
    else this.showLine(scene);
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

  /** `vars()`: values for the placeholders, such as the commanders' names. */
  constructor(private readonly vars: () => Record<string, string>) {}

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

  /** Per campaign: the first time this browser sees one, what already happened counts as heard (bar the welcome in its first days). */
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
    this.seen = new Set(stored ?? unlockedScenes(c).map((s) => s.id).filter((id) => id !== 'welcome' || c.day > 5));
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
