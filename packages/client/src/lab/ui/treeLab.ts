// A mockup of the «Árbol de habilidades» in the manner of the «Prepping Matrix»
// poster (Arturo, 14/10, through Gameplay), for Arturo to approve before the
// real one is built: a round centre (the soldier), five thick trunks of a
// colour family each (the four axes and the Oficio), each opening into its
// branches as big nodes, and the talents in clusters along each branch, row by
// row. Mixta's materials: a planning board of dark steel with a faint grid.
// Everything it shows is the engine's (classes.ts): the tree, its rows and
// gates, and a soldier it builds (autoBuild) with a promotion half spent.
// arbol.html shows it; arbol.html?shot also writes the captures to
// .dev-snapshots/arbol/ (the UX/UI test client's /__uiux-snap).
import '@fontsource/barlow/latin-500.css';
import '@fontsource/barlow/latin-600.css';
import '@fontsource/barlow/latin-700.css';
import '@fontsource/oswald/latin-500.css';
import '@fontsource/oswald/latin-600.css';
import barlow500 from '@fontsource/barlow/files/barlow-latin-500-normal.woff2?url';
import barlow600 from '@fontsource/barlow/files/barlow-latin-600-normal.woff2?url';
import barlow700 from '@fontsource/barlow/files/barlow-latin-700-normal.woff2?url';
import oswald500 from '@fontsource/oswald/files/oswald-latin-500-normal.woff2?url';
import oswald600 from '@fontsource/oswald/files/oswald-latin-600-normal.woff2?url';
import '../../ui/themes/synty-base.css';
import '../../ui/themes/mixta.css';
import '../../ui/themes/synty-icons.css';
import {
  ATTRS,
  autoBuild,
  BRANCHES,
  CENTER,
  CENTER_NAME,
  dormant,
  KIT_CLASS,
  learnTalent,
  pointsAbove,
  pointsInBranch,
  RANKS,
  rowGate,
  Rng,
  SECOND_AXIS_ROWS,
  soldierStats,
  songRowLevel,
  songRowsOpen,
  talentBlocker,
  talentPointsFree,
  TALENTS,
  talentsOf,
  TEMPLATES,
  type AbilityId,
  type Build,
  type ClassId,
  type PerkId,
  type TalentDef,
} from '@bastion/engine';
import { abilityIcon } from '../../ui/icons';

// ------------------------------------------------------------------ the soldier

/**
 * A soldier the engine builds: an Asalto made Capitán (autoBuild), who then
 * learnt a little of the Francotirador's axis and the first trade of its
 * Oficio, and is now half way through a promotion to Coronel (one level
 * drafted, one point left).
 */
function soldier(): { name: string; saved: Build; b: Build; echoes: number } {
  const echoes = 1;
  const saved = autoBuild('assault', 5, new Rng(1410));
  saved.rank = 6;
  for (const id of ['hawkEye', 'songBlade'] as PerkId[]) if (talentPointsFree(saved) > 0 && !talentBlocker(saved, id, echoes)) learnTalent(saved, id);
  // Whatever the picks above left over goes where autoBuild would put it.
  for (const d of [...talentsOf('assault'), ...CENTER]) if (talentPointsFree(saved) > 0 && !talentBlocker(saved, d.id, echoes)) learnTalent(saved, d.id);
  saved.rank = 7;
  const b = structuredClone(saved);
  const draft = talentsOf('assault').find((d) => d.row >= 2 && !talentBlocker(b, d.id, echoes) && !d.song);
  if (draft) learnTalent(b, draft.id);
  return { name: 'Erik Nilsson', saved, b, echoes };
}

const S = soldier();

// ------------------------------------------------------------------ geometry

interface Pt {
  x: number;
  y: number;
}
const pol = (r: number, deg: number): Pt => ({ x: r * Math.cos((deg * Math.PI) / 180), y: r * Math.sin((deg * Math.PI) / 180) });
const add = (a: Pt, b: Pt, k = 1): Pt => ({ x: a.x + b.x * k, y: a.y + b.y * k });
const f = (n: number) => (Math.round(n * 10) / 10).toString();

type Branch = [ClassId, 0 | 1 | 2 | 3];
interface Trunk {
  key: ClassId | 'song';
  name: string;
  angle: number;
  color: string;
  branches: Branch[];
}

/** Five trunks, a colour family each: the axes keep their class colours (mixta.css), the Oficio its own. */
const TRUNKS: Trunk[] = [
  { key: 'assault', name: TEMPLATES.assault.name, angle: -126, color: '#ec644e', branches: [['assault', 0], ['assault', 1], ['assault', 2]] },
  { key: 'grenadier', name: TEMPLATES.grenadier.name, angle: -54, color: '#b7c64a', branches: [['grenadier', 0], ['grenadier', 1], ['grenadier', 2]] },
  { key: 'sharpshooter', name: TEMPLATES.sharpshooter.name, angle: 18, color: '#5aa9ec', branches: [['sharpshooter', 0], ['sharpshooter', 1], ['sharpshooter', 2]] },
  // The Oficio's four trades, each next to the axis it belongs to.
  { key: 'song', name: 'Oficio', angle: 90, color: '#d9a464', branches: [['sharpshooter', 3], ['grenadier', 3], ['assault', 3], ['specialist', 3]] },
  { key: 'specialist', name: TEMPLATES.specialist.name, angle: 162, color: '#4fcaa3', branches: [['specialist', 0], ['specialist', 1], ['specialist', 2]] },
];

const R = { medal: 84, crown: 124, trunk0: 92, split: 296, branch: 366, big: 33, row0: 448, step: 62, bar: 22, node: 14, rim: 790 };
/** Each row's distance from the centre; the class's two rows sit past a wider gap (the bar). */
const rowR = (k: number) => R.row0 + k * R.step + (k >= SECOND_AXIS_ROWS ? R.bar : 0);
/** Where the bar runs, between the third and fourth rows. */
const BAR_R = (R.row0 + 2 * R.step + R.row0 + 3 * R.step + R.bar) / 2;

const branchAngles = (t: Trunk) => (t.branches.length === 4 ? [-21, -7, 7, 21] : [-22, 0, 22]).map((o) => t.angle + o);

// ------------------------------------------------------------------ states

const NO_POINTS = /No quedan puntos/;

interface NodeState {
  level: number;
  fresh: number;
  open: boolean;
  shut: boolean;
  sleeping: boolean;
  blocked: string | null;
}

function stateOf(d: TalentDef): NodeState {
  const level = S.b.talents[d.id] ?? 0;
  const fresh = level - (S.saved.talents[d.id] ?? 0);
  const blocked = talentBlocker(S.b, d.id, S.echoes);
  return { level, fresh, open: !blocked, shut: !level && !!blocked && !NO_POINTS.test(blocked), sleeping: level > 0 && dormant(S.b, d.id), blocked };
}

/** Where a branch's row stands: its gate, the points above, the Taller, the class bar. */
function stepOf(axis: ClassId, branch: number, row: number) {
  const song = branch === 3;
  const gate = rowGate({ row: row as TalentDef['row'], song });
  const above = pointsAbove(S.b.talents, axis, branch, row);
  const taller = song && row >= songRowsOpen(S.echoes) ? songRowLevel(row) : 0;
  const barred = !song && row >= SECOND_AXIS_ROWS && axis !== S.b.cls;
  return { gate, above, taller, barred, lit: above >= gate && !taller && !barred };
}

