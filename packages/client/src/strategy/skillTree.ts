import './skillTree.css';
import {
  ATTRS,
  AXES,
  BRANCHES,
  carriesKit,
  CENTER,
  CENTER_NAME,
  dormant,
  KIT_NAMES,
  learnTalent,
  pointsAbove,
  pointsInBranch,
  RANKS,
  rowGate,
  SECOND_AXIS_ROWS,
  songRowLevel,
  songRowsOpen,
  soldierStats,
  talentBlocker,
  talentsOf,
  TALENTS,
  TEMPLATES,
  wouldSpecialize,
  type AbilityId,
  type Arms,
  type Build,
  type CampaignSoldier,
  type ClassId,
  type PerkId,
  type TalentDef,
} from '@bastion/engine';
import { append, clear, h } from '../ui/dom';
import { abilityIcon } from '../ui/icons';

// The «Árbol de habilidades» of a soldier's sheet, drawn after the «Prepping
// Matrix» poster (Arturo, 14/10: «más orgánico y poblado»): the soldier in a
// big round medal at the centre, five thick trunks of a colour family each
// (the four axes and the Oficio) that bend as they grow, each splitting into
// its branches (big nodes), and every branch's talents growing from it: the
// first row as a flower round it, the rest as leaves along a curling vine,
// row by row. The rules are the engine's and still read from the drawing: a
// row is lit once its branch has the points (a small triangle on the vine
// says how many), the class's two rows sit past a bar that crosses each axis,
// an aptitude it asks for is a triangle on the node, a class's kit a rhombus.
//
// The tree sits in a window of a fixed size (playtest 14/10 #14) that zooms
// and pans: seen whole it shows its shape and where the soldier stands; close
// enough to read, the names and notes come in. The node under the pointer
// shows its card by it (#15) and what a level adds in the soldier's figures
// (#16). The structure is SVG (trunks, vines, notes along the curves); the
// nodes are buttons over it, so the theme's icons, the keyboard and the card
// work as everywhere else. Every rule is the engine's: talentBlocker, rowGate,
// pointsAbove, dormant, soldierStats; names and texts come from classes.ts.

export interface TreeContext {
  /** The soldier as saved. */
  s: CampaignSoldier;
  /** The soldier with the draft laid on (planned in screen.ts). */
  b: Build;
  /** Level of the Taller de oficios. */
  echoes: number;
  /** Points are being spent: open nodes take a level on click. */
  promoting: boolean;
  /** The axis the window goes to (the sheet's «Ver un eje de cerca»). */
  axis: ClassId;
  /** The whole tree in the window instead of one axis. */
  whole: boolean;
  /** The node last picked, ringed. */
  focus: PerkId | null;
  /** The weapons it carries (campaign.ts soldierArms): a weapon's talents wake with that weapon (docs/FASE8.md §A). */
  arms?: Arms;
  clsTag: (cls: ClassId | null) => HTMLElement;
  onAxis: (axis: ClassId) => void;
  onAdd: (id: PerkId) => void;
  onRemove: (id: PerkId) => void;
  onFocus: (id: PerkId | null) => void;
  onZoom: (axis: ClassId, id: PerkId | null) => void;
}

// ------------------------------------------------------------------ icons

/** Each branch's emblem, from the combat icons (the pack's silhouettes where a theme uses them). */
const BRANCH_ICON: Record<ClassId, [AbilityId, AbilityId, AbilityId, AbilityId]> = {
  assault: ['slash', 'shoot', 'runAndGun', 'aegis'],
  grenadier: ['grenade', 'launch', 'suppression', 'fireball'],
  sharpshooter: ['aimedShot', 'pistol', 'overwatch', 'spectralArrow'],
  specialist: ['medkit', 'hack', 'aid', 'firstAid'],
};

