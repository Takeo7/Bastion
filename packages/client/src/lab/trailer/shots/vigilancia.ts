import { behind, combatShot, spot, unitSpot, v3 } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): overwatch. The squad watches the street; in the dead's turn two come up it
// and the reaction shot goes off, and kills. Gameplay's plan «vigilancia»: the squad round (24, 24), the dead from the
// south. Over the squad's heads, a long lens down the street at them; the take begins as the first steps into the
// sights (measured: 3.6 s after the end of the turn), the shot and the fall in its two seconds.

export default combatShot(
  'vigilancia',
  2,
  (_focus, battle) => {
    const squad = unitSpot(battle, 's1');
    const street = spot(24, 31);
    return [
      { t: 0, pos: behind(squad, street, 2.6, 0.3, 2.6), at: v3(spot(24, 32), 0.7), fov: 19 },
      { t: 2, pos: behind(squad, street, 2.3, 0.25, 2.5), at: v3(spot(24, 31.5), 0.65), fov: 17 },
    ];
  },
  { lead: 4.2 },
);
