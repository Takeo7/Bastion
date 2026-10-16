import {
  baseWeaponProfile,
  FOUND_WEAPONS,
  foundWeaponOf,
  MOD_SLOTS,
  meleeWeaponOf,
  modBlocker,
  REACH,
  REPAIR_PER_PART,
  repairBlocker,
  repairParts,
  WEAPON_MODS,
  WEAPON_LABELS,
  weaponFigure,
  weaponLabel,
  weaponProfile,
  WEAPONS,
  WORN_AIM,
  WORN_BELOW,
  type CampaignCommand,
  type CampaignSoldier,
  type CampaignState,
  type FoundWeapon,
  type FoundWeaponId,
  type Slot,
  type WeaponId,
  type WeaponModId,
  type WeaponProfile,
} from '@bastion/engine';
import { h } from '../ui/dom';

// What a soldier carries (docs/FASE8.md §A), drawn for the Armería and the
// Cuartel: each weapon with its silhouette (the packs' icons, by the kind of
// weapon it fires: strategy.css and ui/themes/mixta-base.css `.wpn`), and a
// soldier's arms as two places, the hand and the belt. In the hand, a weapon
// of the Armería or, without one, the pistol; at the belt, the machete or the
// knife. The pistol goes along whenever the hand holds something else.
//
// Fase 9 (docs/FASE9.md §5): a firearm of the Armería wears down as it fires
// (its condition, 0–100: «Gastada» below WORN_BELOW, «Rota» at 0), is mended
// in the Armería with parts, and takes two improvements (WEAPON_MODS), which
// go on and come off there. Every rule is the engine's: repairBlocker,
// modBlocker, repairParts.

/** The silhouette of a kind of weapon, of the munition or of an improvement. */
export function weaponIcon(kind: WeaponId | 'ammo' | 'mod'): HTMLElement {
  return h('span.wpn', { 'data-weapon': kind, 'aria-hidden': 'true' });
}

/** Where a weapon of the Armería goes: the hand or the belt. */
export function placeOf(id: FoundWeaponId): string {
  return FOUND_WEAPONS[id].slot === 'melee' ? 'Cinto' : 'Mano';
}

/** A weapon of the Armería's kind of weapon, for its silhouette. */
export function kindOf(id: FoundWeaponId): WeaponId {
  return FOUND_WEAPONS[id].weapon;
}

// ---------------------------------------------------------------- condition

type Wear = 'ok' | 'worn' | 'broken';

const wearOf = (condition: number): Wear => (condition <= 0 ? 'broken' : condition < WORN_BELOW ? 'worn' : 'ok');

/** Lore's lines for a weapon's state (docs/FASE9.md, «Las armas»). */
const WEAR_LINE: Record<Wear, string> = {
  ok: '',
  worn: `Gastada: −${WORN_AIM} de puntería. Pide piezas.`,
  broken: 'Rota: no dispara. Se arregla en la Armería.',
};

/** A weapon's condition: a gauge and «Estado 64», or «Gastada · 22», or «Rota». */
export function conditionEl(condition = 100): HTMLElement {
  const wear = wearOf(condition);
  return h(
    `span.wcond.${wear}`,
    { title: WEAR_LINE[wear] || `Estado ${condition} de 100` },
    h('i', { style: { '--p': (Math.max(0, condition) / 100).toFixed(2) } }),
    h('small', {}, wear === 'broken' ? 'Rota' : wear === 'worn' ? `Gastada · ${condition}` : `Estado ${condition}`),
  );
}

/** The improvements mounted on a weapon, as small tags, their line on hover. */
export function modTags(mods: WeaponModId[] | undefined): HTMLElement | null {
  if (!mods?.length) return null;
  return h('span.wmods', {}, ...mods.map((m) => h('span.wmod', { title: WEAPON_MODS[m].line }, WEAPON_MODS[m].name)));
}

/**
 * What a firearm says besides its name where there is little room (the
 * Armería's list, the combat sheet): «Gastada» or «Rota» when it is, and «+2»
 * for its improvements, their names on hover. Nothing for a sound one with none.
 */
