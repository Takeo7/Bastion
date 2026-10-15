import * as THREE from 'three';

// The marks over the special loot boxes (docs/FASE8.md §B): the icon of
// what is in each on a plate of the interface's dark steel, ringed in its
// colour, hung over the box. A mark shows once its box has been in sight and
// stays on the explored map; once the box is opened it goes. Drawn over
// everything (it is what the squad knows, like a note on the map), at the
// map's scale, so it shrinks and grows with the camera.

export type LootKind = 'lootWeapons' | 'lootMedical' | 'lootAmmo' | 'lootFood';

export const LOOT_KINDS: ReadonlySet<string> = new Set<LootKind>(['lootWeapons', 'lootMedical', 'lootAmmo', 'lootFood']);

/** Each kind's icon (public/ui/synty/icons) and ring: weapons the hazard yellow, medicines green, ammunition amber, food brass. */
const LOOKS: Record<LootKind, { icon: string; ring: string; size: number }> = {
  lootWeapons: { icon: 'rifle', ring: '#f2c230', size: 72 },
  lootMedical: { icon: 'medkit', ring: '#8fd05a', size: 72 },
  lootAmmo: { icon: 'ammo', ring: '#f5a524', size: 72 },
  // The interface's ration (its cans fill their square: drawn smaller).
  lootFood: { icon: 'r-racion', ring: '#d9a464', size: 54 },
};

/** A box on the map: its place (the engine's index: storey, then tile), its kind, where its mark hangs, and whether it is open. */
export interface LootSpot {
  place: number;
  kind: LootKind;
  at: THREE.Vector3;
  open: boolean;
}

/** Size of a mark, in tiles. */
const SIZE = 0.52;

const materials = new Map<LootKind, THREE.SpriteMaterial>();

/** Each kind's mark, drawn once: the plate, the ring, the icon (once it loads). */
function material(kind: LootKind): THREE.SpriteMaterial {
  let m = materials.get(kind);
  if (m) return m;
  const look = LOOKS[kind];
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 128;
  const ctx = canvas.getContext('2d')!;
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const draw = (icon?: HTMLImageElement) => {
    ctx.clearRect(0, 0, 128, 128);
    ctx.beginPath();
    ctx.arc(64, 64, 56, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(27, 30, 32, 0.94)';
    ctx.fill();
    ctx.lineWidth = 9;
    ctx.strokeStyle = look.ring;
    ctx.stroke();
    // A little short of white, so the bloom leaves it be.
    ctx.globalAlpha = 0.86;
    if (icon) ctx.drawImage(icon, 64 - look.size / 2, 64 - look.size / 2, look.size, look.size);
    ctx.globalAlpha = 1;
    texture.needsUpdate = true;
  };
  draw();
  const img = new Image();
  img.onload = () => draw(img);
  img.src = `/ui/synty/icons/${look.icon}.webp`;
  m = new THREE.SpriteMaterial({ map: texture, depthTest: false, depthWrite: false, transparent: true, toneMapped: false });
  materials.set(kind, m);
  return m;
}

export class LootMarks {
  readonly group = new THREE.Group();
  private marks: { spot: LootSpot; sprite: THREE.Sprite }[] = [];

  /** The boxes as the map stands now (a rebuild finds them again). */
  set(spots: LootSpot[]): void {
    this.group.clear();
    this.marks = spots.map((spot) => {
      const sprite = new THREE.Sprite(material(spot.kind));
      sprite.position.copy(spot.at);
      sprite.scale.setScalar(SIZE);
      sprite.renderOrder = 20;
      sprite.visible = false;
      this.group.add(sprite);
      return { spot, sprite };
    });
  }

  /** Shows the marks of the boxes seen (`explored`, by place) and still shut, but not those the cutaway hides. */
  update(explored: ArrayLike<number>, hidden: (place: number) => boolean): void {
    for (const { spot, sprite } of this.marks) sprite.visible = !spot.open && explored[spot.place] === 1 && !hidden(spot.place);
  }
}
