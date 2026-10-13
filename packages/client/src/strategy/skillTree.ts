import './skillTree.css';
import {
  ATTRS,
  AXES,
  axesOf,
  BRANCHES,
  CENTER,
  CENTER_NAME,
  dormant,
  KIT_CLASS,
  KIT_NAMES,
  leaningClass,
  pointsAbove,
  pointsInAxis,
  pointsInBranch,
  rowGate,
  ROW_GATES,
  SECOND_AXIS_ROWS,
  songRowLevel,
  songRowsOpen,
  SPECIALIZE_POINTS,
  SPECIALIZE_RANK,
  SPECIALIZE_RANK_NAME,
  talentBlocker,
  talentsOf,
  TALENTS,
  TEMPLATES,
  wouldSpecialize,
  type AbilityId,
  type Build,
  type CampaignSoldier,
  type ClassId,
  type PerkId,
  type TalentDef,
} from '@bastion/engine';
import { append, clear, h } from '../ui/dom';
import { abilityIcon } from '../ui/icons';

// The «Árbol de habilidades» of a soldier's sheet: the centre (Recluta) as the
// root, and the four axes of the engine's tree (classes.ts) as paths of
// connected nodes, one branch per path, a row per step. A step's path is lit
// once the branch has the points it asks for above it; the last two rows sit
// past a bar that only the soldier's main axis crosses. One axis is shown at a
// time; «Ver el árbol entero» lays the four of them around the centre. The
// panel next to the aptitudes tells what the node under the pointer does and
// why it is closed. Every rule is the engine's: talentBlocker, rowGate,
// pointsAbove, axesOf; names and texts are read from classes.ts.

