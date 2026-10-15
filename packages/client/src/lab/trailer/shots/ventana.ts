import { combatShot, spot, v3 } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): a soldier jumps out through a window. Gameplay's plan «ventana»: from (30, 22)
// indoors out to (27, 22), through the window at (29, 22) or so. Outside, low, the window in front: they come out at us.

export default combatShot('ventana', 2, () => {
  const window = spot(28.6, 22);
  return [
    { t: 0, pos: [window.x - 4.6, window.y + 1.3, window.z + 1.6], at: v3(window, 1.0), fov: 40 },
    { t: 2, pos: [window.x - 4.0, window.y + 1.2, window.z + 1.4], at: v3(spot(27.4, 22), 0.9), fov: 40 },
  ];
});