export function wearMarks(condition = 100, mods: readonly WeaponModId[] = []): HTMLElement | null {
  const wear = wearOf(condition);
  if (wear === 'ok' && !mods.length) return null;
  return h(
    'span.wmarks',
    {},
    wear !== 'ok' ? h(`small.wwear.${wear}`, { title: `${WEAR_LINE[wear]} (estado ${Math.max(0, condition)})` }, wear === 'broken' ? 'Rota' : 'Gastada') : null,
    mods.length ? h('small.wmod-n', { title: mods.map((m) => `${WEAPON_MODS[m].name}: ${WEAPON_MODS[m].line}`).join('\n') }, `+${mods.length}`) : null,
  );
}

/** wearMarks for a weapon of the Armería (only firearms wear and take improvements). */
export function weaponMarks(w: FoundWeapon): HTMLElement | null {
  return FOUND_WEAPONS[w.kind].slot === 'primary' ? wearMarks(w.condition, w.mods) : null;
}

// ---------------------------------------------------------------- what it does

/**
 * A weapon's figures (aim.ts weaponProfile: Arturo, 17/10, «para saber qué
 * elegir»), for the pistol and the knife too, which aren't the Armería's.
 */
export type Profile = Omit<WeaponProfile, 'kind'>;

/** The pistol's or the knife's figures (aim.ts baseWeaponProfile): what a soldier carries without a weapon of the Armería. */
const baseProfile = (id: 'pistol' | 'knife'): Profile => baseWeaponProfile(id);

/** A weapon of the Armería as it is: its wear and its improvements. */
export const profileOf = (w: FoundWeapon): Profile => weaponProfile(w.kind, w.condition, w.mods ?? []);

/** What a soldier carries in the hand (or at the belt, `melee`): a weapon of the Armería, or the pistol or the knife. */
export function carriedProfile(c: CampaignState, s: CampaignSoldier, melee: boolean): Profile {
  const w = melee ? meleeWeaponOf(c, s) : foundWeaponOf(c, s);
  return w ? profileOf(w) : baseProfile(melee ? 'knife' : 'pistol');
}

const signed = (n: number) => (n > 0 ? `+${n}` : n < 0 ? `−${-n}` : '±0');
const span = (d: { min: number; max: number }) => (d.min === d.max ? String(d.min) : `${d.min}–${d.max}`);
const mean = (d: { min: number; max: number }) => (d.min + d.max) / 2;
/** Mean aim at the near distances (2 and 5 tiles) and the far ones (12 and 16). */
const near = (p: Profile) => (p.range.length ? (p.range[0]!.aim + p.range[1]!.aim) / 2 : 0);
const far = (p: Profile) => (p.range.length ? (p.range[3]!.aim + p.range[4]!.aim) / 2 : 0);

interface Figure {
  label: string;
  value: string;
  title?: string;
  /** A number to compare, and whether more is better; none: not compared. */
  n?: number;
  more?: boolean;
  /** How a difference reads after the sign (« %» for the critical); none for damage, which shows its new span. */
  unit?: string;
}

/** A weapon's figures, in order, in Lore's words (lore.ts WEAPON_LABELS, weaponFigure): damage, critical damage and chance, aim, magazine, actions, noise and the armour it takes off. */
function figures(p: Profile): Figure[] {
  const L = WEAPON_LABELS;
  const out: Figure[] = [
    { label: L.damage, value: span(p.damage), n: mean(p.damage), more: true },
    { label: L.critDamage, value: span(p.critDamage), n: mean(p.critDamage), more: true },
    { label: L.crit, value: `${signed(p.crit)} %`, n: p.crit, more: true, unit: ' %' },
    { label: L.aim, value: signed(p.aim), title: 'Lo que el arma suma o resta a la puntería: la mira, el desgaste', n: p.aim, more: true, unit: '' },
    // No magazine: «No gasta» (a blade, the pistol); not compared with one that has one.
    p.clip && !p.melee ? { label: L.clip, value: weaponFigure('clip', p.clip), n: p.clip, more: true, unit: '' } : { label: L.clip, value: weaponFigure('clip', 0, p.melee) },
    { label: p.melee ? L.actionsMelee : L.actions, value: weaponFigure('actions', p.actions), n: p.actions, more: false, unit: '' },
    { label: L.noise, value: weaponFigure('noise', p.noise), n: p.noise, more: false, unit: '' },
  ];
  if (p.shred) out.push({ label: L.shred, value: String(p.shred), n: p.shred, more: true, unit: '' });
  return out;
}