// ------------------------------------------------------------------ icons

/** A talent's icon: its ability, or what it improves (the same rule as skillTree.ts). */
function iconOf(d: TalentDef): AbilityId {
  if (d.ability) return d.ability;
  const m: Record<string, number | undefined> = { ...d.mods, ...d.onceMods, ...d.maxMods };
  const has = (...keys: string[]) => keys.some((k) => m[k]);
  const starts = (prefix: string) => Object.keys(m).some((k) => k.startsWith(prefix) && m[k]);
  if (d.hp) return 'revival';
  if (d.charges?.grenade || starts('grenade')) return 'grenade';
  if (has('heal')) return 'medkit';
  if (starts('melee') || has('slashDisorient')) return 'slash';
  if (has('reactionKeep', 'squadsightKeep')) return 'overwatch';
  if (has('suppressAim')) return 'suppression';
  if (has('holoAim')) return 'huntersMark';
  if (has('mobility')) return 'runAndGun';
  if (has('will', 'psiGuard')) return 'velmarOath';
  if (has('defense', 'dodge', 'reactionDefense', 'pairDefense')) return 'hunker';
  if (has('shred', 'pierce')) return 'rupture';
  if (has('damage', 'flankDamage', 'closeDamage', 'songDamage', 'critDamage')) return 'rapidFire';
  if (has('crit', 'closeRangeCrit')) return 'aimedShot';
  if (has('aim', 'closeRangeAim', 'pairAim')) return 'shoot';
  return 'shoot';
}

const BRANCH_ICON: Record<ClassId, [AbilityId, AbilityId, AbilityId, AbilityId]> = {
  assault: ['slash', 'shoot', 'runAndGun', 'aegis'],
  grenadier: ['grenade', 'launch', 'suppression', 'fireball'],
  sharpshooter: ['aimedShot', 'pistol', 'overwatch', 'spectralArrow'],
  specialist: ['medkit', 'hack', 'aid', 'firstAid'],
};

/** The packs' silhouette of each ability (synty-icons.css), as data, or null where it keeps its line icon. */
const silhouettes = new Map<AbilityId, string | null>();
const lineIcons = new Map<AbilityId, string>();

async function dataUrl(url: string): Promise<string> {
  const blob = await (await fetch(url)).blob();
  return await new Promise((resolve) => {
    const r = new FileReader();
    r.onload = () => resolve(r.result as string);
    r.readAsDataURL(blob);
  });
}

async function loadIcons(ids: AbilityId[]): Promise<void> {
  for (const id of new Set(ids)) {
    const probe = document.createElement('span');
    probe.dataset.ability = id;
    probe.append(abilityIcon(id));
    document.body.append(probe);
    const v = getComputedStyle(probe).getPropertyValue('--sicon').trim();
    probe.remove();
    const m = /url\(["']?([^"')]+)["']?\)/.exec(v);
    silhouettes.set(id, m ? await dataUrl(m[1]!) : null);
    lineIcons.set(id, abilityIcon(id).innerHTML);
  }
}

/** The icon of `id` in `color`, `size` across, centred on `p`. */
function icon(id: AbilityId, p: Pt, size: number, color: string): string {
  const x = p.x - size / 2;
  const y = p.y - size / 2;
  if (silhouettes.get(id)) return `<rect x="${f(x)}" y="${f(y)}" width="${f(size)}" height="${f(size)}" fill="${color}" mask="url(#si-${id})"/>`;
  return `<svg x="${f(x)}" y="${f(y)}" width="${f(size)}" height="${f(size)}" viewBox="0 0 24 24" fill="none" stroke="${color}" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">${lineIcons.get(id) ?? ''}</svg>`;
}

function iconDefs(): string {
  return [...silhouettes]
    .filter(([, url]) => url)
    .map(([id, url]) => `<mask id="si-${id}" maskContentUnits="objectBoundingBox"><image href="${url}" width="1" height="1" preserveAspectRatio="xMidYMid meet"/></mask>`)
    .join('');
}

// ------------------------------------------------------------------ text

const measure = document.createElement('canvas').getContext('2d')!;
const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** `text` broken into lines no wider than `max` at `font`. */
function wrap(text: string, font: string, max: number): string[] {
  measure.font = font;
  const lines: string[] = [];
  let line = '';
  for (const word of text.split(' ')) {
    const next = line ? `${line} ${word}` : word;
    if (line && measure.measureText(next).width > max) {
      lines.push(line);
      line = word;
    } else line = next;
  }
  if (line) lines.push(line);
  return lines;
}

function textW(text: string, font: string): number {
  measure.font = font;
  return measure.measureText(text).width;
}

// ------------------------------------------------------------------ colour

function mix(a: string, b: string, k: number): string {
  const pa = [1, 3, 5].map((i) => parseInt(a.slice(i, i + 2), 16));
  const pb = [1, 3, 5].map((i) => parseInt(b.slice(i, i + 2), 16));
  return `#${pa.map((v, i) => Math.round(v + (pb[i]! - v) * k).toString(16).padStart(2, '0')).join('')}`;
}

const INK = '#0f1213';
const BOARD = '#15191b';
const MUTED = '#7d847f';
const ACCENT = '#f2c230';
const GOOD = '#8fd05a';

// ------------------------------------------------------------------ the tree

interface View {
  /** Board pixels per tree unit. */
  scale: number;
  /** Tree point at the board's middle. */
  focus: Pt;
  /** Names and notes on every node (close enough to read them). */
  close: boolean;
  /** The node under the pointer: its card, and what it adds in the figures. */
  hover: PerkId | null;
}

interface Placed {
  d: TalentDef;
  p: Pt;
  /** Its point on the branch's stem (a pair hangs from it by a twig). */
  stem: Pt;
  /** Which way its name goes. */
  side: Pt;
  out: Pt;
  color: string;
  st: NodeState;
}

/** Every talent's place: the centre's round the medal, the rest row by row along their branch. */
function placeNodes(): Placed[] {
  const out: Placed[] = [];
  const crown = [-90, -18, 126, -162];
  CENTER.forEach((d, i) => {
    const u = pol(1, crown[i]!);
    out.push({ d, p: pol(R.crown, crown[i]!), stem: pol(R.crown, crown[i]!), side: u, out: u, color: ACCENT, st: stateOf(d) });
  });
  for (const t of TRUNKS) {
    branchAngles(t).forEach((a, i) => {
      const [axis, branch] = t.branches[i]!;
      const u = pol(1, a);
      const v = pol(1, a + 90);
      for (let row = 0; row < 5; row++) {
        const here = talentsOf(axis).filter((d) => d.branch === branch && d.row === row);
        const c = pol(rowR(row), a);
        // A pair sits either side of the stem, one a little further out: their names then never meet the next branch's.
        here.forEach((d, k) => {
          const pair = here.length > 1;
          const sign = k === 0 ? -1 : 1;
          const p = pair ? add(add(c, v, sign * 21), u, sign * 11) : c;
          const side = pair ? { x: v.x * sign, y: v.y * sign } : v;
          out.push({ d, p, stem: c, side, out: u, color: t.color, st: stateOf(d) });
        });
      }
    });
  }
  return out;
}

const NODES = placeNodes();

/** The view's transform of a tree point into board pixels. */
function at(view: View, p: Pt, box: { w: number; h: number }): Pt {
  return { x: box.w / 2 + (p.x - view.focus.x) * view.scale, y: box.h / 2 + (p.y - view.focus.y) * view.scale };
}

