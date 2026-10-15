import { COMBAT_RATES } from '../../../settings';
import { combatShot, unitSpot, v3 } from '../sala';

// Shot 7 (docs/TRAILER.md, 0:56–1:06): the evacuation. The horde comes up from the city towards La Lata's zone;
// one player asks to evacuate from the bus, the other accepts, La Lata pulls away, and one is left outside with the
// dead coming at them. Gameplay's plan «evacuacion»: the zone in the north-east corner (45–47, 0–2); the dead, one
// group, walk up from the south in the enemy turn (both players end theirs); in the next, one walks into the zone,
// the call, the yes. All the orders at once: the game plays them in turn, as they come.
// Measured (the plan's own time): the dead walk from 3.0 s to 8.5 s; the one into the zone 9.7–11; the call at 11,
// the yes 11.7, the boarding to 14.8, then La Lata goes. The take begins as the horde sets off (3.6 s), a touch
// quicker than the game (×1.15), and once it is in the rest plays at the game's «rápida» speed: the call and the
// yes at about 5.5 s of the take, La Lata away by 7.5, the one left outside alone after: UI's line under it.

export default combatShot(
  'evacuacion',
  10,
  (_focus, battle) => {
    const left = unitSpot(battle, 's4');
    return [
      // Low beyond the map's north edge, a long lens straight down the strip of grass between the house (right) and the
      // bus (left): the horde walking up it at us, the one who will stay outside in the foreground at the house's corner.
      { t: 0, pos: [42.8, 2.2, -4.5], at: [41.8, 0.9, 9.0], fov: 30 },
      { t: 4.5, pos: [43.2, 2.5, -4.4], at: [42.0, 0.9, 5.0], fov: 36 },
      // La Lata gone: down to the one left outside, the dead closing on them, the city behind.
      { t: 7.6, pos: [left.x + 3.0, 3.4, left.z - 5.5], at: v3(left.clone().setZ(left.z + 1.5), 0.8), fov: 40 },
      { t: 10, pos: [left.x + 1.9, 2.2, left.z - 4.3], at: v3(left.clone().setX(left.x - 0.2).setZ(left.z + 1.6), 1.0), fov: 36 },
    ];
  },
  { lead: 3.6, times: [0.1, 0.15, 0.2, 0.25, 0.3], reveal: true, rate: (p) => (p < 8.6 ? 1.15 : COMBAT_RATES.fast) },
);