/** Better or worse than `vs` (a class), or nothing when the same or not comparable. */
function versus(f: Figure, vs: Figure | undefined): '' | '.up' | '.down' {
  if (f.n === undefined || vs?.n === undefined || f.n === vs.n) return '';
  return f.n > vs.n === f.more ? '.up' : '.down';
}

/**
 * The aim a firearm adds or takes at each distance, as bars over and under a
 * line (more aim, up), its reach's word over them: «Corto», «Medio», «Largo».
 * With `vs`, each distance where it aims better or worse is marked so.
 */
export function rangeChart(p: Profile, vs?: Profile): HTMLElement | null {
  if (p.melee || !p.range.length) return null;
  const MAX = 40;
  return h(
    'div.wrange',
    { title: WEAPON_LABELS.range },
    h('div.wrange-head', {}, h('small', {}, WEAPON_LABELS.reach), h('b', {}, REACH[p.reach].name)),
    h(
      'div.wrange-bars',
      {},
      ...p.range.map((r, i) => {
        const other = vs && !vs.melee ? vs.range[i]?.aim : undefined;
        const tone = other === undefined || other === r.aim ? '' : r.aim > other ? '.up' : '.down';
        const k = Math.min(1, Math.abs(r.aim) / MAX);
        return h(
          `div.wrange-col${tone}`,
          { title: `${weaponFigure('noise', r.tiles)} · ${signed(r.aim)}${other === undefined ? '' : ` (ahora ${signed(other)})`}` },
          h('small.wrange-n', {}, signed(r.aim)),
          h('span.wrange-track', {}, h(`i${r.aim < 0 ? '.neg' : ''}`, { style: { '--k': k.toFixed(2) } })),
          h('small.wrange-d', {}, String(r.tiles)),
        );
      }),
    ),
    h('small.wrange-unit', {}, 'casillas'),
    h('small.wrange-line', {}, REACH[p.reach].line),
  );
}

/** A weapon's card for the Armería: its figures and its reach; with `vs` (what a soldier carries now), each better in green, worse in red. */
export function weaponStats(p: Profile, vs?: Profile): HTMLElement {
  const theirs = vs ? figures(vs) : [];
  return h(
    'div.wstats',
    {},
    h(
      'dl.wfigs',
      {},
      ...figures(p).flatMap((f) => {
        const tone = versus(f, theirs.find((x) => x.label === f.label));
        return [h('dt', {}, f.label), h(`dd${tone}`, { title: f.title }, f.value)];
      }),
    ),
    rangeChart(p, vs),
  );
}

/**
 * What changes for a soldier from what it carries to `p`, in a line: each
 * figure that changes by how much (damage, its new span), green if better,
 * red if worse, and near and far aim for firearms. «Igual» when nothing does.
 */
export function weaponDiff(p: Profile, vs: Profile): HTMLElement {
  const theirs = figures(vs);
  const out: HTMLElement[] = [];
  for (const f of figures(p)) {
    const other = theirs.find((x) => x.label === f.label);
    const tone = versus(f, other);
    if (tone) out.push(h(`span${tone}`, { title: `${f.label}: ${other!.value} → ${f.value}` }, `${f.label} ${f.unit === undefined ? f.value : `${signed(f.n! - other!.n!)}${f.unit}`}`));
  }
  if (!p.melee && !vs.melee) {
    for (const [label, a, b] of [['De cerca', near(p), near(vs)], ['De lejos', far(p), far(vs)]] as const) {
      if (Math.round(a) !== Math.round(b)) out.push(h(`span${a > b ? '.up' : '.down'}`, { title: `Puntería ${label.toLowerCase()}: ${signed(Math.round(b))} → ${signed(Math.round(a))}` }, `${label} ${signed(Math.round(a - b))}`));
    }
  }
  return h('span.wdiff', {}, ...(out.length ? out : [h('span', {}, 'Igual')]));
}