function hexagon(c: Pt, r: number): string {
  return Array.from({ length: 6 }, (_, i) => pol(r, 60 * i - 90))
    .map((q, i) => `${i ? 'L' : 'M'}${f(c.x + q.x)} ${f(c.y + q.y)}`)
    .join(' ')
    .concat(' Z');
}

function triangle(c: Pt, r: number, dir: Pt): string {
  const ang = (Math.atan2(dir.y, dir.x) * 180) / Math.PI;
  return [0, 120, 240]
    .map((o) => pol(r, ang + o))
    .map((q, i) => `${i ? 'L' : 'M'}${f(c.x + q.x)} ${f(c.y + q.y)}`)
    .join(' ')
    .concat(' Z');
}

function rhombus(c: Pt, r: number): string {
  return `M${f(c.x)} ${f(c.y - r)} L${f(c.x + r * 0.8)} ${f(c.y)} L${f(c.x)} ${f(c.y + r)} L${f(c.x - r * 0.8)} ${f(c.y)} Z`;
}

/** A label of up to two lines next to `p`, on the `dir` side, in board pixels. */
function label(p: Pt, dir: Pt, gap: number, lines: string[], size: number, fill: string, weight = 600, family = 'Barlow'): string {
  const lh = size * 1.12;
  const horizontal = Math.abs(dir.x) > 0.42;
  const q = add(p, dir, gap);
  const anchor = horizontal ? (dir.x > 0 ? 'start' : 'end') : 'middle';
  let y0: number;
  if (horizontal) y0 = q.y - ((lines.length - 1) * lh) / 2 + size * 0.36;
  else if (dir.y > 0) y0 = q.y + size * 0.8;
  else y0 = q.y - (lines.length - 1) * lh - size * 0.2;
  return `<text x="${f(q.x)}" y="${f(y0)}" text-anchor="${anchor}" font-family="${family}" font-weight="${weight}" font-size="${size}" fill="${fill}" class="halo">${lines
    .map((l, i) => `<tspan x="${f(q.x)}" dy="${i ? f(lh) : 0}">${esc(l)}</tspan>`)
    .join('')}</text>`;
}

