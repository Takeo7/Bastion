import { around, combatShot } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): the machete. A soldier's Tajo at the zombi beside them, which falls for good.
// Gameplay's plan «machete»; close and low on the two of them, pushing in as the blow lands.

export default combatShot('machete', 2, (focus) => [
  { t: 0, pos: around(focus, 35, 4.2, 1.5), at: [focus.x, focus.y + 0.85, focus.z], fov: 38 },
  { t: 2, pos: around(focus, 28, 3.4, 1.3), at: [focus.x, focus.y + 0.75, focus.z], fov: 36 },
]);
