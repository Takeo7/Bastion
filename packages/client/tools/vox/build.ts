// Writes the hand-made kits to src/assets/voxel/*.vox:
//   npx tsx packages/client/tools/vox/build.ts [kit…]
// Careful: this overwrites the files, touch-ups made in MagicaVoxel included.
// Name the kits to write only those.

import { writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { readVox, writeVox } from '../../src/game/vox.ts';
import type { Kit } from './sculpt.ts';
import { adventKit } from './advent.ts';
import { mecKit } from './mec.ts';
import { relayKit } from './relay.ts';
import { sectoidKit } from './sectoid.ts';
import { soldierKit } from './soldier.ts';
import { vipKit } from './vip.ts';
import { xenoidKit } from './xenoid.ts';
import { zombieKit } from './zombie.ts';

const KITS: Record<string, () => Kit> = {
  soldier: soldierKit,
  sectoid: sectoidKit,
  advent: adventKit,
  xenoid: xenoidKit,
  mec: mecKit,
  zombie: zombieKit,
  relay: relayKit,
  vip: vipKit,
};

const out = resolve(import.meta.dirname, '../../src/assets/voxel');
const wanted = process.argv.slice(2);
for (const [name, make] of Object.entries(KITS)) {
  if (wanted.length && !wanted.includes(name)) continue;
  const kit = make().toVox();
  const bytes = writeVox(kit);
  // Read it back: what MagicaVoxel and the game will see.
  const back = readVox(bytes);
  const cells = back.parts.reduce((n, p) => n + p.cells.length / 4, 0);
  writeFileSync(resolve(out, `${name}.vox`), bytes);
  console.log(`${name}.vox: ${back.parts.length} models, ${cells} voxels, ${bytes.length} bytes`);
}