function treeSvg(view: View, box: { w: number; h: number }): string {
  const s = view.scale;
  const P = (p: Pt) => at(view, p, box);
  const parts: string[] = [];
  const centre = P({ x: 0, y: 0 });

  // Rings of the planning board round the centre, and the rim.
  for (const r of [R.branch, rowR(2), R.rim - 20]) parts.push(`<circle cx="${f(centre.x)}" cy="${f(centre.y)}" r="${f(r * s)}" fill="none" stroke="rgba(255,255,255,0.035)" stroke-width="1"/>`);

  // ---- trunks and branches
  for (const t of TRUNKS) {
    const axes = t.key === 'song' ? t.branches.map(([a]) => a) : [t.key];
    const points = t.branches.reduce((sum, [a, br]) => sum + pointsInBranch(S.b.talents, a, br), 0);
    const live = points > 0 || t.key === S.b.cls;
    const trunkColor = live ? t.color : mix(t.color, BOARD, 0.55);
    const a0 = P(pol(R.trunk0, t.angle));
    const a1 = P(pol(R.split, t.angle));
    // The branches' curves first, under the trunk's end.
    branchAngles(t).forEach((a, i) => {
      const [axis, br] = t.branches[i]!;
      const has = pointsInBranch(S.b.talents, axis, br) > 0;
      const end = P(pol(R.branch, a));
      const ctrl = P(pol(R.split + 46, a));
      parts.push(`<path d="M${f(a1.x)} ${f(a1.y)} Q${f(ctrl.x)} ${f(ctrl.y)} ${f(end.x)} ${f(end.y)}" fill="none" stroke="${has ? t.color : mix(t.color, BOARD, 0.6)}" stroke-width="${f(Math.max(3, (has ? 12 : 9) * s))}" stroke-linecap="round"/>`);
      // The branch's stem out through its rows: lit while the rows open, dashed past the class's bar.
      for (let row = 0; row < 5; row++) {
        const st = stepOf(axis, br, row);
        const from = P(pol(row ? rowR(row - 1) : R.branch + R.big, a));
        const to = P(pol(rowR(row), a));
        const lit = st.lit && (row === 0 || stepOf(axis, br, row - 1).lit);
        const color = lit ? t.color : mix(t.color, BOARD, st.barred || st.taller ? 0.78 : 0.62);
        const dash = st.barred || st.taller ? ` stroke-dasharray="${f(5 * s)} ${f(5 * s)}"` : '';
        parts.push(`<line x1="${f(from.x)}" y1="${f(from.y)}" x2="${f(to.x)}" y2="${f(to.y)}" stroke="${color}" stroke-width="${f(Math.max(1.6, (lit ? 5 : 3) * s))}" stroke-linecap="round"${dash}/>`);
      }
    });
    parts.push(`<line x1="${f(a0.x)}" y1="${f(a0.y)}" x2="${f(a1.x)}" y2="${f(a1.y)}" stroke="${trunkColor}" stroke-width="${f(34 * s)}" stroke-linecap="round"/>`);
    // The trunk's name along it, read from the centre out (turned over on the left, never upside down).
    const left = Math.cos((t.angle * Math.PI) / 180) < -0.01;
    const m0 = P(pol(left ? R.split - 10 : R.trunk0 + 18, t.angle));
    const m1 = P(pol(left ? R.trunk0 + 18 : R.split - 10, t.angle));
    const id = `trunk-${t.key}-${view.close ? 'c' : 'o'}`;
    // As big as the trunk allows: its length, and its width.
    const room = Math.hypot(m1.x - m0.x, m1.y - m0.y);
    const word = t.name.toUpperCase();
    const fit = room / ((textW(word, '600 10px Oswald') + word.length * 1.6) / 10);
    const size = Math.min(view.close ? 17 : 14, fit, 30 * s * 0.62);
    parts.push(`<path id="${id}" d="M${f(m0.x)} ${f(m0.y)} L${f(m1.x)} ${f(m1.y)}" fill="none"/>`);
    parts.push(
      `<text font-family="Oswald" font-weight="600" font-size="${f(size)}" letter-spacing="${f(size * 0.16)}" fill="${live ? INK : mix(INK, t.color, 0.35)}" dy="${f(size * 0.36)}"><textPath href="#${id}" startOffset="50%" text-anchor="middle">${esc(word)}</textPath></text>`,
    );
    void axes;
  }

  // ---- the class's bar before rows 4 and 5: a fence across each axis, its note following the curve
  for (const t of TRUNKS.filter((x) => x.key !== 'song')) {
    const own = t.key === S.b.cls;
    const as = branchAngles(t);
    const r = BAR_R;
    const p0 = P(pol(r, as[0]! - 9));
    const p1 = P(pol(r, as[2]! + 9));
    const id = `fence-${t.key}-${view.close ? 'c' : 'o'}`;
    parts.push(
      `<path id="${id}" d="M${f(p0.x)} ${f(p0.y)} A${f(r * s)} ${f(r * s)} 0 0 1 ${f(p1.x)} ${f(p1.y)}" fill="none" stroke="${own ? mix(t.color, '#ffffff', 0.25) : 'rgba(255,255,255,0.22)'}" stroke-width="${f(Math.max(1, 1.6 * s))}" stroke-dasharray="${own ? 'none' : `${f(2 * s)} ${f(4 * s)}`}" opacity="${view.close ? 1 : 0.5}"/>`,
    );
    if (view.close) {
      const text = own ? 'Filas 4 y 5 · de su clase' : `Filas 4 y 5 · solo su clase (desde ${RANKS[5]!.name})`;
      parts.push(`<text font-family="Barlow" font-weight="600" font-size="9.5" letter-spacing="0.6" fill="${own ? mix(t.color, '#ffffff', 0.35) : '#8d948f'}" dy="-5"><textPath href="#${id}" startOffset="32%" text-anchor="middle">${esc(text)}</textPath></text>`);
    }
  }

  // ---- row gates: small triangles on the stems, with the points the row asks for (close only)
  for (const t of view.close ? TRUNKS : []) {
    branchAngles(t).forEach((a, i) => {
      const [axis, br] = t.branches[i]!;
      for (let row = 1; row < 5; row++) {
        const st = stepOf(axis, br, row);
        const c = P(pol(row === SECOND_AXIS_ROWS ? BAR_R + 15 : (rowR(row - 1) + rowR(row)) / 2, a));
        const ok = st.above >= st.gate && !st.taller;
        const dir = pol(1, a);
        const size = Math.max(4.5, 8 * s);
        parts.push(`<path d="${triangle(c, size, dir)}" fill="${ok ? t.color : '#20262a'}" stroke="${ok ? mix(t.color, INK, 0.3) : st.taller ? ACCENT : '#59605c'}" stroke-width="${f(Math.max(0.8, 1.2 * s))}"/>`);
        if (view.close) {
          const text = st.taller ? `Taller ${st.taller}` : `${st.gate}`;
          const v = pol(1, a + 90);
          // On the stem's side away from a single node's name.
          parts.push(label(c, { x: -v.x, y: -v.y }, 10, [text], 9, st.taller ? ACCENT : ok ? mix(t.color, '#ffffff', 0.3) : '#8a918c', 700));
        }
      }
    });
  }

  // ---- the medal: the soldier
  const medal = R.medal * s;
  parts.push(`<circle cx="${f(centre.x)}" cy="${f(centre.y)}" r="${f(medal + 8 * s)}" fill="#0c0e0f" stroke="rgba(255,255,255,0.08)" stroke-width="1"/>`);
  parts.push(`<clipPath id="medal-${view.close ? 'c' : 'o'}"><circle cx="${f(centre.x)}" cy="${f(centre.y)}" r="${f(medal)}"/></clipPath>`);
  parts.push(`<circle cx="${f(centre.x)}" cy="${f(centre.y)}" r="${f(medal)}" fill="#2a2f31"/>`);
  if (portrait) parts.push(`<image href="${portrait}" x="${f(centre.x - medal * 1.25)}" y="${f(centre.y - medal * 1.05)}" width="${f(medal * 2.5)}" height="${f(medal * 2.5)}" clip-path="url(#medal-${view.close ? 'c' : 'o'})" preserveAspectRatio="xMidYMin slice"/>`);
  parts.push(`<circle cx="${f(centre.x)}" cy="${f(centre.y)}" r="${f(medal)}" fill="none" stroke="${TRUNKS[0]!.color}" stroke-width="${f(Math.max(2, 4 * s))}"/>`);
  // Its class on a band across the medal's foot.
  const band = 15 * Math.max(s, 0.75);
  parts.push(`<rect x="${f(centre.x - medal * 0.8)}" y="${f(centre.y + medal * 0.52)}" width="${f(medal * 1.6)}" height="${f(band)}" rx="2" fill="${TRUNKS[0]!.color}"/>`);
  const tag = `${TEMPLATES[S.b.cls!].name} · ${RANKS[S.b.rank]!.name}`.toUpperCase();
  const tagSize = Math.min(band * 0.68, (medal * 1.6 - 10) / ((textW(tag, '600 10px Oswald') + tag.length) / 10));
  parts.push(`<text x="${f(centre.x)}" y="${f(centre.y + medal * 0.52 + band / 2 + tagSize * 0.36)}" text-anchor="middle" font-family="Oswald" font-weight="600" font-size="${f(tagSize)}" letter-spacing="${f(tagSize * 0.1)}" fill="${INK}">${esc(tag)}</text>`);

  // ---- big nodes: the branches
  for (const t of TRUNKS) {
    branchAngles(t).forEach((a, i) => {
      const [axis, br] = t.branches[i]!;
      const def = BRANCHES[axis][br];
      const pts = pointsInBranch(S.b.talents, axis, br);
      const c = P(pol(R.branch, a));
      const r = R.big * s;
      parts.push(`<circle cx="${f(c.x)}" cy="${f(c.y)}" r="${f(r + 3 * s)}" fill="${BOARD}"/>`);
      parts.push(`<circle cx="${f(c.x)}" cy="${f(c.y)}" r="${f(r)}" fill="${pts ? t.color : '#1f2427'}" stroke="${pts ? mix(t.color, '#ffffff', 0.3) : mix(t.color, BOARD, 0.25)}" stroke-width="${f(Math.max(1.2, 2.2 * s))}"/>`);
      if (view.close) {
        parts.push(icon(BRANCH_ICON[axis][br], add(c, { x: 0, y: -9 }), 17, pts ? INK : t.color));
        parts.push(`<text x="${f(c.x)}" y="${f(c.y + 10)}" text-anchor="middle" font-family="Oswald" font-weight="600" font-size="${def.name.length > 9 ? 8.6 : 10}" letter-spacing="0.5" fill="${pts ? INK : mix(t.color, '#ffffff', 0.2)}">${esc(def.name.toUpperCase())}</text>`);
        parts.push(`<text x="${f(c.x)}" y="${f(c.y + 20)}" text-anchor="middle" font-family="Barlow" font-weight="700" font-size="8.5" fill="${pts ? 'rgba(15,18,19,0.75)' : MUTED}">${pts ? `${pts} ${pts === 1 ? 'punto' : 'puntos'}` : 'sin puntos'}</text>`);
        // What the branch is about, on a note that follows the big node's curve, on its inner side.
        const id = `focus-${axis}-${br}`;
        const rr = r + 9;
        const inward = a + 180;
        const q0 = add(c, pol(rr, inward + 62));
        const q1 = add(c, pol(rr, inward - 62));
        parts.push(`<path id="${id}" d="M${f(q0.x)} ${f(q0.y)} A${f(rr)} ${f(rr)} 0 0 0 ${f(q1.x)} ${f(q1.y)}" fill="none"/>`);
        const focus = def.song ? def.focus.replace(/^Oficio: /, '') : def.focus;
        parts.push(`<text font-family="Barlow" font-weight="500" font-style="italic" font-size="8.3" fill="${mix(t.color, '#ffffff', 0.45)}" opacity="0.85"><textPath href="#${id}" startOffset="50%" text-anchor="middle">${esc(focus)}</textPath></text>`);
      } else {
        parts.push(icon(BRANCH_ICON[axis][br], c, Math.max(10, 20 * s), pts ? INK : t.color));
        // Far away, the branch's name sits past the end of its stem, on the rim.
        const rim = P(pol(R.rim, a));
        parts.push(label(rim, pol(1, a), 0, [def.name.toUpperCase()], 10.5, pts ? mix(t.color, '#ffffff', 0.15) : mix(t.color, BOARD, 0.25), 600, 'Oswald'));
      }
    });
  }

  // ---- talents needing another: a dashed link between them
  for (const n of NODES.filter((x) => x.d.needs)) {
    const from = NODES.find((x) => x.d.id === n.d.needs);
    if (!from) continue;
    const p0 = P(from.p);
    const p1 = P(n.p);
    const mid = P(add(add(from.p, n.p, 1), { x: 0, y: 0 }, 0));
    const bulge = add({ x: (p0.x + p1.x) / 2, y: (p0.y + p1.y) / 2 }, n.out, 22 * s);
    void mid;
    parts.push(`<path d="M${f(p0.x)} ${f(p0.y)} Q${f(bulge.x)} ${f(bulge.y)} ${f(p1.x)} ${f(p1.y)}" fill="none" stroke="${n.color}" stroke-width="${f(Math.max(1, 1.5 * s))}" stroke-dasharray="${f(3 * s)} ${f(3 * s)}" opacity="0.8"/>`);
  }

  // ---- the talents, a pair hanging from the stem by twigs
  for (const n of NODES) {
    if (n.stem.x === n.p.x && n.stem.y === n.p.y) continue;
    const a = P(n.stem);
    const b = P(n.p);
    const on = n.st.level > 0 || n.st.open;
    parts.push(`<line x1="${f(a.x)}" y1="${f(a.y)}" x2="${f(b.x)}" y2="${f(b.y)}" stroke="${on ? n.color : mix(n.color, BOARD, 0.6)}" stroke-width="${f(Math.max(1, (on ? 2.4 : 1.6) * s))}" stroke-linecap="round"/>`);
  }
  for (const n of NODES) {
    const c = P(n.p);
    const st = n.st;
    const ability = !!n.d.ability;
    const r = (ability ? R.node + 2.5 : R.node) * s;
    const shape = (rr: number) => (ability ? `<path d="${hexagon(c, rr)}"` : `<circle cx="${f(c.x)}" cy="${f(c.y)}" r="${f(rr)}"`);
    const taken = st.level > 0;
    // Every node keeps its family's colour: lit when learnt, a dark tint of it when not, darker still when shut.
    const fill = taken ? (st.sleeping ? mix(n.color, '#7a807c', 0.55) : n.color) : mix(n.color, BOARD, st.shut ? 0.88 : 0.76);
    const stroke = taken ? mix(n.color, '#ffffff', 0.35) : st.open ? n.color : mix(n.color, BOARD, st.shut ? 0.7 : 0.4);
    const width = taken ? 1.6 : st.open ? 2.4 : 1.2;
    parts.push(`${shape(r + 2.5 * s)} fill="${BOARD}"/>`);
    if (st.open && !taken) parts.push(`${shape(r + 1.5 * s)} fill="none" stroke="${n.color}" stroke-width="${f(5 * s)}" opacity="0.28" filter="url(#glow)"/>`);
    parts.push(`${shape(r)} fill="${fill}" stroke="${stroke}" stroke-width="${f(Math.max(0.8, width * s))}"/>`);
    // A level of this promotion: a ring in the accent, dashed (as the draft is in the sheet).
    if (st.fresh) parts.push(`${shape(r + 4 * s)} fill="none" stroke="${ACCENT}" stroke-width="${f(Math.max(1, 1.6 * s))}" stroke-dasharray="${f(3 * s)} ${f(2 * s)}"/>`);
    if (n.d.coop) parts.push(`${shape(r + (st.fresh ? 7.5 : 4) * s)} fill="none" stroke="${mix(n.color, '#ffffff', 0.5)}" stroke-width="${f(Math.max(0.6, 1 * s))}" opacity="0.7"/>`);
    const iconColor = taken ? INK : st.open ? n.color : mix(n.color, BOARD, st.shut ? 0.62 : 0.3);
    parts.push(icon(iconOf(n.d), c, r * 1.15, iconColor));
    // Levels: pips under the node.
    if (n.d.max > 1) {
      for (let i = 0; i < n.d.max; i++) {
        const q = add(c, { x: (i - (n.d.max - 1) / 2) * 5.2 * s, y: r + 4.2 * s });
        parts.push(`<circle cx="${f(q.x)}" cy="${f(q.y)}" r="${f(Math.max(0.9, 1.7 * s))}" fill="${i < st.level ? (i < st.level - st.fresh ? n.color : ACCENT) : '#3b4245'}" stroke="${BOARD}" stroke-width="${f(0.8 * s)}"/>`);
      }
    }
    // Requirements stuck to the node: an aptitude in a triangle (outward), a class's kit in a rhombus (inward).
    const away = { x: -n.side.x, y: -n.side.y };
    if (n.d.req && view.close) {
      const have = S.b.attrs[n.d.req[0]] >= n.d.req[1];
      const q = add(add(c, n.out, (r + 7 * s) * 0.82), away, (r + 7 * s) * 0.58);
      parts.push(`<path d="${triangle(q, Math.max(3.5, 6.5 * s), n.out)}" fill="${have ? GOOD : '#20262a'}" stroke="${have ? mix(GOOD, INK, 0.3) : ACCENT}" stroke-width="${f(Math.max(0.7, 1.1 * s))}"/>`);
      if (view.close) parts.push(label(q, away, 8, [`${ATTRS[n.d.req[0]].short} ${n.d.req[1]}`], 8.8, have ? GOOD : ACCENT, 700));
    }
    if (n.d.kit && view.close) {
      const own = S.b.cls === KIT_CLASS[n.d.kit];
      const q = add(add(c, n.out, -(r + 6 * s) * 0.8), away, (r + 6 * s) * 0.6);
      parts.push(`<path d="${rhombus(q, Math.max(3.2, 5.6 * s))}" fill="${own ? mix(n.color, '#ffffff', 0.25) : '#20262a'}" stroke="${own ? INK : '#8a918c'}" stroke-width="${f(Math.max(0.6, 1 * s))}"/>`);
    }
    if (view.close) {
      const nameColor = taken ? '#f4f5f1' : st.open ? mix(n.color, '#ffffff', 0.45) : st.shut ? '#6c7370' : '#a9afab';
      parts.push(label(c, n.side, r + 6, wrap(n.d.name, '600 10.5px Barlow', 64), 10.5, nameColor, 600));
    }
  }

  // ---- the centre's crown, named
  if (view.close) parts.push(label(add(centre, { x: 0, y: -(R.medal + 8) * s }), { x: 0, y: -1 }, 0, [CENTER_NAME.toUpperCase()], 9, MUTED, 700));
  return parts.join('');
}

