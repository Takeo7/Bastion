import { around, combatShot, spot, v3 } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): searching. The soldier goes through the room, then opens the weapons crate
// beside them. Gameplay's plan «registrar»: the soldier at (28, 28), the crate at (29, 28). From above at an angle (the
// game takes the roof off the room its soldier is in), coming in on the crate.

export default combatShot(
  'registrar',
  2,
  () => {
    const room = spot(28.5, 28);
    return [
      { t: 0, pos: around(room, 165, 3.4, 3.6), at: v3(room, 0.45), fov: 40 },
      { t: 2, pos: around(room, 155, 2.9, 3.2), at: v3(spot(29, 28), 0.35), fov: 38 },
    ];
  },
  // The rummage, then the crate within the two seconds.
  { lead: 0.4, times: [0.2, 1.3] },
);