/** A weapon in a line, where there is little room (the Cuartel's sheet): its damage and, for a firearm, its reach. */
export function weaponLine(p: Profile): HTMLElement {
  return h(
    'small.wline',
    { title: figures(p).map((f) => `${f.label}: ${f.value}`).join(' · ') },
    `${WEAPON_LABELS.damage} ${span(p.damage)}`,
    p.melee ? null : ` · ${WEAPON_LABELS.reach} ${REACH[p.reach].name.toLowerCase()}`,
  );
}

// ---------------------------------------------------------------- the soldier

/** A soldier's arms as the hand and the belt, each with its silhouette and its name; the hand's state and improvements under it. */
export function loadoutEl(c: CampaignState, s: CampaignSoldier): HTMLElement {
  const main = foundWeaponOf(c, s);
  const melee = meleeWeaponOf(c, s);
  const cell = (where: string, kind: WeaponId, name: string, ...notes: (HTMLElement | string | null)[]) =>
    h('div.ld-cell', {}, h('small.ld-where', {}, where), weaponIcon(kind), h('b', {}, name), ...notes.map((n) => (typeof n === 'string' ? h('small.ld-note', {}, n) : n)));
  return h(
    'div.x-loadout',
    {},
    cell('Mano', main ? kindOf(main.kind) : 'pistol', weaponLabel(c, s), main ? 'y la pistola' : 'sin arma: la pistola', weaponLine(carriedProfile(c, s, false)), main ? conditionEl(main.condition) : null, main ? modTags(main.mods) : null),
    cell('Cinto', melee ? kindOf(melee.kind) : 'knife', melee ? FOUND_WEAPONS[melee.kind].name : WEAPONS.knife.name, weaponLine(carriedProfile(c, s, true))),
  );
}

/** What the Cuartel's sheet gives a weapon picker: the screen keeps which is open and sends the orders. */
export interface WeaponPickContext {
  c: CampaignState;
  s: CampaignSoldier;
  mySlot: Slot;
  open: boolean;
  toggle: () => void;
  /** The campaign's `assignWeapon`: this weapon to the soldier, or (`soldier` null) back to the Armería. */
  send: (cmd: CampaignCommand) => void;
  ownerName: (slot: Slot) => string;
}

/**
 * A soldier's hand or belt in the Cuartel as a picker (Arturo, 17/10: «como
 * los accesorios, asignar las armas en el mismo soldado»), like the belt's
 * places: the Armería's weapons for that place that are free or carried by
 * one's own soldiers (taking it from them), and none (the pistol, the knife);
 * a partner's soldier's, off with why. Each with its state, its improvements
 * and, against what it carries now, what is better in green and worse in red.
 * Every rule is the engine's (`assignWeapon`).
 */