// ------------------------------------------------------------------ panels

const W = 1440;
const H = 900;
const BOX = { x: 22, y: 96, w: 1396, h: 742 };

function plate(x: number, y: number, w: number, h: number, extra = ''): string {
  return (
    `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="2" fill="url(#steel)" stroke="#060708" stroke-width="1"${extra}/>` +
    `<rect x="${x + 1}" y="${y + 1}" width="${w - 2}" height="${h - 2}" rx="1.5" fill="none" stroke="rgba(255,255,255,0.07)"/>` +
    [
      [x + 6, y + 6],
      [x + w - 6, y + 6],
      [x + 6, y + h - 6],
      [x + w - 6, y + h - 6],
    ]
      .map(([cx, cy]) => `<circle cx="${cx}" cy="${cy}" r="1.9" fill="url(#rivet)"/>`)
      .join('')
  );
}

const FIGURES: [string, (st: ReturnType<typeof soldierStats>) => number][] = [
  ['Salud', (st) => st.maxHp],
  ['Puntería', (st) => TEMPLATES[st.template].aim + st.mods.aim],
  ['Defensa', (st) => TEMPLATES[st.template].defense + st.mods.defense],
  ['Voluntad', (st) => st.will],
  ['Movilidad', (st) => TEMPLATES[st.template].mobility + st.mods.mobility],
  ['Crítico', (st) => TEMPLATES[st.template].crit + st.mods.crit],
  ['Blindaje', (st) => st.armor],
];