export interface TreeContext {
  /** The soldier as saved. */
  s: CampaignSoldier;
  /** The soldier with the draft laid on (planned in screen.ts). */
  b: Build;
  /** Level of the Taller de oficios. */
  echoes: number;
  /** Points are being spent: open nodes take a level on click. */
  promoting: boolean;
  /** The axis open in the detailed view. */
  axis: ClassId;
  /** The four axes around the centre instead of one. */
  whole: boolean;
  /** The node the inspector shows when the pointer is elsewhere. */
  focus: PerkId | null;
  clsTag: (cls: ClassId | null) => HTMLElement;
  onAxis: (axis: ClassId) => void;
  onAdd: (id: PerkId) => void;
  onRemove: (id: PerkId) => void;
  onFocus: (id: PerkId | null) => void;
  /** From the whole tree into one axis, on that node. */
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

function disc(icon: AbilityId, extra: string = ''): HTMLElement {
  return h(`span.tnode-disc${extra}`, { 'data-ability': icon }, abilityIcon(icon));
}

// ------------------------------------------------------------ the rules

const NO_POINTS = /No quedan puntos/;

/** Where a row of a branch stands: its gate, the points above it, and what else holds it shut. */
function step(ctx: TreeContext, axis: ClassId, branch: number, row: number) {
  const song = branch === 3;
  const gate = rowGate({ row: row as TalentDef['row'], song });
  const above = pointsAbove(ctx.b.talents, axis, branch, row);
  const taller = song && row >= songRowsOpen(ctx.echoes) ? songRowLevel(row) : 0;
  const barred = row >= SECOND_AXIS_ROWS && axis !== ctx.b.cls;
  return { gate, above, taller, barred, lit: above >= gate && !taller && !barred };
}

type Role = 'main' | 'second' | 'lean' | 'open' | 'closed' | 'free';

/** What an axis is to this soldier: its class, its second axis, where a recruit leans, or shut. */
function roleOf(b: Build, axis: ClassId): Role {
  if (axis === b.cls) return 'main';
  const { open } = axesOf(b);
  if (open.includes(axis)) return b.cls ? 'second' : axis === leaningClass(b) ? 'lean' : 'open';
  return open.length >= (b.cls ? 1 : 2) ? 'closed' : 'free';
}

const ROLE_TEXT: Record<Role, string> = {
  main: 'Su clase',
  second: 'Segundo eje',
  lean: 'Se inclina aquí',
  open: 'Aprendiendo',
  closed: 'Cerrado',
  free: '',
};

/** A node's state, from the engine's own blocker. */
function stateOf(ctx: TreeContext, d: TalentDef) {
  const level = ctx.b.talents[d.id] ?? 0;
  const fresh = level - (ctx.s.talents[d.id] ?? 0);
  const blocked = talentBlocker(ctx.b, d.id, ctx.echoes);
  const open = ctx.promoting && !blocked;
  // Out of points is not a lock: the node keeps its look, the path says the rest.
  const shut = !level && !!blocked && !NO_POINTS.test(blocked);
  return { level, fresh, blocked, open, shut, sleeping: level > 0 && dormant(ctx.b, d.id) };
}

// ------------------------------------------------------------------ nodes

function nodeEl(ctx: TreeContext, d: TalentDef, inspect: (d: TalentDef | null) => void, mini = false, zoom?: ClassId): HTMLElement {
  const st = stateOf(ctx, d);
  const cls =
    `${d.ability ? '.ab' : ''}${st.level ? '.taken' : ''}${st.level >= d.max ? '.full' : ''}${st.fresh ? '.drafted' : ''}` +
    `${st.open ? '.open' : ''}${st.shut ? '.shut' : ''}${st.sleeping ? '.sleeping' : ''}${d.coop ? '.coop' : ''}${d.song ? '.song' : ''}${ctx.focus === d.id ? '.focus' : ''}`;
  const pips = h('span.tnode-pips', {}, ...Array.from({ length: d.max }, (_, i) => h(`i${i < st.level - st.fresh ? '.on' : i < st.level ? '.plan' : ''}`)));
  return h(
    `button.tnode${mini ? '.mini' : ''}${cls}`,
    {
      type: 'button',
      'aria-disabled': st.open ? 'false' : 'true',
      'aria-label': `${d.name}, ${st.level} de ${d.max}${st.blocked && !st.open ? `. ${st.blocked}` : ''}`,
      onmouseenter: () => inspect(d),
      onfocus: () => inspect(d),
      onclick: () => {
        if (zoom) ctx.onZoom(zoom, d.id);
        else if (st.open) ctx.onAdd(d.id);
        else {
          ctx.onFocus(d.id);
          inspect(d);
        }
      },
      oncontextmenu: (e: Event) => {
        if (!st.fresh || zoom) return;
        e.preventDefault();
        ctx.onRemove(d.id);
      },
    },
    h('span.tnode-mark', {}, disc(iconOf(d)), pips),
    mini ? null : h('span.tnode-name', {}, d.name),
    // The path again under the name, down to the next row (a lone node sits on it).
    h('span.tnode-tail'),
    st.fresh && !mini
      ? h(
          'span.tnode-undo',
          {
            title: 'Quitar este nivel',
            onclick: (e: Event) => {
              e.stopPropagation();
              ctx.onRemove(d.id);
            },
          },
          '✕',
        )
      : null,
  );
}

/** A row of one branch: the nodes on their path, the gate on top. */
function stepEl(ctx: TreeContext, axis: ClassId, branch: number, row: number, inspect: (d: TalentDef | null) => void, mini = false, zoom?: ClassId): HTMLElement {
  const nodes = talentsOf(axis).filter((d) => d.branch === branch && d.row === row);
  const st = step(ctx, axis, branch, row);
  const name = BRANCHES[axis][branch].name;
  // The gate is in the row's margin (the same for the three ordinary branches); a row the
  // Taller de oficios still holds carries its lock on the path.
  const pill = !mini && st.taller ? h('span.tstep-lock', { title: `Hace falta el Taller de oficios a nivel ${st.taller}` }, `Taller ${st.taller}`) : null;
  const title = row ? `Esta fila pide ${st.gate} ${st.gate === 1 ? 'punto' : 'puntos'} en ${name}, en las filas de arriba (lleva ${st.above})` : '';
  return h(
    `div.tstep.b${branch}${st.lit ? '.lit' : ''}${st.barred ? '.barred' : ''}${st.taller ? '.taller' : ''}${row === ROW_GATES.length - 1 ? '.last' : ''}${nodes.length > 1 ? '.pair' : ''}`,
    { title },
    pill,
    h('div.tstep-nodes', {}, ...nodes.map((d) => nodeEl(ctx, d, inspect, mini, zoom))),
  );
}

// -------------------------------------------------------------- inspector

function inspector(ctx: TreeContext): { el: HTMLElement; show: (d: TalentDef | null) => void } {
  const el = h('div.tree-inspect', { 'aria-live': 'polite' });
  const show = (d: TalentDef | null) => {
    clear(el);
    if (!d) {
      el.append(
        h('div.x-group', {}, 'Talento'),
        h(
          'p.ti-hint',
          {},
          ctx.promoting
            ? 'Pasa por encima de un nodo para ver qué hace. Los que tienen el aro encendido se aprenden ya: pulsa para añadir un nivel.'
            : 'Pasa por encima de un nodo para ver qué hace y qué le falta.',
        ),
      );
      return;
    }
    const st = stateOf(ctx, d);
    const where = d.axis ? `${TEMPLATES[d.axis].name} · ${BRANCHES[d.axis][d.branch].name} · fila ${d.row + 1}` : `${CENTER_NAME} · para todos`;
    const notes: HTMLElement[] = [];
    if (d.req) {
      const have = ctx.b.attrs[d.req[0]];
      notes.push(h(`li${have < d.req[1] ? '.miss' : '.ok'}`, {}, `Pide ${ATTRS[d.req[0]].name} ${d.req[1]} (tiene ${have}).`));
    }
    if (d.needs) notes.push(h(`li${ctx.b.talents[d.needs] ? '.ok' : '.miss'}`, {}, `Antes, ${TALENTS[d.needs].name}.`));
    if (d.kit && ctx.b.cls !== KIT_CLASS[d.kit]) notes.push(h('li.sleep', {}, `${st.sleeping ? 'Dormido' : 'Dormirá'} hasta llevar ${KIT_NAMES[d.kit]}.`));
    if (d.coop) notes.push(h('li.coop', {}, 'Llave doble: solo funciona junto a soldados de tu compañero.'));
    const becomes = wouldSpecialize(ctx.b, d.id);
    if (becomes && st.open) notes.push(h('li.ok', {}, `Con este punto se especializa como ${TEMPLATES[becomes].name}.`));
    // What happens on a click, or what is missing, in plain words.
    let status: HTMLElement;
    if (st.level >= d.max) status = h('p.ti-status.done', {}, st.level > 1 ? 'Aprendido al máximo.' : 'Aprendido.');
    else if (st.open) status = h('p.ti-status.open', {}, `Se puede aprender: +1 nivel (${st.level + 1}/${d.max}).`);
    else if (st.blocked && NO_POINTS.test(st.blocked) && d.axis) {
      const p = step(ctx, d.axis, d.branch, d.row);
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
          h('span.tnode-pips', {}, ...Array.from({ length: d.max }, (_, i) => h(`i${i < st.level - st.fresh ? '.on' : i < st.level ? '.plan' : ''}`)), h('small', {}, ` ${st.level}/${d.max}`)),
        ),
      ),
      h('p.ti-desc', {}, d.description),
      notes.length ? h('ul.ti-notes', {}, ...notes) : null,
      status,
      st.open || st.fresh
        ? h(
            'div.ti-actions',
            {},
            st.open ? h('button.x-btn.small.primary', { onclick: () => ctx.onAdd(d.id) }, 'Aprender +1') : null,
            st.fresh ? h('button.x-btn.small', { onclick: () => ctx.onRemove(d.id) }, 'Quitar') : null,
          )
        : null,
    );
  };
  return { el, show };
}

