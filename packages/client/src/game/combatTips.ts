// Morse's first-time warnings in combat (UX-REVISION-2 #30): this decides when
// each of Lore's RADIO_TIPS is due, from the events as they are animated and the
// state on screen (`view`). RadioTips plays them and remembers, per browser,
// which have been heard; each id is only worked out until it has been asked for.
import { ABILITIES, abilitiesOf, attackWeapon, coverAgainst, getUnit, type AttackAbility, type GameEvent, type GameState, type Slot, type TipId, type Unit } from '@bastion/engine';
import { RadioTips } from '../strategy/transmissions';

/**
 * Whether `a` shoots: an attack on an enemy with a weapon that isn't for close quarters. Only then
 * does cover count against it: a bite or a blow ignores cover and never flanks (aim.ts), and most
 * enemies now are the dead (docs/ZOMBIS-PRIMERO.md). The Chillón screams and the Hinchado bursts.
 */
export function shootsFromRange(a: Unit): boolean {
  return abilitiesOf(a).some((id) => ABILITIES[id].target === 'enemy' && !!ABILITIES[id].weapon && !attackWeapon(a, id as AttackAbility).melee);
}

export class CombatTips {
  private readonly radio: RadioTips;
  /** Ids already handed to the radio (played, queued or heard before): not worked out again. */
  private readonly asked = new Set<TipId>();

  constructor(container: HTMLElement, private readonly mySlot: Slot) {
    this.radio = new RadioTips(container);
  }

  /** The mission briefing is on screen until `closed`: nothing plays over it. */
  intro(closed: Promise<void>): void {
    this.radio.holdUntil(closed);
  }

  /** `e` has just been animated; `view` is the state after it. */
  event(e: GameEvent, view: GameState): void {
    switch (e.t) {
      case 'podActivated':
        this.show('contact');
        return;
      case 'damaged': {
        // Only a soldier still standing: "herido no es caído" over a fallen one would jar.
        const u = getUnit(view, e.unit);
        if (e.amount && u && this.mine(u) && u.alive && u.hp > 0) this.show('wounded');
        return;
      }
      case 'psi': {
        const u = getUnit(view, e.target);
        if (u && this.mine(u)) this.show('psionic');
        return;
      }
      // The Saqueo's horde comes in by the edges, not under a flare.
      case 'reinforcementsIncoming':
        if (e.r.horde) this.show('horde');
        else if (e.r.pos) this.show('flare');
        return;
      // The first room or box of mine (docs/FASE8.md §B: the tip speaks of both).
      case 'roomSearched':
      case 'searched': {
        const u = getUnit(view, e.unit);
        if (u && this.mine(u)) this.show('search');
        // The first good weapon found (docs/FASE4.md), whoever found it.
        if (e.loot?.weapon) this.show('foundWeapon');
        return;
      }
      // docs/FASE3.md: the first of mine bitten, and the first of mine who rises (no Chillón raised it).
      case 'infected': {
        const u = getUnit(view, e.unit);
        if (u && this.mine(u)) this.show('bitten');
        return;
      }
      case 'reanimated': {
        const corpse = getUnit(view, e.corpse);
        if (!e.unit && corpse && this.mine(corpse)) this.show('risen');
        return;
      }
      // docs/FASE7.md: the first of mine left without spares after reloading (`view` has it spent).
      case 'reload': {
        const u = getUnit(view, e.unit);
        if (u && this.mine(u) && u.mags === 0) this.show('noMags');
        return;
      }
      // The Oficio: the first trade one of mine uses (noisy).
      case 'sung': {
        const u = getUnit(view, e.unit);
        if (u && this.mine(u)) this.show('song');
        return;
      }
    }
  }

  /**
   * The state on screen has changed. `sharing`: a partner is here and it is the
   * squad's turn, so the command matters. A soldier of mine is flanked when a
   * visible enemy has no cover to beat, the same test as the red shield on its label.
   */
  state(view: GameState, visibleAliens: ReadonlySet<string>, sharing: boolean): void {
    if (sharing) this.show('command');
    // docs/FASE6.md: the first time one of mine stands on a storey (climbing the stairs leads there).
    if (!this.asked.has('storeys') && view.units.some((u) => u.alive && this.mine(u) && u.pos.z >= 1)) this.show('storeys');
    const watchers = view.units.filter((a) => a.alive && a.team === 'alien' && visibleAliens.has(a.id));
    if (!watchers.length) return;
    // The first Hinchado in sight: keep it away from the squad.
    if (watchers.some((a) => a.template === 'bloater')) this.show('bloater');
    if (this.asked.has('flanked')) return;
    // Only against those that shoot: a bite ignores cover (docs/ZOMBIS-PRIMERO.md), and the lesson would come with the first zombie.
    const shooters = watchers.filter(shootsFromRange);
    if (!shooters.length) return;
    const flanked = view.units.some((u) => u.alive && this.mine(u) && !u.evacuated && Math.min(...shooters.map((a) => coverAgainst(view, u.pos, a.pos))) === 0);
    if (flanked) this.show('flanked');
  }

  /**
   * A night mission begins (docs/ALTURA-Y-NOCHE.md): the first one gets Lore's
   * warning. False when it won't play (heard before), so the caller can say
   * its own line instead.
   */
  night(): boolean {
    if (this.asked.has('night')) return false;
    this.asked.add('night');
    return this.radio.show('night');
  }

  /** «Os han oído» has just shown: enemies the squad can't see are coming to look. */
  heard(): void {
    this.show('heard');
  }

  /** The odds panel of an attack is open. */
  shotPanel(): void {
    this.show('shot');
  }

  /** A story chapter's radio line (STORY_RADIO), played every time; `read` once the player has closed it. */
  story(line: string, read?: () => void): void {
    this.radio.say(line, read);
  }

  destroy(): void {
    this.radio.destroy();
  }

  private show(id: TipId): void {
    if (this.asked.has(id)) return;
    this.asked.add(id);
    this.radio.show(id);
  }

  private mine(u: Unit): boolean {
    return u.team === 'xcom' && (u.owner === this.mySlot || u.owner === null);
  }
}