/** The soldier's figures, top right of the board, with what a level more of the node under the pointer adds. */
function figuresSvg(hover: PerkId | null): string {
  const now = soldierStats(S.b);
  let next: ReturnType<typeof soldierStats> | null = null;
  if (hover && (S.b.talents[hover] ?? 0) < TALENTS[hover].max) {
    const b = structuredClone(S.b);
    learnTalent(b, hover);
    next = soldierStats(b);
  }
  const cw = 62;
  const w = cw * FIGURES.length + 20;
  const x = BOX.x + BOX.w - w - 14;
  const y = BOX.y + 14;
  const out = [plate(x, y, w, 70)];
  out.push(`<text x="${x + 12}" y="${y + 17}" font-family="Barlow" font-weight="700" font-size="9.5" letter-spacing="1.4" fill="${MUTED}">${esc(hover ? 'EL SOLDADO · CON UN NIVEL MÁS DE ESTE NODO' : 'EL SOLDADO')}</text>`);
  FIGURES.forEach(([name, read], i) => {
    const cx = x + 10 + i * cw + cw / 2;
    const v = read(now);
    const d = next ? read(next) - v : 0;
    out.push(`<rect x="${f(cx - cw / 2 + 3)}" y="${y + 24}" width="${cw - 6}" height="38" rx="2" fill="rgba(0,0,0,0.32)" stroke="${d > 0 ? 'rgba(143,208,90,0.55)' : 'rgba(0,0,0,0.5)'}"/>`);
    out.push(`<text x="${f(cx)}" y="${y + 35}" text-anchor="middle" font-family="Barlow" font-weight="700" font-size="8.5" letter-spacing="0.6" fill="${MUTED}">${esc(name.toUpperCase())}</text>`);
    out.push(`<text x="${f(d ? cx - 8 : cx)}" y="${y + 56}" text-anchor="middle" font-family="Oswald" font-weight="500" font-size="18" fill="#f4f5f1">${v}</text>`);
    if (d) out.push(`<text x="${f(cx + 11)}" y="${y + 55}" text-anchor="middle" font-family="Oswald" font-weight="600" font-size="13" fill="${d > 0 ? GOOD : '#f0503c'}">${d > 0 ? '+' : '−'}${Math.abs(d)}</text>`);
  });
  return out.join('');
}

/** The card of the node under the pointer, by it (playtest 14/10 #15). */
function tooltipSvg(view: View, box: { w: number; h: number }): string {
  if (!view.hover) return '';
  const n = NODES.find((x) => x.d.id === view.hover)!;
  const d = n.d;
  const st = n.st;
  const c = at(view, n.p, box);
  const w = 268;
  const desc = wrap(d.description, '500 12px Barlow', w - 28);
  const notes: [string, string][] = [];
  if (d.req) notes.push([`Pide ${ATTRS[d.req[0]].name} ${d.req[1]} (tiene ${S.b.attrs[d.req[0]]}).`, S.b.attrs[d.req[0]] >= d.req[1] ? GOOD : ACCENT]);
  if (d.needs) notes.push([`Antes, ${TALENTS[d.needs].name}.`, S.b.talents[d.needs] ? GOOD : ACCENT]);
  const status = st.level >= d.max ? 'Aprendido al máximo.' : st.open ? `Clic: +1 nivel (${st.level + 1}/${d.max}).` : (st.blocked ?? '');
  const statusLines = wrap(status, '700 11.5px Barlow', w - 28);
  const h = 74 + desc.length * 15 + notes.length * 15 + statusLines.length * 14 + 10;
  const right = c.x + 26 + w < box.w - 10;
  const x = BOX.x + (right ? c.x + 26 : c.x - 26 - w);
  const y = BOX.y + Math.min(Math.max(10, c.y - h / 2), box.h - h - 10);
  const where = d.axis ? `${TEMPLATES[d.axis].name} · ${BRANCHES[d.axis][d.branch].name} · fila ${d.row + 1}` : `${CENTER_NAME} · para todos`;
  const out = [plate(x, y, w, h, ` filter="url(#drop)"`)];
  out.push(`<rect x="${x}" y="${y}" width="3" height="${h}" fill="${n.color}"/>`);
  out.push(`<text x="${x + 16}" y="${y + 20}" font-family="Barlow" font-weight="700" font-size="9.5" letter-spacing="1.3" fill="${MUTED}">${esc(where.toUpperCase())}</text>`);
  out.push(`<text x="${x + 16}" y="${y + 42}" font-family="Oswald" font-weight="500" font-size="19" fill="#f4f5f1">${esc(d.name)}</text>`);
  out.push(`<text x="${x + 16}" y="${y + 58}" font-family="Barlow" font-weight="600" font-size="10.5" fill="${mix(n.color, '#ffffff', 0.35)}">${esc(`${d.ability ? 'Habilidad: un botón más en combate' : 'Pasivo'}${d.song ? ' · Oficio' : ''} · nivel ${st.level}/${d.max}`)}</text>`);
  let yy = y + 78;
  for (const l of desc) {
    out.push(`<text x="${x + 16}" y="${yy}" font-family="Barlow" font-weight="500" font-size="12" fill="#dfe2dc">${esc(l)}</text>`);
    yy += 15;
  }
  for (const [t, col] of notes) {
    out.push(`<text x="${x + 16}" y="${yy}" font-family="Barlow" font-weight="600" font-size="11" fill="${col}">${esc(t)}</text>`);
    yy += 15;
  }
  yy += 4;
  for (const l of statusLines) {
    out.push(`<text x="${x + 16}" y="${yy}" font-family="Barlow" font-weight="700" font-size="11.5" fill="${st.open ? ACCENT : MUTED}">${esc(l)}</text>`);
    yy += 14;
  }
  // A thin lead from the card to its node.
  const nx = BOX.x + c.x;
  const ny = BOX.y + c.y;
  out.push(`<line x1="${f(right ? x : x + w)}" y1="${f(Math.min(Math.max(ny, y + 12), y + h - 12))}" x2="${f(nx + (right ? 18 : -18))}" y2="${f(ny)}" stroke="${n.color}" stroke-width="1.2" opacity="0.8"/>`);
  return out.join('');
}

