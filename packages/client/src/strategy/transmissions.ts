// "Transmisiones": the story's short radio conversations (engine/src/transmissions.ts),
// played in a panel docked over the base screens. It never blocks: it types each
// line, moves on by itself, advances with a click and closes with ✕ or Esc.
//
// Each browser remembers which scenes it has played, per campaign, and plays at
// most one per "moment" (back at the base, or after a scan); the rest wait for
// the next moment, and every unlocked scene stays in the archive. RadioTips puts
// the same panel in combat for Morse's first-time warnings.
import {
  nextScene,
  RADIO_TIPS,
  RECAP,
  recapLines,
  sceneLines,
  SCENES,
  SPEAKERS,
  TIP_SCENE,
  unlockedScenes,
  type CampaignState,
  type Scene,
  type SceneId,
  type SceneLine,
  type Slot,
  type SpeakerId,
  type TipId,
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
    if (e.key === 'Escape' && this.active) {
      e.stopPropagation();
      this.close();
    }
  };

  /** `combat`: the warnings' card in battle, placed by `.radio.combat`; Esc is left to the battle's menu. */
  constructor(variant?: 'combat') {
    if (variant) this.el.classList.add(variant);
    this.el.addEventListener('click', (e) => {
      if ((e.target as HTMLElement).closest('.radio-close')) return;
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
    this.sound.static_();
    this.showLine();
  }

  close(): void {
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
    this.close();
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
          h('span.radio-next', {}, this.index + 1 < this.lines.length ? 'Seguir ▸' : many ? 'Fin ▸' : 'Entendido ▸'),
        ),
      ),
    );
    if (this.index > 0) this.sound.static_();
    this.timer = window.setInterval(() => this.type(), 26);
  }

  private type(): void {
    this.shown++;
    this.textEl.textContent = this.text.slice(0, this.shown);
    if (this.shown % 3 === 0 && this.text[this.shown - 1] !== ' ') this.sound.key();
    if (this.shown >= this.text.length) this.typed();
  }

  /** The whole line is on paper: it moves on by itself after a reading pause (longer with a rule to read). */
  private typed(): void {
    window.clearInterval(this.timer);
    this.textEl.textContent = this.text;
    this.shown = this.text.length;
    const rule = this.lines[this.index]?.rule?.length ?? 0;
    this.wait = window.setTimeout(() => this.next(), 1600 + (this.text.length + rule) * 38);
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

// --------------------------------------------------------------- warnings

/**
 * Morse's first-time warnings in combat (RADIO_TIPS), one at a time on a card
 * of its own. Each plays once per browser, whatever the campaign: the rules
 * don't change. The game decides when each one matters and calls `show`.
 */
export class RadioTips {
  private readonly panel = new RadioPanel('combat');
  private readonly key = storageKey('tips');
  private readonly heard: Set<TipId>;
  private readonly queue: { tip?: TipId; line: SceneLine }[] = [];
  private heldUntil = 0;
  private holdTimer = 0;
  private gone = false;

  constructor(container: HTMLElement) {
    container.append(this.panel.el);
    let stored: TipId[] = [];
    try {
      stored = JSON.parse(localStorage.getItem(this.key) ?? '[]') as TipId[];
    } catch {
      /* storage unavailable: every warning plays */
    }
    this.heard = new Set(stored);
  }

  /** Queues the warning unless this browser has heard it; false if it won't play. */
  show(id: TipId): boolean {
    if (this.gone || this.heard.has(id) || this.queue.some((q) => q.tip === id)) return false;
    const tip = RADIO_TIPS[id];
    this.queue.push({ tip: id, line: { who: tip.who, text: tip.line, rule: tip.rule } });
    this.pump();
    return true;
  }

  /** A one-off line from Morse (the story chapters), queued like the warnings and never remembered. */
  say(text: string): void {
    if (this.gone) return;
    this.queue.push({ line: { who: 'morse', text } });
    this.pump();
  }

  /** Nothing plays until `ms` from now (the mission's arrival briefing); the queue waits. */
  holdFor(ms: number): void {
    this.heldUntil = Math.max(this.heldUntil, Date.now() + ms);
    window.clearTimeout(this.holdTimer);
    this.holdTimer = window.setTimeout(() => this.pump(), this.heldUntil - Date.now());
  }

  destroy(): void {
    this.gone = true;
    this.queue.length = 0;
    window.clearTimeout(this.holdTimer);
    this.panel.dispose();
  }

  private pump(): void {
    if (this.gone || this.panel.active || !this.queue.length || Date.now() < this.heldUntil) return;
    const next = this.queue.shift()!;
    if (next.tip) {
      this.heard.add(next.tip);
      try {
        localStorage.setItem(this.key, JSON.stringify([...this.heard]));
      } catch {
        /* private mode: the warnings may play again after a reload */
      }
    }
    this.panel.play(TIP_SCENE, [next.line], {}, () => this.pump());
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
    this.seen = new Set(stored ?? unlockedScenes(c).map((s) => s.id).filter((id) => id !== 'welcome' || underWay));
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