// ------------------------------------------------------------------ views

/** The axis buttons: each with its class, what it is to the soldier and the points in it. */
function axisBar(ctx: TreeContext): HTMLElement {
  return h(
    'div.tree-axes',
    { role: 'tablist' },
    ...AXES.map((a) => {
      const role = roleOf(ctx.b, a);
      const points = pointsInAxis(ctx.b.talents, a);
      return h(
        `button.tree-axis.role-${role}${!ctx.whole && a === ctx.axis ? '.on' : ''}`,
        { type: 'button', role: 'tab', 'aria-selected': String(!ctx.whole && a === ctx.axis), onclick: () => ctx.onAxis(a) },
        ctx.clsTag(a),
        h('b', {}, TEMPLATES[a].name),
        h('small', {}, [ROLE_TEXT[role], points ? plural(points, 'punto') : ''].filter(Boolean).join(' · ') || 'Sin empezar'),
      );
    }),
  );
}

/** Why the last two rows of this axis are closed, or that they are its own. */
function barText(ctx: TreeContext, axis: ClassId): string {
  if (axis === ctx.b.cls) return `Filas 4 y 5 · de su clase, ${TEMPLATES[axis].name}`;
  if (ctx.b.cls) return `Filas 4 y 5 · solo del eje principal (${TEMPLATES[ctx.b.cls].name})`;
  if (ctx.b.rank >= SPECIALIZE_RANK) return `Filas 4 y 5 · se abren al especializarse: con ${SPECIALIZE_POINTS} puntos en un eje (el Oficio no cuenta)`;
  return `Filas 4 y 5 · se abren en ${SPECIALIZE_RANK_NAME}, en el eje en que se especialice`;
}