function legendSvg(): string {
  const y = BOX.y + BOX.h + 30;
  const items: [string, string][] = [];
  const c = '#ec644e';
  const sw = (svg: string, text: string) => items.push([svg, text]);
  sw(`<circle cx="0" cy="0" r="7" fill="${c}" stroke="${mix(c, '#ffffff', 0.35)}" stroke-width="1.4"/>`, 'Aprendido');
  sw(`<circle cx="0" cy="0" r="7" fill="${c}" stroke="${mix(c, '#ffffff', 0.35)}" stroke-width="1.4"/><circle cx="0" cy="0" r="10" fill="none" stroke="${ACCENT}" stroke-width="1.4" stroke-dasharray="3 2"/>`, 'En este ascenso');
  sw(`<circle cx="0" cy="0" r="7" fill="#1d2225" stroke="${c}" stroke-width="2.2"/>`, 'Se puede aprender');
  sw(`<circle cx="0" cy="0" r="7" fill="#171b1d" stroke="#353b3e" stroke-width="1.2"/>`, 'Cerrado');
  sw(`<path d="${hexagon({ x: 0, y: 0 }, 8.5)}" fill="#1d2225" stroke="${c}" stroke-width="1.6"/>`, 'Habilidad (un botón más)');
  sw(`<path d="${triangle({ x: 0, y: 0 }, 6.5, { x: 0, y: -1 })}" fill="#20262a" stroke="${ACCENT}" stroke-width="1.1"/>`, 'Requisito: puntos, aptitud o Taller');
  sw(`<path d="${rhombus({ x: 0, y: 0 }, 6)}" fill="#20262a" stroke="#8a918c" stroke-width="1"/>`, 'Pide el equipo de una clase');
  sw(`<line x1="-10" y1="0" x2="10" y2="0" stroke="${c}" stroke-width="1.5" stroke-dasharray="3 3"/>`, 'Antes, otro talento');
  sw(`<circle cx="0" cy="0" r="7" fill="#1d2225" stroke="${c}" stroke-width="1.2"/><circle cx="0" cy="0" r="10" fill="none" stroke="${mix(c, '#ffffff', 0.5)}" stroke-width="1"/>`, 'Con el compañero');
  let x = BOX.x + 18;
  return items
    .map(([svg, text]) => {
      const g = `<g transform="translate(${f(x + 10)} ${y - 4})">${svg}</g><text x="${f(x + 26)}" y="${y}" font-family="Barlow" font-weight="600" font-size="11.5" fill="#b9beb9">${esc(text)}</text>`;
      x += 26 + textW(text, '600 11.5px Barlow') + 24;
      return g;
    })
    .join('');
}

/** What each trunk is to this soldier, down the board's left: its colour, its name, its points. */
function axesSvg(): string {
  const x = BOX.x + 14;
  const y = BOX.y + 14;
  const w = 226;
  const rowH = 44;
  const out = [plate(x, y, w, 30 + TRUNKS.length * rowH)];
  out.push(`<text x="${x + 14}" y="${y + 20}" font-family="Barlow" font-weight="700" font-size="9.5" letter-spacing="1.4" fill="${MUTED}">EJES · PULSA UNO PARA IR A ÉL</text>`);
  TRUNKS.forEach((t, i) => {
    const yy = y + 30 + i * rowH;
    const points = t.branches.reduce((sum, [a, br]) => sum + pointsInBranch(S.b.talents, a, br), 0);
    const role =
      t.key === 'song'
        ? `Taller de oficios, nivel ${S.echoes}`
        : t.key === S.b.cls
          ? 'Su clase'
          : points
            ? 'Segundo eje'
            : 'Sin empezar';
    const live = points > 0 || t.key === S.b.cls;
    out.push(`<rect x="${x + 8}" y="${yy + 2}" width="${w - 16}" height="${rowH - 6}" rx="2" fill="rgba(0,0,0,0.28)" stroke="rgba(0,0,0,0.5)"/>`);
    out.push(`<rect x="${x + 8}" y="${yy + 2}" width="4" height="${rowH - 6}" fill="${live ? t.color : mix(t.color, BOARD, 0.55)}"/>`);
    out.push(`<text x="${x + 22}" y="${yy + 20}" font-family="Oswald" font-weight="600" font-size="14" letter-spacing="1" fill="${live ? t.color : mix(t.color, '#ffffff', 0.1)}">${esc(t.name.toUpperCase())}</text>`);
    out.push(`<text x="${x + 22}" y="${yy + 34}" font-family="Barlow" font-weight="600" font-size="10.5" fill="${MUTED}">${esc(role)}</text>`);
    out.push(`<text x="${x + w - 18}" y="${yy + 26}" text-anchor="end" font-family="Oswald" font-weight="500" font-size="17" fill="${points ? '#f4f5f1' : '#5d645f'}">${points}</text>`);
    out.push(`<text x="${x + w - 18}" y="${yy + 36}" text-anchor="end" font-family="Barlow" font-weight="600" font-size="8.5" fill="${MUTED}">${points === 1 ? 'PUNTO' : 'PUNTOS'}</text>`);
  });
  return out.join('');
}

function headerSvg(): string {
  const free = talentPointsFree(S.b);
  const out = [plate(0, 0, W, 82)];
  out.push(`<rect x="0" y="80" width="${W}" height="2" fill="rgba(242,194,48,0.35)"/>`);
  out.push(`<text x="26" y="28" font-family="Oswald" font-weight="500" font-size="12" letter-spacing="2.6" fill="${ACCENT}">HOJA DE SERVICIO · ÁRBOL DE HABILIDADES</text>`);
  out.push(`<text x="26" y="62" font-family="Oswald" font-weight="600" font-size="30" letter-spacing="1.2" fill="#ffffff">${esc(S.name.toUpperCase())}</text>`);
  const nameW = textW(S.name.toUpperCase(), '600 30px Oswald') + 30 * 0.04 * S.name.length;
  out.push(`<text x="${f(26 + nameW + 16)}" y="61" font-family="Barlow" font-weight="600" font-size="14" fill="#c9cdc8">${esc(`${RANKS[S.b.rank]!.name} · ${TEMPLATES[S.b.cls!].name} · segundo eje: ${TEMPLATES.sharpshooter.name}`)}</text>`);
  // The points left to spend, lit while there are any.
  const chip = `${free} ${free === 1 ? 'punto' : 'puntos'} de talento por repartir`;
  const cw = textW(chip, '700 13px Barlow') + 46;
  const cx = W - 26 - cw;
  out.push(`<rect x="${f(cx)}" y="26" width="${f(cw)}" height="32" rx="2" fill="#3a3418" stroke="rgba(242,194,48,0.7)"/>`);
  out.push(`<circle cx="${f(cx + 16)}" cy="42" r="4" fill="${ACCENT}" filter="url(#glow)"/>`);
  out.push(`<text x="${f(cx + 30)}" y="46.5" font-family="Barlow" font-weight="700" font-size="13" fill="#ffe9a6">${esc(chip)}</text>`);
  return out.join('');
}

function controlsSvg(view: View): string {
  const w = 236;
  const x = BOX.x + BOX.w - w - 14;
  const y = BOX.y + BOX.h - 50;
  const zoom = `${Math.round((view.scale / FIT) * 100)} %`;
  const out = [plate(x, y, w, 36)];
  const btn = (bx: number, bw: number, text: string, on = false) =>
    `<rect x="${bx}" y="${y + 6}" width="${bw}" height="24" rx="2" fill="${on ? '#4a4325' : '#30363a'}" stroke="${on ? 'rgba(242,194,48,0.7)' : '#0a0b0c'}"/>` +
    `<text x="${bx + bw / 2}" y="${y + 22.5}" text-anchor="middle" font-family="Barlow" font-weight="700" font-size="12" fill="${on ? '#ffe9a6' : '#e6e8e3'}">${esc(text)}</text>`;
  out.push(btn(x + 8, 26, '−'), `<text x="${x + 62}" y="${y + 22.5}" text-anchor="middle" font-family="Oswald" font-weight="500" font-size="13" fill="#e6e8e3">${zoom}</text>`, btn(x + 84, 26, '+'), btn(x + 120, 106, 'Ver todo', !view.close));
  out.push(`<text x="${BOX.x + 16}" y="${BOX.y + BOX.h - 18}" font-family="Barlow" font-weight="600" font-size="10.5" fill="${MUTED}">Rueda: acercar · arrastrar: mover · clic en un nodo: aprender · clic derecho: quitar</text>`);
  return out.join('');
}

