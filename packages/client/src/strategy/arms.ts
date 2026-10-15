import { FOUND_WEAPONS, foundWeaponOf, meleeWeaponOf, weaponLabel, WEAPONS, type CampaignSoldier, type CampaignState, type FoundWeaponId, type WeaponId } from '@bastion/engine';
import { h } from '../ui/dom';

// What a soldier carries (docs/FASE8.md §A), drawn for the Armería and the
// Cuartel: each weapon with its silhouette (the packs' icons, by the kind of
// weapon it fires: strategy.css and ui/themes/mixta-base.css `.wpn`), and a
// soldier's arms as two places, the hand and the belt. In the hand, a weapon
// of the Armería or, without one, the pistol; at the belt, the machete or the
// knife. The pistol goes along whenever the hand holds something else.

/** The silhouette of a kind of weapon, or of the munition. */
export function weaponIcon(kind: WeaponId | 'ammo'): HTMLElement {
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

/** A soldier's arms as the hand and the belt, each with its silhouette and its name. */
export function loadoutEl(c: CampaignState, s: CampaignSoldier): HTMLElement {
  const main = foundWeaponOf(c, s);
  const melee = meleeWeaponOf(c, s);
  const cell = (where: string, kind: WeaponId, name: string, note: string | null) =>
    h('div.ld-cell', {}, h('small.ld-where', {}, where), weaponIcon(kind), h('b', {}, name), note ? h('small.ld-note', {}, note) : null);
  return h(
    'div.x-loadout',
    {},
    cell('Mano', main ? kindOf(main.kind) : 'pistol', weaponLabel(c, s), main ? 'y la pistola' : 'sin arma: la pistola'),
    cell('Cinto', melee ? kindOf(melee.kind) : 'knife', melee ? FOUND_WEAPONS[melee.kind].name : WEAPONS.knife.name, null),
  );
}