/**
 * A talent's icon: its ability if it grants one, otherwise what it improves
 * (aim, health, defence…), otherwise its branch's emblem. Talents have no
 * art of their own yet (a gap on the list for Arturo).
 */
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
  return d.axis ? BRANCH_ICON[d.axis][d.branch] : 'shoot';
}

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`;

function disc(icon: AbilityId, extra = ''): HTMLElement {
  return h(`span.tn-disc${extra}`, { 'data-ability': icon }, abilityIcon(icon));
}

// ------------------------------------------------------------ the rules

const NO_POINTS = /No quedan puntos/;

/** Where a row of a branch stands: its gate, the points above it, and what else holds it shut. */
function stepOf(b: Build, echoes: number, axis: ClassId, branch: number, row: number) {
  const song = branch === 3;
  const gate = rowGate({ row: row as TalentDef['row'], song });
  const above = pointsAbove(b.talents, axis, branch, row);
  const taller = song && row >= songRowsOpen(echoes) ? songRowLevel(row) : 0;
  const barred = !song && row >= SECOND_AXIS_ROWS && axis !== b.cls;
  return { gate, above, taller, barred, lit: above >= gate && !taller && !barred };
}

/** A node's state, from the engine's own blocker. */
function stateOf(ctx: TreeContext, d: TalentDef) {
  const level = ctx.b.talents[d.id] ?? 0;
  const fresh = level - (ctx.s.talents[d.id] ?? 0);
  const blocked = talentBlocker(ctx.b, d.id, ctx.echoes);
  const open = ctx.promoting && !blocked;
  // Out of points is not a lock: the node keeps its look, the vine says the rest.
  const shut = !level && !!blocked && !NO_POINTS.test(blocked);
  return { level, fresh, blocked, open, shut, sleeping: level > 0 && dormant(ctx.b, d.id) };
}

// ------------------------------------------------------------------ the shape

interface Pt {
  x: number;
  y: number;
}
const pol = (r: number, deg: number): Pt => ({ x: r * Math.cos((deg * Math.PI) / 180), y: r * Math.sin((deg * Math.PI) / 180) });
const add = (a: Pt, b: Pt, k = 1): Pt => ({ x: a.x + b.x * k, y: a.y + b.y * k });
const sub = (a: Pt, b: Pt): Pt => ({ x: a.x - b.x, y: a.y - b.y });
const len = (a: Pt) => Math.hypot(a.x, a.y);
const unit = (a: Pt): Pt => {
  const l = len(a) || 1;
  return { x: a.x / l, y: a.y / l };
};
const f = (n: number) => (Math.round(n * 10) / 10).toString();

/** A steady wobble per name, so the tree grows the same every time: −1…1. */
function wobble(key: string): number {
  let x = 2166136261;
  for (let i = 0; i < key.length; i++) x = Math.imul(x ^ key.charCodeAt(i), 16777619);
  return ((x >>> 0) % 20001) / 10000 - 1;
}

type Cubic = [Pt, Pt, Pt, Pt];

function at(c: Cubic, t: number): Pt {
  const u = 1 - t;
  const [a, b, d, e] = c;
  return {
    x: u * u * u * a.x + 3 * u * u * t * b.x + 3 * u * t * t * d.x + t * t * t * e.x,
    y: u * u * u * a.y + 3 * u * u * t * b.y + 3 * u * t * t * d.y + t * t * t * e.y,
  };
}

function tangent(c: Cubic, t: number): Pt {
  const u = 1 - t;
  const [a, b, d, e] = c;
  return unit({
    x: 3 * u * u * (b.x - a.x) + 6 * u * t * (d.x - b.x) + 3 * t * t * (e.x - d.x),
    y: 3 * u * u * (b.y - a.y) + 6 * u * t * (d.y - b.y) + 3 * t * t * (e.y - d.y),
  });
}

/** The stretch of `c` between t0 and t1, as a cubic of its own (de Casteljau). */
function piece(c: Cubic, t0: number, t1: number): Cubic {
  const split = (k: Cubic, t: number): [Cubic, Cubic] => {
    const [a, b, d, e] = k;
    const l = (p: Pt, q: Pt) => ({ x: p.x + (q.x - p.x) * t, y: p.y + (q.y - p.y) * t });
    const ab = l(a, b);
    const bd = l(b, d);
    const de = l(d, e);
    const abd = l(ab, bd);
    const bde = l(bd, de);
    const m = l(abd, bde);
    return [
      [a, ab, abd, m],
      [m, bde, de, e],
    ];
  };
  const right = split(c, t0)[1];
  return split(right, t0 < 1 ? (t1 - t0) / (1 - t0) : 0)[0];
}

const cubicD = (c: Cubic) => `M${f(c[0].x)} ${f(c[0].y)}C${f(c[1].x)} ${f(c[1].y)} ${f(c[2].x)} ${f(c[2].y)} ${f(c[3].x)} ${f(c[3].y)}`;

/** A limb of the tree as a filled shape that thins from `w0` to `w1` along `c`. */
function taper(c: Cubic, w0: number, w1: number): string {
  const n = 18;
  const left: Pt[] = [];
  const right: Pt[] = [];
  for (let i = 0; i <= n; i++) {
    const t = i / n;
    const p = at(c, t);
    const tn = tangent(c, t);
    const nrm = { x: -tn.y, y: tn.x };
    const w = (w0 + (w1 - w0) * t) / 2;
    left.push(add(p, nrm, w));
    right.push(add(p, nrm, -w));
  }
  const pts = [...left, ...right.reverse()];
  return `M${pts.map((p) => `${f(p.x)} ${f(p.y)}`).join('L')}Z`;
}

/** The tree is laid out round, then drawn wider than tall to fill the window (Arturo: «que llene la ventana»). */
const SX = 1.32;
const SY = 0.84;
const stretch = (p: Pt): Pt => ({ x: p.x * SX, y: p.y * SY });
const stretchC = (c: Cubic): Cubic => c.map(stretch) as Cubic;

type TrunkKey = ClassId | 'song';
type Branch = [ClassId, 0 | 1 | 2 | 3];

interface TrunkDef {
  key: TrunkKey;
  angle: number;
  branches: Branch[];
}

/** Five trunks, a colour family each (the axes keep their class colours; the Oficio its brass), neighbours sharing a side. */
const TRUNKS: TrunkDef[] = [
  { key: 'assault', angle: -126, branches: [['assault', 0], ['assault', 1], ['assault', 2]] },
  { key: 'grenadier', angle: -54, branches: [['grenadier', 0], ['grenadier', 1], ['grenadier', 2]] },
  { key: 'sharpshooter', angle: 18, branches: [['sharpshooter', 0], ['sharpshooter', 1], ['sharpshooter', 2]] },
  // The Oficio's four trades, each by the axis it belongs to.
  { key: 'song', angle: 90, branches: [['sharpshooter', 3], ['grenadier', 3], ['assault', 3], ['specialist', 3]] },
  { key: 'specialist', angle: 162, branches: [['specialist', 0], ['specialist', 1], ['specialist', 2]] },
];

const trunkName = (k: TrunkKey) => (k === 'song' ? 'Oficio' : TEMPLATES[k].name);
const trunkClass = (k: TrunkKey) => (k === 'song' ? 'tk-song' : `axis-${k}`);

/** Where along its vine each row grows (0: round the branch's node), and where the class's bar crosses. */
const ROW_T = [0, 0.27, 0.5, 0.77, 0.98];
const BAR_T = 0.635;
const MEDAL = 84;

interface NodeGeo {
  d: TalentDef;
  p: Pt;
  /** Where its twig leaves the vine (or the branch's node). */
  from: Pt;
  /** Which way its name would rather go (away from its vine). */
  side: Pt;
  r: number;
  trunk: TrunkKey;
  /** Where its name went: beside it (e, w) or above or below it (n, s), clear of the others. */
  label: 'e' | 'w' | 'n' | 's';
}

interface BranchGeo {
  axis: ClassId;
  branch: 0 | 1 | 2 | 3;
  trunk: TrunkKey;
  node: Pt;
  limb: Cubic;
  vine: Cubic;
  /** The vine's end, where far away the branch's name sits. */
  tip: Pt;
  out: Pt;
}

interface TrunkGeo {
  def: TrunkDef;
  body: Cubic;
}

interface Layout {
  trunks: TrunkGeo[];
  branches: BranchGeo[];
  nodes: NodeGeo[];
  box: { x0: number; y0: number; x1: number; y1: number };
  trunkBox: Record<TrunkKey, { x0: number; y0: number; x1: number; y1: number }>;
}

/** A talent's size: the more levels it holds, the bigger; abilities and first rows a little more. */
function radiusOf(d: TalentDef): number {
  const base = d.ability ? 21 : 15 + 2 * (d.max - 1);
  return d.axis === null ? 18 : base + (d.row === 0 ? 1.5 : 0);
}

/** A name's box in tree units: its lines at the names' size, up to NAME_W wide. */
const NAME_W = 96;
const NAME_LINE = 13.8;
const measure = typeof document !== 'undefined' ? document.createElement('canvas').getContext('2d') : null;

function nameBox(text: string): { w: number; h: number } {
  const width = (t: string) => (measure ? ((measure.font = '600 12.5px Barlow, sans-serif'), measure.measureText(t).width) : t.length * 6.6);
  const lines: string[] = [];
  let line = '';
  for (const word of text.split(' ')) {
    const next = line ? `${line} ${word}` : word;
    if (line && width(next) > NAME_W) {
      lines.push(line);
      line = word;
    } else line = next;
  }
  if (line) lines.push(line);
  return { w: Math.min(NAME_W, Math.max(...lines.map(width))) + 2, h: lines.length * NAME_LINE };
}

type Rect = { x0: number; y0: number; x1: number; y1: number };

function nameRect(n: NodeGeo, at: NodeGeo['label'], box: { w: number; h: number }): Rect {
  const { x, y } = n.p;
  const g = n.r + 6;
  switch (at) {
    case 'e':
      return { x0: x + g, y0: y - box.h / 2, x1: x + g + box.w, y1: y + box.h / 2 };
    case 'w':
      return { x0: x - g - box.w, y0: y - box.h / 2, x1: x - g, y1: y + box.h / 2 };
    case 's':
      return { x0: x - box.w / 2, y0: y + n.r + 10, x1: x + box.w / 2, y1: y + n.r + 10 + box.h };
    case 'n':
      return { x0: x - box.w / 2, y0: y - n.r - 4 - box.h, x1: x + box.w / 2, y1: y - n.r - 4 };
  }
}

const overlap = (a: Rect, b: Rect) => Math.max(0, Math.min(a.x1, b.x1) - Math.max(a.x0, b.x0)) * Math.max(0, Math.min(a.y1, b.y1) - Math.max(a.y0, b.y0));

/**
 * Where each name goes: the side away from its vine if it is clear, else the
 * first other side that is, else the one that covers least. Clear means off
 * the other names, the sockets and the branches' big nodes.
 */
function placeNames(nodes: NodeGeo[], branches: BranchGeo[]): void {
  const blocks: Rect[] = [
    ...nodes.map((n) => ({ x0: n.p.x - n.r - 3, y0: n.p.y - n.r - 3, x1: n.p.x + n.r + 3, y1: n.p.y + n.r + 10 })),
    ...branches.map((b) => ({ x0: b.node.x - 44, y0: b.node.y - 44, x1: b.node.x + 44, y1: b.node.y + 44 })),
    { x0: -MEDAL - 14, y0: -MEDAL - 14, x1: MEDAL + 14, y1: MEDAL + 14 },
    // The vines and limbs, as a string of small boxes: a name across a vine reads as crossed out.
    ...branches.flatMap((b) =>
      [b.vine, b.limb].flatMap((c) =>
        Array.from({ length: 26 }, (_, k) => {
          const q = at(c, k / 25);
          return { x0: q.x - 4, y0: q.y - 4, x1: q.x + 4, y1: q.y + 4 };
        }),
      ),
    ),
  ];
  const placed: Rect[] = [];
  // Inner rows first: they are read first.
  const order = [...nodes].sort((a, b) => a.d.row - b.d.row);
  for (const n of order) {
    const box = nameBox(n.d.name);
    const s = n.side;
    const first: NodeGeo['label'] = Math.abs(s.x) > 0.42 ? (s.x > 0 ? 'e' : 'w') : s.y > 0 ? 's' : 'n';
    const others: NodeGeo['label'][] = first === 'e' || first === 'w' ? [first === 'e' ? 'w' : 'e', s.y > 0 ? 's' : 'n', s.y > 0 ? 'n' : 's'] : [s.x > 0 ? 'e' : 'w', s.x > 0 ? 'w' : 'e', first === 's' ? 'n' : 's'];
    let best = first;
    let least = Infinity;
    for (const at of [first, ...others]) {
      const r = nameRect(n, at, box);
      const own = { x0: n.p.x - n.r - 3, y0: n.p.y - n.r - 3, x1: n.p.x + n.r + 3, y1: n.p.y + n.r + 10 };
      const hit = [...placed, ...blocks.filter((b) => b.x0 !== own.x0 || b.y0 !== own.y0)].reduce((sum, b) => sum + overlap(r, b), 0);
      if (hit < least) {
        least = hit;
        best = at;
      }
      if (!hit) break;
    }
    n.label = best;
    placed.push(nameRect(n, best, box));
  }
}

let cached: Layout | null = null;

/** Where everything grows: computed once, the same for every soldier. */
function layout(): Layout {
  if (cached) return cached;
  const trunks: TrunkGeo[] = [];
  const branches: BranchGeo[] = [];
  const nodes: NodeGeo[] = [];

  // The centre's four talents, a crown round the medal in the gaps between trunks.
  const crown = [-90, -18, 126, -162];
  CENTER.forEach((d, i) => {
    const p = pol(MEDAL + 46, crown[i]!);
    nodes.push({ d, p: stretch(p), from: stretch(pol(MEDAL, crown[i]!)), side: unit(p), r: radiusOf(d), trunk: 'assault', label: 'e' });
  });

  for (const def of TRUNKS) {
    const w = wobble(def.key);
    const bend = (def.key === 'song' ? 6 : 9) * (w >= 0 ? 1 : -1);
    // From under the medal (it is drawn over the trunks' roots).
    const p0 = pol(MEDAL - 18, def.angle);
    const p3 = pol(236, def.angle + bend);
    const body: Cubic = [p0, pol(140, def.angle - bend * 0.6), pol(196, def.angle + bend * 1.4), p3];
    const n = def.branches.length;
    const offsets = n === 4 ? [-27, -9, 9, 27] : [-25, 0, 25];
    def.branches.forEach(([axis, br], i) => {
      const key = `${axis}${br}`;
      // The branches fan from the trunk's own angle (its bend is only how it grows): neighbours keep their gap.
      const a = def.angle + offsets[i]! + wobble(key) * 2;
      const node = pol(316 + wobble(`${key}r`) * 14, a);
      const u = pol(1, a);
      // The limb leaves the trunk's end along it, then turns to its branch.
      const dir = tangent(body, 1);
      const limb: Cubic = [p3, add(p3, dir, 38), add(node, u, -34), node];
      // Each vine curls its own way; the outer ones only inward, so they never reach into the next trunk's.
      const side = n === 4 ? (i < 2 ? -1 : 1) : i - 1;
      const own = wobble(`${key}c`);
      // The Oficio's four trades, closer together, grow nearly straight.
      const curl = n === 4 ? own * 1.5 : side ? -side * (3 + Math.abs(own) * 3) : own * 5;
      const tipA = a + curl;
      const tip = pol(len(node) + 318, tipA);
      const vine: Cubic = [node, add(node, u, 104), add(tip, pol(1, tipA - curl * 0.5), -92), tip];
      const svine = stretchC(vine);
      branches.push({ axis, branch: br, trunk: def.key, node: stretch(node), limb: stretchC(limb), vine: svine, tip: stretch(tip), out: unit(sub(stretch(tip), stretch(node))) });

      const talents = talentsOf(axis).filter((d) => d.branch === br);
      for (let row = 0; row < 5; row++) {
        const here = talents.filter((d) => d.row === row);
        if (!here.length) continue;
        if (row === 0) {
          // The first row: petals round the branch's node, on its outward side.
          const spread = here.length === 1 ? [26 * (side || 1)] : [-38, 38];
          here.forEach((d, k) => {
            const ang = (Math.atan2(u.y, u.x) * 180) / Math.PI + spread[k]! + wobble(d.id) * 5;
            const p = add(node, pol(64 + wobble(`${d.id}r`) * 6, ang));
            nodes.push({ d, p: stretch(p), from: stretch(add(node, pol(36, ang))), side: unit(sub(stretch(p), stretch(node))), r: radiusOf(d), trunk: def.key, label: 'e' });
          });
          continue;
        }
        const t = ROW_T[row]!;
        const v = at(vine, t);
        const tn = tangent(vine, t);
        const nrm = { x: -tn.y, y: tn.x };
        // Later rows: leaves either side of the vine, a pair a little apart along it; a single one alternates sides.
        here.forEach((d, k) => {
          const s = here.length > 1 ? (k === 0 ? -1 : 1) : row % 2 ? 1 : -1;
          const off = here.length > 1 ? 44 : 26;
          const along = here.length > 1 ? s * 12 : 0;
          const p = add(add(v, nrm, s * (off + wobble(d.id) * 4)), tn, along + wobble(`${d.id}a`) * 5);
          nodes.push({ d, p: stretch(p), from: stretch(v), side: unit(sub(stretch(p), stretch(v))), r: radiusOf(d), trunk: def.key, label: 'e' });
        });
      }
    });
    trunks.push({ def, body: stretchC(body) });
  }

  placeNames(nodes, branches);

  // What the window frames: everything, and each trunk with the medal.
  const grow = (box: Layout['box'], p: Pt, m: number) => {
    box.x0 = Math.min(box.x0, p.x - m);
    box.y0 = Math.min(box.y0, p.y - m);
    box.x1 = Math.max(box.x1, p.x + m);
    box.y1 = Math.max(box.y1, p.y + m);
  };
  const empty = () => ({ x0: Infinity, y0: Infinity, x1: -Infinity, y1: -Infinity });
  const box = empty();
  const trunkBox = Object.fromEntries(TRUNKS.map((t) => [t.key, empty()])) as Layout['trunkBox'];
  for (const n of nodes) grow(box, n.p, n.r + 40);
  // A trunk is framed by its branches and their talents (the medal and the vines' far ends may fall outside).
  for (const b of branches) {
    grow(box, add(b.tip, b.out, 64), 46);
    grow(trunkBox[b.trunk], b.node, 50);
  }
  for (const n of nodes) if (n.d.axis !== null) grow(trunkBox[n.trunk], n.p, n.r + 34);
  // The branches' names stand past the vines' ends, the whole view keeps them in.
  for (const b of branches) {
    const { at, anchor } = rimOf(b);
    const w = rimWidth(BRANCHES[b.axis][b.branch].name.toUpperCase());
    const x0 = anchor === 'start' ? at.x : anchor === 'end' ? at.x - w : at.x - w / 2;
    const y = at.y + (b.out.y > 0.6 ? 24 : b.out.y < -0.6 ? -22 : 0);
    grow(box, { x: x0, y }, 30);
    grow(box, { x: x0 + w, y }, 30);
  }
  cached = { trunks, branches, nodes, box, trunkBox };
  return cached;
}

// ------------------------------------------------------------ the window

/** Board pixels per tree unit from which names and notes are drawn (close enough to read them). */
const NEAR = 0.6;
const MAX_SCALE = 1.5;

interface View {
  /** What the sheet asked for last: the whole tree or an axis. A new ask frames it again. */
  ask: string;
  mode: 'fit' | 'trunk' | 'free';
  trunk: TrunkKey;
  s: number;
  /** The tree point at the window's middle. */
  cx: number;
  cy: number;
}

/** Each soldier's window, kept while the sheet redraws (every click redraws it). */
const views = new Map<string, View>();

/** Where a branch's name sits, far away past its vine's end; the Oficio's four, close together, every other one further out. */
function rimOf(g: BranchGeo): { at: Pt; anchor: 'start' | 'end' | 'middle' } {
  const at = add(g.tip, g.out, g.branch === 3 && ['grenadier', 'specialist'].includes(g.axis) ? 74 : 34);
  return { at, anchor: g.out.x > 0.35 ? 'start' : g.out.x < -0.35 ? 'end' : 'middle' };
}

/** How wide a branch's name is at the rim (.tt-rim: Oswald 600, 27px, a pixel apart). */
function rimWidth(text: string): number {
  return measure ? ((measure.font = '600 27px Oswald, sans-serif'), measure.measureText(text).width + text.length) : text.length * 15;
}

function frame(box: Layout['box'], w: number, h: number, cap: number): { s: number; cx: number; cy: number } {
  const s = Math.min(cap, (w - 24) / (box.x1 - box.x0), (h - 24) / (box.y1 - box.y0));
  return { s, cx: (box.x0 + box.x1) / 2, cy: (box.y0 + box.y1) / 2 };
}

// ------------------------------------------------------------------ drawing

/** The structure under the nodes: trunks, limbs, vines lit as far as their rows open, twigs, notes on the curves. */
function structure(ctx: TreeContext, L: Layout): SVGSVGElement {
  const { box } = L;
  const parts: string[] = [];
  const b = ctx.b;
  const step = (axis: ClassId, br: number, row: number) => stepOf(b, ctx.echoes, axis, br, row);

  for (const t of L.trunks) {
    const k = t.def.key;
    const points = t.def.branches.reduce((sum, [a, br]) => sum + pointsInBranch(b.talents, a, br), 0);
    const live = points > 0 || k === b.cls;
    parts.push(`<g class="${trunkClass(k)}${live ? ' live' : ''}">`);
    for (const g of L.branches.filter((x) => x.trunk === k)) {
      const has = pointsInBranch(b.talents, g.axis, g.branch) > 0;
      parts.push(`<path class="tt-limb${has ? ' lit' : ''}" d="${taper(g.limb, 16, 9)}"/>`);
      // The vine, a stretch per row: lit while its rows open, dashed past the class's bar or the Taller.
      for (let row = 1; row < 5; row++) {
        const st = step(g.axis, g.branch, row);
        const lit = st.lit && (row === 1 || step(g.axis, g.branch, row - 1).lit);
        const c = piece(g.vine, ROW_T[row - 1]!, ROW_T[row]!);
        parts.push(`<path class="tt-vine${lit ? ' lit' : ''}${st.barred || st.taller ? ' barred' : ''}" d="${cubicD(c)}"/>`);
      }
      // What the branch is about, on its limb (close).
      const id = `tt-note-${g.axis}-${g.branch}`;
      parts.push(`<path id="${id}" d="${cubicD(piece(g.limb, 0.32, 0.98))}" fill="none"/>`);
      const focus = BRANCHES[g.axis][g.branch].focus.replace(/^Oficio: /, '');
      parts.push(`<text class="tt-note near" dy="-11"><textPath href="#${id}" startOffset="50%" text-anchor="middle">${esc(focus)}</textPath></text>`);
      // Far away, the branch's name past the end of its vine.
      const rim = rimOf(g);
      parts.push(`<text class="tt-rim far${has ? ' lit' : ''}" x="${f(rim.at.x)}" y="${f(rim.at.y)}" text-anchor="${rim.anchor}" dy="${g.out.y > 0.6 ? '0.9em' : g.out.y < -0.6 ? '-0.1em' : '0.35em'}">${esc(BRANCHES[g.axis][g.branch].name.toUpperCase())}</text>`);
      // The points a row asks for: small triangles on the vine (close).
      for (let row = 1; row < 5; row++) {
        const st = step(g.axis, g.branch, row);
        const tm = row === SECOND_AXIS_ROWS && g.branch !== 3 ? BAR_T + 0.05 : (ROW_T[row - 1]! + ROW_T[row]!) / 2;
        const p = at(g.vine, tm);
        const tn = tangent(g.vine, tm);
        const ok = st.above >= st.gate && !st.taller;
        const tri = [0, 120, 240].map((o) => {
          const a = Math.atan2(tn.y, tn.x) + (o * Math.PI) / 180;
          return `${f(p.x + Math.cos(a) * 7.5)},${f(p.y + Math.sin(a) * 7.5)}`;
        });
        const nrm = { x: -tn.y, y: tn.x };
        const q = add(p, nrm, -14);
        parts.push(`<polygon class="tt-gate near${ok ? ' ok' : ''}${st.taller ? ' taller' : ''}" points="${tri.join(' ')}"/>`);
        parts.push(`<text class="tt-gate-n near${ok ? ' ok' : ''}${st.taller ? ' taller' : ''}" x="${f(q.x)}" y="${f(q.y)}" text-anchor="middle" dy="0.35em">${st.taller ? `Taller ${st.taller}` : st.gate}</text>`);
      }
    }
    // The trunk, thick and thinning, its name along it.
    parts.push(`<path class="tt-trunk" d="${taper(t.body, 46, 26)}"/>`);
    const left = t.body[3].x < t.body[0].x;
    const nid = `tt-name-${k}`;
    const line = left ? ([t.body[3], t.body[2], t.body[1], t.body[0]] as Cubic) : t.body;
    parts.push(`<path id="${nid}" d="${cubicD(piece(line, left ? 0.08 : 0.16, left ? 0.84 : 0.92))}" fill="none"/>`);
    // As big as the trunk's length allows (Francotirador is long).
    const word = trunkName(k).toUpperCase();
    const room = len(sub(at(line, left ? 0.84 : 0.92), at(line, left ? 0.08 : 0.16)));
    const size = Math.min(22, room / (word.length * 0.74));
    parts.push(`<text class="tt-name" dy="0.36em" style="font-size:${f(size)}px;letter-spacing:${f(size * 0.16)}px"><textPath href="#${nid}" startOffset="50%" text-anchor="middle">${esc(word)}</textPath></text>`);
    // The bar before the class's two rows: a crossbar on each of the axis's vines, solid on the soldier's own
    // axis; its note runs along the middle one's crossbar, like a sign on a fence (close).
    if (k !== 'song') {
      const own = k === b.cls;
      L.branches
        .filter((g) => g.trunk === k)
        .forEach((g, i) => {
          const p = at(g.vine, BAR_T);
          const tn = tangent(g.vine, BAR_T);
          const nrm = { x: -tn.y, y: tn.x };
          const a = add(p, nrm, -17);
          const z = add(p, nrm, 17);
          parts.push(`<path class="tt-bar${own ? ' own' : ''}" d="M${f(a.x)} ${f(a.y)}L${f(z.x)} ${f(z.y)}"/>`);
          if (i !== 1) return;
          // A short sign at the crossbar's end, on the side away from the fourth row's node (it hangs on the
          // other), reading left to right whichever way that is.
          const away = { x: -nrm.x, y: -nrm.y };
          const near = add(p, away, 22);
          const far = add(p, away, 130);
          const ltr = away.x >= 0;
          const bid = `tt-bar-${k}`;
          parts.push(`<path id="${bid}" d="M${f((ltr ? near : far).x)} ${f((ltr ? near : far).y)}L${f((ltr ? far : near).x)} ${f((ltr ? far : near).y)}" fill="none"/>`);
          const note = own ? 'Su clase' : 'Solo su clase';
          parts.push(`<text class="tt-bar-note near${own ? ' own' : ''}" dy="0.35em"><textPath href="#${bid}"${ltr ? '' : ' startOffset="100%" text-anchor="end"'}>${esc(note)}</textPath></text>`);
        });
    }
    parts.push('</g>');
  }

  // Twigs from the vines to the talents, and a dashed line where a talent needs another first.
  for (const n of L.nodes) {
    const st = stateOf(ctx, n.d);
    const mid = add({ x: (n.from.x + n.p.x) / 2, y: (n.from.y + n.p.y) / 2 }, n.side, 5);
    parts.push(`<path class="${trunkClass(n.trunk)} tt-twig${st.level || st.open ? ' lit' : ''}${n.d.axis === null ? ' centre' : ''}" d="M${f(n.from.x)} ${f(n.from.y)}Q${f(mid.x)} ${f(mid.y)} ${f(n.p.x)} ${f(n.p.y)}"/>`);
    if (n.d.needs) {
      const first = L.nodes.find((x) => x.d.id === n.d.needs);
      if (first) {
        const bend = add({ x: (first.p.x + n.p.x) / 2, y: (first.p.y + n.p.y) / 2 }, n.side, 26);
        parts.push(`<path class="${trunkClass(n.trunk)} tt-needs" d="M${f(first.p.x)} ${f(first.p.y)}Q${f(bend.x)} ${f(bend.y)} ${f(n.p.x)} ${f(n.p.y)}"/>`);
      }
    }
  }

  const w = box.x1 - box.x0;
  const hgt = box.y1 - box.y0;
  const t = document.createElement('template');
  t.innerHTML = `<svg class="tt-svg" xmlns="http://www.w3.org/2000/svg" viewBox="${f(box.x0)} ${f(box.y0)} ${f(w)} ${f(hgt)}" width="${f(w)}" height="${f(hgt)}" style="left:${f(box.x0)}px;top:${f(box.y0)}px" aria-hidden="true">
    <ellipse class="tt-ring" cx="0" cy="0" rx="${f(360 * SX)}" ry="${f(360 * SY)}"/>
    <ellipse class="tt-ring" cx="0" cy="0" rx="${f(560 * SX)}" ry="${f(560 * SY)}"/>
    ${parts.join('')}
  </svg>`;
  return t.content.firstElementChild as SVGSVGElement;
}

const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** Shows `d` by `at` (the node's button), or hides it. */
type Inspect = (d: TalentDef | null, at?: HTMLElement) => void;

/** A talent: a socket in its family's colour, sized by what it holds, its name beside it. */
function nodeEl(ctx: TreeContext, n: NodeGeo, inspect: Inspect): HTMLElement[] {
  const d = n.d;
  const st = stateOf(ctx, d);
  const cls =
    `.${trunkClass(n.trunk)}${d.axis === null ? '.centre' : ''}${d.ability ? '.ab' : ''}${st.level ? '.taken' : ''}${st.level >= d.max ? '.full' : ''}${st.fresh ? '.drafted' : ''}` +
    `${st.open ? '.open' : ''}${st.shut ? '.shut' : ''}${st.sleeping ? '.sleeping' : ''}${d.coop ? '.coop' : ''}${ctx.focus === d.id ? '.focus' : ''}`;
  const pips = d.max > 1 ? h('span.tn-pips', {}, ...Array.from({ length: d.max }, (_, i) => h(`i${i < st.level - st.fresh ? '.on' : i < st.level ? '.plan' : ''}`))) : null;
  // What it asks for, stuck to it: an aptitude in a triangle, a class's kit in a rhombus.
  const marks: HTMLElement[] = [];
  if (d.req) marks.push(h(`span.tn-req${ctx.b.attrs[d.req[0]] >= d.req[1] ? '.ok' : ''}`, { title: `Pide ${ATTRS[d.req[0]].name} ${d.req[1]}` }, h('i'), h('b', {}, `${ATTRS[d.req[0]].short} ${d.req[1]}`)));
  if (d.kit) marks.push(h(`span.tn-kit${carriesKit(ctx.b, d.kit, ctx.arms) ? '.ok' : ''}`, { title: `Funciona con ${KIT_NAMES[d.kit]}` }, h('i')));
  const button = h(
    `button.tn${cls}`,
    {
      type: 'button',
      style: { left: `${f(n.p.x)}px`, top: `${f(n.p.y)}px`, '--r': `${f(n.r)}px` },
      'aria-disabled': st.open ? 'false' : 'true',
      'aria-label': `${d.name}, ${st.level} de ${d.max}${st.blocked && !st.open ? `. ${st.blocked}` : ''}`,
      onmouseenter: (e: Event) => inspect(d, e.currentTarget as HTMLElement),
      onmouseleave: () => inspect(null),
      onfocus: (e: Event) => inspect(d, e.currentTarget as HTMLElement),
      onblur: () => inspect(null),
      onclick: (e: Event) => {
        if (st.open) ctx.onAdd(d.id);
        else {
          ctx.onFocus(d.id);
          inspect(d, e.currentTarget as HTMLElement);
        }
      },
      oncontextmenu: (e: Event) => {
        if (!st.fresh) return;
        e.preventDefault();
        ctx.onRemove(d.id);
      },
    },
    disc(iconOf(d)),
    pips,
    ...marks,
  );
  // The name where the layout found room for it (placeNames).
  const at = n.label;
  const x = at === 'e' ? n.p.x + n.r + 6 : at === 'w' ? n.p.x - n.r - 6 : n.p.x;
  const y = at === 's' ? n.p.y + n.r + 10 : at === 'n' ? n.p.y - n.r - 4 : n.p.y;
  const label = h(`span.tn-name.near.${trunkClass(n.trunk)}${d.axis === null ? '.centre' : ''}.${at}${st.level ? '.taken' : ''}${st.open ? '.open' : ''}${st.shut ? '.shut' : ''}`, { style: { left: `${f(x)}px`, top: `${f(y)}px` } }, d.name);
  return [button, label];
}

/** A branch's big node: its emblem, its name and its points. */
function branchEl(ctx: TreeContext, g: BranchGeo): HTMLElement {
  const def = BRANCHES[g.axis][g.branch];
  const pts = pointsInBranch(ctx.b.talents, g.axis, g.branch);
  const song = g.branch === 3;
  return h(
    `div.tb.${trunkClass(g.trunk)}${pts ? '.lit' : ''}`,
    {
      style: { left: `${f(g.node.x)}px`, top: `${f(g.node.y)}px` },
      title: song ? `${def.name} · Oficio de ${TEMPLATES[g.axis].name} · ${ctx.echoes ? `Taller de oficios nivel ${ctx.echoes}` : 'hace falta el Taller de oficios'}` : `${def.name}: ${def.focus}`,
    },
    disc(BRANCH_ICON[g.axis][g.branch], '.emblem'),
    h('b.tb-name.near', {}, def.name),
    h('small.tb-points.near', {}, pts ? plural(pts, 'punto') : 'sin puntos'),
  );
}

/** The soldier in the middle: their class's portrait, the class and the rank on a band. */
function medalEl(ctx: TreeContext): HTMLElement {
  const cls = ctx.b.cls;
  const file = cls ? { assault: 'asalto', grenadier: 'granadero', sharpshooter: 'francotirador', specialist: 'especialista' }[cls] : 'recluta';
  return h(
    `div.tm${cls ? `.axis-${cls}` : ''}`,
    { style: { '--portrait': `url('/ui/synty/renders/${file}.webp')` } },
    h('span.tm-face'),
    h('span.tm-band', {}, `${cls ? TEMPLATES[cls].name : CENTER_NAME} · ${RANKS[ctx.b.rank]!.name}`),
  );
}

// ------------------------------------------------------- card and figures

/**
 * The card of the node under the pointer (or with the keyboard's focus), by
 * it: what it does, what it lacks, what a click does. It floats over the
 * sheet, so the window keeps its size whatever the text (playtest 14/10 #14,
 * #15). A click on an open node learns a level; a level of this promotion
 * goes with a right click.
 */
function tooltip(ctx: TreeContext): { el: HTMLElement; show: (d: TalentDef | null, at?: HTMLElement) => void } {
  const el = h('div.tree-inspect.tree-tip', { role: 'tooltip' });
  const show = (d: TalentDef | null, at?: HTMLElement) => {
    if (!d || !at) {
      el.remove();
      return;
    }
    clear(el);
    el.className = `tree-inspect tree-tip${d.axis ? ` ${d.song ? 'tk-song' : `axis-${d.axis}`}` : ''}`;
    const st = stateOf(ctx, d);
    const where = d.axis ? `${TEMPLATES[d.axis].name} · ${BRANCHES[d.axis][d.branch].name} · fila ${d.row + 1}` : `${CENTER_NAME} · para todos`;
    const notes: HTMLElement[] = [];
    if (d.req) {
      const have = ctx.b.attrs[d.req[0]];
      notes.push(h(`li${have < d.req[1] ? '.miss' : '.ok'}`, {}, `Pide ${ATTRS[d.req[0]].name} ${d.req[1]} (tiene ${have}).`));
    }
    if (d.needs) notes.push(h(`li${ctx.b.talents[d.needs] ? '.ok' : '.miss'}`, {}, `Antes, ${TALENTS[d.needs].name}.`));
    if (d.kit && !carriesKit(ctx.b, d.kit, ctx.arms)) notes.push(h('li.sleep', {}, `${st.sleeping ? 'Dormido' : 'Dormirá'} hasta llevar ${KIT_NAMES[d.kit]}.`));
    if (d.coop) notes.push(h('li.coop', {}, 'Con tu compañero: solo funciona junto a sus soldados.'));
    const becomes = wouldSpecialize(ctx.b, d.id);
    if (becomes && st.open) notes.push(h('li.ok', {}, `Con este punto se especializa como ${TEMPLATES[becomes].name}.`));
    // What a click does, or what is missing, in plain words.
    let status: HTMLElement;
    if (st.level >= d.max) status = h('p.ti-status.done', {}, st.level > 1 ? 'Aprendido al máximo.' : 'Aprendido.');
    else if (st.open) status = h('p.ti-status.open', {}, `Clic: +1 nivel (${st.level + 1}/${d.max}).`);
    else if (st.blocked && NO_POINTS.test(st.blocked) && d.axis) {
      const p = stepOf(ctx.b, ctx.echoes, d.axis, d.branch, d.row);
      status = h('p.ti-status', {}, ctx.promoting ? st.blocked : 'Sin puntos que repartir ahora.', p.above < p.gate ? ` Además, esta fila pide ${p.gate} en ${BRANCHES[d.axis][d.branch].name} (lleva ${p.above}).` : '');
    } else status = h('p.ti-status', {}, st.blocked ?? '');
    append(
      el,
      h('div.x-group', {}, where),
      h(
        'div.ti-head',
        {},
        disc(iconOf(d), `${d.ability ? '.ab' : ''}${st.level ? '.taken' : ''}`),
        h(
          'div',
          {},
          h('b.ti-name', {}, d.name),
          h('small.ti-kind', {}, d.ability ? 'Habilidad: un botón más en combate' : 'Pasivo', d.song ? ' · Oficio' : ''),
          h('span.tn-pips', {}, ...Array.from({ length: d.max }, (_, i) => h(`i${i < st.level - st.fresh ? '.on' : i < st.level ? '.plan' : ''}`)), h('small', {}, ` ${st.level}/${d.max}`)),
        ),
      ),
      h('p.ti-desc', {}, d.description),
      notes.length ? h('ul.ti-notes', {}, ...notes) : null,
      status,
      st.fresh ? h('p.ti-undo', {}, 'Clic derecho: quitar el nivel de este ascenso.') : null,
    );
    // By the node, inside the sheet: to its right if there is room, else to its left; never past the sheet's edges.
    const host = at.closest<HTMLElement>('.x-panel') ?? at.parentElement!;
    if (el.parentElement !== host) host.append(el);
    const box = host.getBoundingClientRect();
    const node = at.getBoundingClientRect();
    // The sheet may be zoomed (--ui-scale): screen pixels back to the sheet's own.
    const scale = box.width / host.offsetWidth || 1;
    const w = el.offsetWidth * scale;
    const tall = el.offsetHeight * scale;
    const right = node.right + 12 + w <= box.right - 8;
    const x = right ? node.right + 12 : Math.max(box.left + 8, node.left - 12 - w);
    const y = Math.min(Math.max(box.top + 8, node.top + node.height / 2 - tall / 2), box.bottom - 8 - tall);
    el.style.left = `${(x - box.left) / scale + host.scrollLeft}px`;
    el.style.top = `${(y - box.top) / scale + host.scrollTop}px`;
  };
  return { el, show };
}

/** The sheet's figures: what each is called and how it is read from soldierStats. */
const FIGURES: [string, string, (st: ReturnType<typeof soldierStats>) => number][] = [
  ['hp', 'Salud', (st) => st.maxHp],
  ['aim', 'Puntería', (st) => TEMPLATES[st.template].aim + st.mods.aim],
  ['defense', 'Defensa', (st) => TEMPLATES[st.template].defense + st.mods.defense],
  ['will', 'Voluntad', (st) => st.will],
  ['mobility', 'Movilidad', (st) => TEMPLATES[st.template].mobility + st.mods.mobility],
  ['crit', 'Crítico', (st) => TEMPLATES[st.template].crit + st.mods.crit],
  ['armor', 'Blindaje', (st) => st.armor],
];

/**
 * The soldier's figures, top right of the sheet (playtest 14/10 #16), with
 * the plan laid on; under the pointer, what a level more of that node adds,
 * in green (or takes, in red).
 */
function figures(ctx: TreeContext): { el: HTMLElement; preview: (d: TalentDef | null) => void } {
  const now = soldierStats(ctx.b);
  const cells = FIGURES.map(([key, name, read]) => {
    const delta = h('small.tf-delta');
    return { read, delta, el: h(`span.tf.${key}`, {}, h('small.tf-name', {}, name), h('b', {}, String(read(now))), delta) };
  });
  const el = h('div.tree-figures', {}, ...cells.map((c) => c.el));
  const preview = (d: TalentDef | null) => {
    let next: ReturnType<typeof soldierStats> | null = null;
    if (d && (ctx.b.talents[d.id] ?? 0) < d.max) {
      const b = structuredClone(ctx.b);
      learnTalent(b, d.id);
      next = soldierStats(b);
    }
    for (const c of cells) {
      const diff = next ? c.read(next) - c.read(now) : 0;
      c.delta.textContent = diff ? `${diff > 0 ? '+' : '−'}${Math.abs(diff)}` : '';
      c.delta.className = `tf-delta${diff > 0 ? ' up' : diff < 0 ? ' down' : ''}`;
    }
  };
  return { el, preview };
}

// ------------------------------------------------------------------ the window

/** The trunks as chips over the window: what each is to the soldier and its points; a click takes the window there. */
function trunkBar(ctx: TreeContext, go: (k: TrunkKey) => void): HTMLElement {
  const b = ctx.b;
  return h(
    'div.tree-axes',
    {},
    ...TRUNKS.map((t) => {
      const points = t.branches.reduce((sum, [a, br]) => sum + pointsInBranch(b.talents, a, br), 0);
      const role =
        t.key === 'song'
          ? ctx.echoes
            ? `Taller nivel ${ctx.echoes}`
            : 'Sin Taller'
          : t.key === b.cls
            ? 'Su clase'
            : points
              ? b.cls
                ? 'Segundo eje'
                : 'Aprendiendo'
              : 'Sin empezar';
      return h(
        `button.tree-axis.${trunkClass(t.key)}${points || t.key === b.cls ? '.live' : ''}`,
        { type: 'button', onclick: () => go(t.key), title: `Llevar la ventana a ${trunkName(t.key)}` },
        h('b', {}, trunkName(t.key)),
        h('small', {}, [role, plural(points, 'punto')].join(' · ')),
      );
    }),
  );
}

/**
 * The tree, and the soldier's figures for the sheet (screen.ts places them as
 * `inspect`; styles put them top right): a node under the pointer shows its
 * card by it and what it adds in the figures.
 */
export function skillTree(ctx: TreeContext): { tree: HTMLElement; inspect: HTMLElement } {
  const L = layout();
  const tip = tooltip(ctx);
  const figs = figures(ctx);
  const inspect: Inspect = (d, at) => {
    tip.show(d, at);
    figs.preview(d);
  };

  // The window keeps its frame for the soldier across redraws; the sheet's own «whole/axis» asks for a new one.
  const ask = ctx.whole ? 'whole' : `axis:${ctx.axis}`;
  let view = views.get(ctx.s.id);
  if (!view || view.ask !== ask) {
    view = { ask, mode: ctx.whole ? 'fit' : 'trunk', trunk: ctx.axis, s: 0, cx: 0, cy: 0 };
    views.set(ctx.s.id, view);
  }
  const v = view;

  const world = h('div.tree-world');
  world.append(structure(ctx, L));
  for (const g of L.branches) world.append(branchEl(ctx, g));
  world.append(h('div.tm-at', {}, medalEl(ctx)));
  for (const n of L.nodes) world.append(...nodeEl(ctx, n, inspect));

  const zoomText = h('span.tree-zoom-n');
  const board = h('div.tree-board', { onmouseleave: () => inspect(null) }, world);
  const size = () => ({ w: board.clientWidth, h: board.clientHeight });
  const fitScale = () => {
    const { w, h: hh } = size();
    return frame(L.box, w, hh, MAX_SCALE).s;
  };
  const apply = () => {
    const { w, h: hh } = size();
    if (!w || !hh) return false;
    if (v.mode !== 'free') {
      const target = v.mode === 'fit' ? frame(L.box, w, hh, MAX_SCALE) : frame(L.trunkBox[v.trunk], w, hh, 0.95);
      v.s = target.s;
      v.cx = target.cx;
      v.cy = target.cy;
    }
    world.style.transform = `translate(${f(w / 2 - v.cx * v.s)}px, ${f(hh / 2 - v.cy * v.s)}px) scale(${v.s.toFixed(4)})`;
    board.classList.toggle('near', v.s >= NEAR);
    zoomText.textContent = `${Math.round((v.s / fitScale()) * 100)} %`;
    return true;
  };
  const zoomBy = (k: number, mx?: number, my?: number) => {
    const { w, h: hh } = size();
    const s = Math.max(fitScale() * 0.85, Math.min(MAX_SCALE, v.s * k));
    const px = mx ?? w / 2;
    const py = my ?? hh / 2;
    // The tree point under the pointer stays under it.
    const tx = (px - w / 2) / v.s + v.cx;
    const ty = (py - hh / 2) / v.s + v.cy;
    v.cx = tx - (px - w / 2) / s;
    v.cy = ty - (py - hh / 2) / s;
    v.s = s;
    v.mode = 'free';
    apply();
  };
  /** The sheet may be zoomed (--ui-scale): screen pixels to the board's own. */
  const ratio = () => board.getBoundingClientRect().width / (board.offsetWidth || 1) || 1;

  // Moving: the wheel zooms round the pointer; dragging the board (not a node) pans.
  board.addEventListener(
    'wheel',
    (e) => {
      e.preventDefault();
      const r = board.getBoundingClientRect();
      const k = ratio();
      board.classList.add('moving');
      zoomBy(Math.exp(-e.deltaY * 0.0016), (e.clientX - r.left) / k, (e.clientY - r.top) / k);
      window.clearTimeout(Number(board.dataset.still ?? 0));
      board.dataset.still = String(window.setTimeout(() => board.classList.remove('moving'), 160));
    },
    { passive: false },
  );
  let drag: { x: number; y: number; cx: number; cy: number; id: number } | null = null;
  board.addEventListener('pointerdown', (e) => {
    if (e.button !== 0 || (e.target as Element).closest('.tn, .tree-ctl')) return;
    drag = { x: e.clientX, y: e.clientY, cx: v.cx, cy: v.cy, id: e.pointerId };
    board.setPointerCapture(e.pointerId);
    board.classList.add('moving');
  });
  board.addEventListener('pointermove', (e) => {
    if (!drag || e.pointerId !== drag.id) return;
    const k = ratio() * v.s;
    v.cx = drag.cx - (e.clientX - drag.x) / k;
    v.cy = drag.cy - (e.clientY - drag.y) / k;
    v.mode = 'free';
    apply();
  });
  const drop = (e: PointerEvent) => {
    if (!drag || e.pointerId !== drag.id) return;
    drag = null;
    board.classList.remove('moving');
  };
  board.addEventListener('pointerup', drop);
  board.addEventListener('pointercancel', drop);

  const go = (k: TrunkKey) => {
    v.mode = 'trunk';
    v.trunk = k;
    apply();
  };
  board.append(
    h(
      'div.tree-ctl',
      {},
      h('button.x-btn.small', { type: 'button', title: 'Alejar', onclick: () => zoomBy(1 / 1.3) }, '−'),
      zoomText,
      h('button.x-btn.small', { type: 'button', title: 'Acercar', onclick: () => zoomBy(1.3) }, '+'),
      h('button.x-btn.small', { type: 'button', onclick: () => ((v.mode = 'fit'), apply()) }, 'Ver todo'),
    ),
  );

  // Framed once the board has its size (it is built off the page), and again if the window changes. The
  // first frame is set at once (from the redraw before, or anew), not flown into from the corner.
  board.classList.add('moving');
  let framed = false;
  const settle = (tries = 0) => {
    if (framed || apply() || tries > 30) {
      framed = true;
      requestAnimationFrame(() => board.classList.remove('moving'));
      return;
    }
    requestAnimationFrame(() => settle(tries + 1));
  };
  requestAnimationFrame(() => settle());
  if (typeof ResizeObserver !== 'undefined') {
    const watch = new ResizeObserver(() => {
      if (!board.isConnected) return watch.disconnect();
      if (apply()) framed = true;
    });
    watch.observe(board);
  }

  const tree = h(
    'div.skill-tree',
    { onmouseleave: () => inspect(null) },
    trunkBar(ctx, go),
    board,
    h(
      'p.tree-legend',
      {},
      h('span.lg.taken', {}, h('i'), 'Aprendido'),
      h('span.lg.plan', {}, h('i'), 'En este ascenso'),
      h('span.lg.open', {}, h('i'), 'Se puede aprender'),
      h('span.lg.shut', {}, h('i'), 'Cerrado'),
      h('span.lg.ab', {}, h('i'), 'Habilidad'),
      h('span.lg.req', {}, h('i'), 'Requisito'),
      h('span.lg.kit', {}, h('i'), 'Equipo de una clase'),
      h('span.lg.needs', {}, h('i'), 'Antes, otro talento'),
      h('span.lg.coop', {}, h('i'), 'Con el compañero'),
      h('span.tree-hint', {}, 'Rueda: acercar · arrastrar: mover · clic: aprender · clic derecho: quitar'),
    ),
  );
  void AXES;
  return { tree, inspect: figs.el };
}
