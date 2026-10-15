import { combatShot, spot, v3 } from '../sala';

// Shot 5, the montage (docs/TRAILER.md): a door opens and behind it are the dead. Gameplay's plan «puerta»: the
// soldier at (26, 32) opens the door at (27, 32); two zombis at (28, 32) and (29, 32). From inside the room, behind
// the dead: the door swings open on the soldier in the doorway.

export default combatShot('puerta', 2, () => {
  const door = spot(27, 32);
  return [
    { t: 0, pos: [door.x + 4.2, door.y + 1.7, door.z + 1.3], at: v3(door, 1.05), fov: 38 },
    { t: 2, pos: [door.x + 3.8, door.y + 1.6, door.z + 1.1], at: v3(door, 1.0), fov: 36 },
  ];
});