// ------------------------------------------------------------------ the page

let portrait: string | null = null;
let FIT = 1;

function defs(): string {
  return `<defs>
    <linearGradient id="steel" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#363c3f"/><stop offset="0.55" stop-color="#2a2e30"/><stop offset="1" stop-color="#202426"/></linearGradient>
    <radialGradient id="rivet" cx="0.42" cy="0.38" r="0.6"><stop offset="0" stop-color="#a7aeb2"/><stop offset="0.5" stop-color="#5b6266"/><stop offset="1" stop-color="#23272a"/></radialGradient>
    <pattern id="grid" width="32" height="32" patternUnits="userSpaceOnUse"><path d="M32 0H0V32" fill="none" stroke="rgba(255,255,255,0.035)" stroke-width="1"/></pattern>
    <pattern id="grid5" width="160" height="160" patternUnits="userSpaceOnUse"><path d="M160 0H0V160" fill="none" stroke="rgba(255,255,255,0.05)" stroke-width="1"/></pattern>
    <radialGradient id="pool" cx="0.5" cy="0.5" r="0.6"><stop offset="0" stop-color="rgba(242,194,48,0.06)"/><stop offset="1" stop-color="rgba(0,0,0,0)"/></radialGradient>
    <radialGradient id="vignette" cx="0.5" cy="0.5" r="0.75"><stop offset="0.6" stop-color="rgba(0,0,0,0)"/><stop offset="1" stop-color="rgba(0,0,0,0.55)"/></radialGradient>
    <filter id="glow" x="-1" y="-1" width="3" height="3"><feGaussianBlur stdDeviation="2.4"/></filter>
    <filter id="drop" x="-0.2" y="-0.2" width="1.4" height="1.4"><feDropShadow dx="0" dy="6" stdDeviation="7" flood-color="#000" flood-opacity="0.55"/></filter>
    <clipPath id="board"><rect x="0" y="0" width="${BOX.w}" height="${BOX.h}"/></clipPath>
    ${iconDefs()}
  </defs>`;
}

let fontFaces = '';

async function loadFonts(): Promise<void> {
  const faces: [string, number, string][] = [
    ['Barlow', 500, barlow500],
    ['Barlow', 600, barlow600],
    ['Barlow', 700, barlow700],
    ['Oswald', 500, oswald500],
    ['Oswald', 600, oswald600],
  ];
  const css: string[] = [];
  for (const [family, weight, url] of faces) css.push(`@font-face{font-family:'${family}';font-weight:${weight};src:url(${await dataUrl(url)}) format('woff2');}`);
  fontFaces = css.join('');
  await Promise.all(faces.map(([family, weight]) => document.fonts.load(`${weight} 12px ${family}`)));
}

function page(view: View): string {
  const box = { w: BOX.w, h: BOX.h };
  const centre = at(view, { x: 0, y: 0 }, box);
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
    <style>${fontFaces} .halo{paint-order:stroke;stroke:${BOARD};stroke-width:3px;stroke-linejoin:round}</style>
    ${defs()}
    <rect width="${W}" height="${H}" fill="#0b0d0e"/>
    ${headerSvg()}
    <g transform="translate(${BOX.x} ${BOX.y})">
      <rect width="${BOX.w}" height="${BOX.h}" fill="${BOARD}"/>
      <g clip-path="url(#board)">
        <rect width="${BOX.w}" height="${BOX.h}" fill="url(#grid)"/>
        <rect width="${BOX.w}" height="${BOX.h}" fill="url(#grid5)"/>
        <circle cx="${f(centre.x)}" cy="${f(centre.y)}" r="${f(700 * view.scale)}" fill="url(#pool)"/>
        ${treeSvg(view, box)}
        <rect width="${BOX.w}" height="${BOX.h}" fill="url(#vignette)"/>
      </g>
      <rect width="${BOX.w}" height="${BOX.h}" fill="none" stroke="#060708" stroke-width="2"/>
      <rect x="1" y="1" width="${BOX.w - 2}" height="${BOX.h - 2}" fill="none" stroke="rgba(0,0,0,0.6)" stroke-width="4"/>
    </g>
    ${axesSvg()}
    ${figuresSvg(view.hover)}
    ${controlsSvg(view)}
    ${tooltipSvg(view, box)}
    ${legendSvg()}
  </svg>`;
}

/** Draws the page's SVG on a canvas at `k`× and sends it to the test client, which writes it to .dev-snapshots. */
async function snap(svg: string, name: string, k = 2): Promise<string> {
  const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }));
  const img = new Image();
  img.src = url;
  await img.decode();
  const canvas = document.createElement('canvas');
  canvas.width = W * k;
  canvas.height = H * k;
  const g = canvas.getContext('2d')!;
  // Fonts inside an SVG image can land a moment after it decodes: draw again after a beat.
  g.drawImage(img, 0, 0, canvas.width, canvas.height);
  await new Promise((r) => setTimeout(r, 400));
  g.drawImage(img, 0, 0, canvas.width, canvas.height);
  const png = await new Promise<Blob>((r) => canvas.toBlob((b) => r(b!), 'image/png'));
  const res = await fetch(`/__uiux-snap?name=${encodeURIComponent(name)}`, { method: 'POST', body: png });
  return res.text();
}

/** The open talent the card shows: the first one a level of which changes a figure. */
function pickHover(filter: (d: TalentDef) => boolean): PerkId | null {
  const now = soldierStats(S.b);
  for (const n of NODES) {
    if (!filter(n.d) || !n.st.open) continue;
    const b = structuredClone(S.b);
    learnTalent(b, n.d.id);
    const next = soldierStats(b);
    if (FIGURES.some(([, read]) => read(next) !== read(now))) return n.d.id;
  }
  return null;
}

async function main(): Promise<void> {
  const ids = new Set<AbilityId>(NODES.map((n) => iconOf(n.d)));
  for (const a of Object.values(BRANCH_ICON)) for (const id of a) ids.add(id);
  await loadIcons([...ids]);
  await loadFonts();
  portrait = await dataUrl('/ui/synty/renders/asalto.webp').catch(() => null);
  // Seen whole: the tree fits the board's height.
  FIT = (BOX.h - 40) / ((R.rim + 40) * 2);
  const views: [string, View][] = [
    ['arbol/1-todo.png', { scale: FIT, focus: { x: -40, y: 6 }, close: false, hover: pickHover((d) => d.axis === 'sharpshooter') }],
    ['arbol/2-de-cerca.png', { scale: 0.92, focus: { x: -318, y: -372 }, close: true, hover: pickHover((d) => d.axis === 'assault' && d.branch === 2) }],
  ];
  const which = new URLSearchParams(location.search).get('v') === '2' ? 1 : 0;
  document.body.innerHTML = page(views[which]![1]);
  if (new URLSearchParams(location.search).has('shot')) {
    const out: string[] = [];
    for (const [name, view] of views) out.push(await snap(page(view), name));
    (window as unknown as { __snap: string[] }).__snap = out;
  }
}

void main();