export function weaponPick(place: 'hand' | 'belt', ctx: WeaponPickContext): HTMLElement {
  const { c, s } = ctx;
  const melee = place === 'belt';
  const current = melee ? meleeWeaponOf(c, s) : foundWeaponOf(c, s);
  const now = carriedProfile(c, s, melee);
  const none = melee ? WEAPONS.knife.name : WEAPONS.pistol.name;
  const pick = (cmd: CampaignCommand | null) => {
    ctx.toggle();
    if (cmd) ctx.send(cmd);
  };
  const holderOf = (w: FoundWeapon) => (w.holder ? c.soldiers.find((x) => x.id === w.holder) : undefined);
  // Free ones first, then one's own soldiers', then the partner's (off).
  const rank = (w: FoundWeapon) => (!w.holder ? 0 : holderOf(w)?.owner === ctx.mySlot ? 1 : 2);
  const options = (c.foundWeapons ?? []).filter((w) => w.id !== current?.id && (FOUND_WEAPONS[w.kind].slot === 'melee') === melee).sort((a, b) => rank(a) - rank(b));
  const option = (w: FoundWeapon) => {
    const holder = holderOf(w);
    const theirs = holder && holder.owner !== ctx.mySlot;
    const note = !holder ? 'En la Armería' : theirs ? `La lleva ${holder.name}, de ${ctx.ownerName(holder.owner)}` : `La lleva ${holder.name}: se la quitas`;
    return h(
      'button.wpick',
      { disabled: !!theirs, title: theirs ? note : undefined, onclick: () => pick({ type: 'assignWeapon', weapon: w.id, soldier: s.id }) },
      h('span.wpick-name', {}, weaponIcon(kindOf(w.kind)), FOUND_WEAPONS[w.kind].name, weaponMarks(w)),
      h('small.wpick-note', {}, note),
      theirs ? null : weaponDiff(profileOf(w), now),
    );
  };
  return h(
    'div.x-pick.wpick-host',
    {},
    h(
      'button.x-pick-field',
      { onclick: ctx.toggle },
      h('small', {}, melee ? 'Cinto' : 'Mano'),
      h('b', {}, current ? FOUND_WEAPONS[current.kind].name : none),
    ),
    // What it carries, under its name: damage and reach, its state, its improvements.
    h('div.wpick-now', {}, weaponLine(now), current && !melee ? conditionEl(current.condition) : null, current && !melee ? modTags(current.mods) : null, melee ? null : h('small.ld-note', {}, current ? 'y la pistola al cinto' : 'sin arma: la pistola')),
    ctx.open
      ? h(
          'div.x-pick-list',
          {},
          h(
            `button.wpick${current ? '' : '.on'}`,
            { onclick: () => pick(current ? { type: 'assignWeapon', weapon: current.id, soldier: null } : null) },
            h('span.wpick-name', {}, weaponIcon(melee ? 'knife' : 'pistol'), melee ? 'Sin arma: el cuchillo' : 'Sin arma: la pistola'),
            current ? h('small.wpick-note', {}, `${FOUND_WEAPONS[current.kind].name} vuelve a la Armería`) : null,
            current ? weaponDiff(baseProfile(melee ? 'knife' : 'pistol'), now) : null,
          ),
          ...options.map(option),
          options.length ? null : h('small.wpick-empty', {}, melee ? 'No hay armas de cuerpo a cuerpo en la Armería.' : 'No hay armas en la Armería.'),
        )
      : null,
  );
}

// ---------------------------------------------------------------- the Armería

/**
 * A firearm's care, for its panel in the Armería: its state and «Arreglar»
 * (one part every REPAIR_PER_PART, as far as the parts go), and its two places
 * for improvements, each with what is mounted and «Quitar», or what there is
 * loose to put on.
 */
