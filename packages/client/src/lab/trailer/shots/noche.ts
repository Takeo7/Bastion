import { combatShot, spot, v3 } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): by night, a soldier with a torch down a street under the lamps. Gameplay's
// plan «noche»: from (24, 23) to (24, 30). Low in front of them, looking back up the street as they come.

export default combatShot('noche', 2, () => {
  const end = spot(24, 30);
  return [
    { t: 0, pos: [end.x - 1.3, end.y + 1.15, end.z + 1.2], at: v3(spot(24, 25), 1.05), fov: 30 },
    { t: 2, pos: [end.x - 1.25, end.y + 1.1, end.z + 1.6], at: v3(spot(24, 27.5), 1.05), fov: 30 },
  ];
});