/** One axis in detail: the root, its four branches as paths, the bar before the last two rows. */
function axisView(ctx: TreeContext, inspect: (d: TalentDef | null) => void): HTMLElement {
  const axis = ctx.axis;
  const role = roleOf(ctx.b, axis);
  const grid = h(`div.tree-grid${role === 'closed' ? '.closed' : ''}`);
  grid.append(
    h('div.tree-gutter'),
    h('div.tree-root', {}, h('span.tree-root-name', {}, h('b', {}, CENTER_NAME), h('small', {}, 'Para todos')), h('div.tree-root-nodes', {}, ...CENTER.map((d) => nodeEl(ctx, d, inspect)))),
    h('div.tree-gutter'),
  );
  BRANCHES[axis].forEach((br, i) => {
    const focus = br.song ? `Oficio · 1 punto por fila · ${ctx.echoes ? `Taller nivel ${ctx.echoes}` : 'falta el Taller'}` : br.focus;
    grid.append(
      h(
        `div.tree-branch.b${i}${br.song ? '.song' : ''}`,
        {},
        h('div.tree-branch-name', {}, disc(BRANCH_ICON[axis][i], '.emblem'), h('b', {}, br.name), h('small.tree-branch-points', {}, plural(pointsInBranch(ctx.b.talents, axis, i), 'punto'))),
        h('small.tree-branch-focus', { title: focus }, focus),
      ),
    );
  });
  ROW_GATES.forEach((_, row) => {
    if (row === SECOND_AXIS_ROWS) grid.append(h(`div.tree-bar${axis === ctx.b.cls ? '.own' : ''}`, {}, h('span', {}, barText(ctx, axis))));
    const gate = ROW_GATES[row]!;
    grid.append(
      h(
        'div.tree-gutter',
        { title: row ? `Fila ${row + 1}: pide ${gate} puntos en la rama, en las filas de arriba (en el Oficio, uno por fila)` : 'Fila 1: abierta' },
        h('b', {}, String(row + 1)),
        h('small', {}, row ? `pide ${gate}` : 'fila'),
      ),
    );
    for (const branch of [0, 1, 2, 3]) grid.append(stepEl(ctx, axis, branch, row, inspect));
  });
  const closedNote =
    role === 'closed'
      ? h('p.tree-note', {}, `Eje cerrado: ${ctx.b.cls ? `aprende ya de ${[ctx.b.cls, ...axesOf(ctx.b).open].map((a) => TEMPLATES[a!].name).join(' y ')}` : `un recluta aprende de dos ejes como mucho, y ya tiene ${axesOf(ctx.b).open.map((a) => TEMPLATES[a].name).join(' y ')}`}.`)
      : null;
  return h('div.tree-axis-view', {}, closedNote, grid);
}