export function weaponCare(c: CampaignState, slot: Slot, w: FoundWeapon, send: (cmd: CampaignCommand) => void): HTMLElement[] {
  if (FOUND_WEAPONS[w.kind].slot !== 'primary') return [];
  const condition = w.condition ?? 100;
  const wear = wearOf(condition);
  const missing = repairParts(w);
  const have = c.parts ?? 0;
  const repairWhy = repairBlocker(c, slot, w.id);
  const out: HTMLElement[] = [
    h('div.x-group', {}, 'Estado'),
    h('div.wcare-state', {}, conditionEl(condition), WEAR_LINE[wear] ? h(`p.x-text.wcare-line.${wear}`, {}, WEAR_LINE[wear]) : null),
  ];
  if (missing) {
    const parts = Math.min(missing, have);
    out.push(
      h(
        'div.wcare-repair',
        {},
        h(
          'button.x-btn.primary.small',
          { disabled: !!repairWhy, title: repairWhy ?? '', onclick: () => send({ type: 'repair', weapon: w.id }) },
          `Arreglar: 1 pieza cada ${REPAIR_PER_PART}`,
        ),
        h(
          'small.x-muted',
          {},
          repairWhy && repairWhy !== 'No hay piezas.'
            ? repairWhy
            : `Como nueva, ${missing} ${missing === 1 ? 'pieza' : 'piezas'}; tenéis ${have}.${have && have < missing ? ` Con ${parts} llega a ${Math.min(100, condition + parts * REPAIR_PER_PART)}.` : ''}`,
        ),
      ),
    );
  } else out.push(h('p.x-muted', {}, 'Está como nueva.'));

  // The improvements: what is on, then the free places with what could go in them.
  const mounted = w.mods ?? [];
  const loose = (Object.keys(WEAPON_MODS) as WeaponModId[]).filter((m) => (c.weaponMods?.[m] ?? 0) > 0 && !mounted.includes(m));
  out.push(h('div.x-group', {}, `Mejoras · ${mounted.length}/${MOD_SLOTS}`));
  for (const m of mounted) {
    const why = modBlocker(c, slot, w.id, m, false);
    out.push(
      h(
        'div.x-gear.wmod-row',
        {},
        h('span', {}, h('b', {}, WEAPON_MODS[m].name), h('small', {}, ` · ${WEAPON_MODS[m].line}`)),
        h('button.x-btn.small', { disabled: !!why, title: why ?? 'Vuelve a la Armería, suelta', onclick: () => send({ type: 'mod', weapon: w.id, mod: m, on: false }) }, 'Quitar'),
      ),
    );
  }
  for (let i = mounted.length; i < MOD_SLOTS; i++) {
    out.push(
      h(
        'div.wmod-free',
        {},
        h('small.wmod-slot', {}, 'Hueco libre'),
        loose.length
          ? h(
              'span.wmod-offer',
              {},
              ...loose.map((m) => {
                const why = modBlocker(c, slot, w.id, m, true);
                return h(
                  'button.x-btn.small',
                  { disabled: !!why, title: why ?? WEAPON_MODS[m].line, onclick: () => send({ type: 'mod', weapon: w.id, mod: m, on: true }) },
                  `Poner ${WEAPON_MODS[m].name.toLowerCase()} (${c.weaponMods![m]})`,
                );
              }),
            )
          : h('small.x-muted', {}, 'No hay mejoras sueltas: salen en cajas y habitaciones, o del Taller.'),
      ),
    );
    // One line of choices is enough: they are the same for every free place.
    if (loose.length) break;
  }
  return out;
}

/**
 * An improvement loose in the Armería: what it does, how many there are, and
 * your firearms, each with what it carries and «Ponérsela».
 */
export function modPanel(c: CampaignState, slot: Slot, mod: WeaponModId, send: (cmd: CampaignCommand) => void, name: (s: CampaignSoldier) => string): HTMLElement[] {
  const def = WEAPON_MODS[mod];
  const count = c.weaponMods?.[mod] ?? 0;
  const guns = (c.foundWeapons ?? []).filter((w) => FOUND_WEAPONS[w.kind].slot === 'primary');
  return [
    h('div.alert-kicker', {}, 'MEJORA · SUELTA'),
    h('h2.x-title', {}, def.name),
    h('p.x-text', {}, def.line),
    h('p.x-chips', {}, h('span.x-chip', {}, count === 1 ? 'Tenéis una' : `Tenéis ${count}`)),
    h('div.x-group', {}, 'Ponérsela a'),
    guns.length
      ? h(
          'div.x-list',
          {},
          ...guns.map((w) => {
            const holder = w.holder ? c.soldiers.find((s) => s.id === w.holder) : undefined;
            const why = modBlocker(c, slot, w.id, mod, true);
            return h(
              'div.x-gear',
              {},
              h('span', {}, weaponIcon(kindOf(w.kind)), FOUND_WEAPONS[w.kind].name, h('small', {}, ` · ${holder ? name(holder) : 'en la Armería'}`), modTags(w.mods)),
              h('button.x-btn.small', { disabled: !!why, title: why ?? '', onclick: () => send({ type: 'mod', weapon: w.id, mod, on: true }) }, 'Ponérsela'),
            );
          }),
        )
      : h('p.x-muted', {}, 'No hay armas de fuego en la Armería.'),
  ];
}
