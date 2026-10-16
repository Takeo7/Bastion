import {
  FOUND_WEAPONS,
  foundWeaponOf,
  MOD_SLOTS,
  meleeWeaponOf,
  modBlocker,
  REPAIR_PER_PART,
  repairBlocker,
  repairParts,
  WEAPON_MODS,
  weaponLabel,
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
    cell('Mano', main ? kindOf(main.kind) : 'pistol', weaponLabel(c, s), main ? 'y la pistola' : 'sin arma: la pistola', main ? conditionEl(main.condition) : null, main ? modTags(main.mods) : null),
    cell('Cinto', melee ? kindOf(melee.kind) : 'knife', melee ? FOUND_WEAPONS[melee.kind].name : WEAPONS.knife.name),
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
