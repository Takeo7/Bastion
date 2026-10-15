import { combatShot, spot, v3 } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): the service area in the woods, by day, in one long shot. Gameplay's plan
// «area»: a soldier runs from (24, 24) to (17, 24) across it. Wide, from the north-east and above, drifting with them.

export default combatShot('area', 2, () => {
  const mid = spot(20.5, 23);
  return [
    { t: 0, pos: [mid.x + 3.6, mid.y + 4.4, mid.z - 7.4], at: v3(spot(21, 23.5), 0.6), fov: 42 },
    { t: 2, pos: [mid.x + 2.2, mid.y + 4.2, mid.z - 7.6], at: v3(spot(18.5, 23.5), 0.6), fov: 42 },
  ];
});