/** The four axes around the centre, every node small: the map to plan far ahead. Click to open an axis. */
function wholeView(ctx: TreeContext, inspect: (d: TalentDef | null) => void): HTMLElement {
  const quarter = (axis: ClassId, top: boolean) => {
    const role = roleOf(ctx.b, axis);
    const body = h('div.tw-body');
    BRANCHES[axis].forEach((br, i) => {
      const col = h(`div.tw-branch.b${i}${br.song ? '.song' : ''}`, {}, h('small.tw-branch-name', {}, br.name));
      ROW_GATES.forEach((_, row) => col.append(stepEl(ctx, axis, i, row, inspect, true, axis)));
      body.append(col);
    });
    return h(
      `div.tw-quarter.${top ? 'tw-up' : 'tw-down'}.role-${role}${axis === ctx.axis ? '.last-open' : ''}`,
      {},
      h(
        'button.tw-head',
        { type: 'button', onclick: () => ctx.onZoom(axis, null), title: `Abrir el eje de ${TEMPLATES[axis].name}` },
        ctx.clsTag(axis),
        h('b', {}, TEMPLATES[axis].name),
        h('small', {}, [ROLE_TEXT[role], plural(pointsInAxis(ctx.b.talents, axis), 'punto')].filter(Boolean).join(' · ')),
      ),
      body,
    );
  };
  const [a0, a1, a2, a3] = AXES as [ClassId, ClassId, ClassId, ClassId];
  return h(
    'div.tree-whole',
    {},
    quarter(a0, true),
    quarter(a1, true),
    h('div.tw-centre', {}, h('b', {}, CENTER_NAME), h('div.tw-centre-nodes', {}, ...CENTER.map((d) => nodeEl(ctx, d, inspect, true))), h('small', {}, 'Pulsa un eje o un nodo para verlo de cerca')),
    // Clockwise: the third axis sits under the second.
    quarter(a3, false),
    quarter(a2, false),
  );
}

/** The talents part of the sheet, and the inspector that goes next to the aptitudes. */
export function skillTree(ctx: TreeContext): { tree: HTMLElement; inspect: HTMLElement } {
  const insp = inspector(ctx);
  const focused = ctx.focus ? (TALENTS[ctx.focus] ?? null) : null;
  insp.show(focused);
  const tree = h(
    `div.skill-tree${ctx.whole ? '.whole' : ''}`,
    { onmouseleave: () => insp.show(focused) },
    axisBar(ctx),
    ctx.whole ? wholeView(ctx, insp.show) : axisView(ctx, insp.show),
  );
  tree.append(
    h(
      'p.tree-legend',
      {},
      h('span.lg.open', {}, h('i'), 'Se puede aprender'),
      h('span.lg.taken', {}, h('i'), 'Aprendido'),
      h('span.lg.plan', {}, h('i'), 'En este ascenso'),
      h('span.lg.shut', {}, h('i'), 'Cerrado'),
      h('span.lg.ab', {}, h('i'), 'Habilidad (rombo)'),
      h('span.lg.sleep', {}, h('i'), 'Dormido: falta el equipo de su clase'),
    ),
  );
  return { tree, inspect: insp.el };
}
