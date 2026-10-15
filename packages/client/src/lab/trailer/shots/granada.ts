import { behind, combatShot, spot, unitSpot, v3 } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): the grenade at a wall. It breaks, and the two zombis behind it take the blast.
// Gameplay's plan «granada»: the thrower at (28, 22), the wall at (28, 26), the dead at (28, 28) and (28, 29).
// Behind the thrower and to one side: the throw, the wall breaking, the blast beyond it.

export default combatShot('granada', 2, (_focus, battle) => {
  const thrower = unitSpot(battle, 's1');
  const wall = spot(28, 26.5);
  return [
    { t: 0, pos: behind(thrower, wall, 3.0, 1.6, 2.7), at: v3(wall, 0.9), fov: 40 },
    { t: 2, pos: behind(thrower, wall, 2.6, 1.4, 2.5), at: v3(wall, 0.8), fov: 38 },
  ];
});
