import { combatShot, spot, v3 } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): the industrial estate, in one long shot. Gameplay's plan «poligono»: a soldier
// runs from (24, 24) to (24, 31) among the containers. From above, in front of them, as they run at us.

export default combatShot('poligono', 2, () => {
  const mid = spot(24, 27.5);
  return [
    { t: 0, pos: [mid.x + 0.6, mid.y + 5.6, mid.z + 9.4], at: v3(spot(24, 26), 0.8), fov: 38 },
    { t: 2, pos: [mid.x + 0.5, mid.y + 5.2, mid.z + 9.6], at: v3(spot(24, 28.5), 0.8), fov: 38 },
  ];
});
