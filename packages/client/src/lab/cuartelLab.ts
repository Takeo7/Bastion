import { createCampaign, PLAYER_COLORS, Rng } from '@bastion/engine';
import * as THREE from 'three';
import { gearOf } from '../game/models';
import { BastionSet } from '../strategy/bastion';
import { Stage } from '../strategy/stage';
import { loadBaseKit } from '../strategy/baseKit';
import '../styles.css';
import '../strategy/strategy.css';

// Cuartel lab (/cuartel.html): the base's stage on its own, the camera on the
// soldier being inspected (armory shot), to look at how a unit renders there.
// `__cuartel.snap(name)` saves the frame to the dev server's /__snap;
// `__cuartel.aoWithUnit(true)` puts the soldier into the ambient occlusion's
// pre-pass (the battle leaves units out of it, UNIT_LAYER) to compare.
// `?timer` drives frames with a timer, for a preview that isn't on screen.

if (new URLSearchParams(location.search).has('timer')) {
  window.requestAnimationFrame = (cb) => window.setTimeout(() => cb(performance.now()), 16);
  Object.defineProperty(document, 'hidden', { get: () => false });
}

const stage = new Stage();
stage.element.style.cssText = 'position: fixed; inset: 0';
document.body.append(stage.element);
const bastion = new BastionSet(() => undefined);
stage.add('bastion', bastion);
const campaign = createCampaign([0], new Rng(3));
const infirmary = new URLSearchParams(location.search).get('infirmary');
const medicalPlot = infirmary ? campaign.base.plots.find((p) => !p.building) : undefined;
if (medicalPlot) {
  medicalPlot.building = 'infirmary';
  medicalPlot.terrain = 'clear';
  medicalPlot.level = Math.max(1, Math.min(3, Number(infirmary)));
}
bastion.setBase(campaign);
const soldier = campaign.soldiers[0]!;
bastion.setShowcase('rookie', PLAYER_COLORS[0], soldier.appearance, gearOf(1, 1), soldier.id);
void stage.show('bastion', medicalPlot ? bastion.room(medicalPlot.id) : bastion.armory(), true);

function canvas(): HTMLCanvasElement {
  return stage.element.querySelector('canvas')!;
}

/** Saves the stage's last frame as .dev-snapshots/<name>.jpg. */
async function snap(name = 'cuartel', width = 1200): Promise<string> {
  await new Promise((r) => requestAnimationFrame(() => r(null)));
  const src = canvas();
  const c = document.createElement('canvas');
  c.width = width;
  c.height = Math.round((width * src.height) / src.width);
  c.getContext('2d')!.drawImage(src, 0, 0, c.width, c.height);
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: c.toDataURL('image/jpeg', 0.92) });
  return name;
}

/** The soldier on the pedestal into (or out of) the occlusion's pre-pass. */
function aoWithUnit(on: boolean): void {
  bastion.scene.traverse((o) => {
    if (o instanceof THREE.Mesh && o.layers.isEnabled(1)) {
      if (on) o.layers.enable(0);
      else o.layers.disable(0);
    }
  });
}

(window as unknown as { __cuartel: unknown }).__cuartel = { stage, bastion, snap, aoWithUnit };
window.addEventListener('keydown', (e) => { if (e.code === 'KeyP') void snap(infirmary ? `infirmary_base_${infirmary}` : 'cuartel'); });
if (new URLSearchParams(location.search).has('snapshot')) {
  void loadBaseKit().then(async () => {
    for (let n = 0; n < 30; n++) await new Promise<void>((r) => requestAnimationFrame(() => r()));
    stage.frameNow();
    await snap(`infirmary_base_${infirmary}`);
  });
}
