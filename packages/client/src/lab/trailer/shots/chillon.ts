import { behind, combatShot, spot, unitSpot, v3 } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): the Hinchado bursts, the Chillón screams. Gameplay's plan «chillon»: the
// soldier at (24, 24) shoots the Hinchado at (24, 30); in the dead's turn the Chillón at (24, 33) screams. The end of
// the turn brought forward (the same orders, in the same order), so both fit; over the shooter's shoulder, down the street.

export default combatShot(
  'chillon',
  2,
  (_focus, battle) => {
    const shooter = unitSpot(battle, 's1');
    const street = spot(24, 31.5);
    return [
      { t: 0, pos: behind(shooter, street, 2.4, 1.3, 2.6), at: v3(street, 0.7), fov: 27 },
      { t: 2, pos: behind(shooter, street, 2.0, 1.2, 2.45), at: v3(spot(24, 32.5), 0.8), fov: 25 },
    ];
  },
  { lead: 0.3, times: [0.2, 1.0] },
);
