import * as THREE from 'three';
import { distance, type Vec2 } from '@bastion/engine';
import { tileToWorld } from './coords';

/** A place the squad has to reach (the objective, the evacuation zone), or where the partner just made something happen. */
export interface EdgeMark {
  kind: 'objective' | 'evac' | 'alert';
  label: string;
  pos: Vec2;
}

interface Shown {
  mark: EdgeMark;
  world: THREE.Vector3;
  el: HTMLElement;
  arrow: HTMLElement;
  text: HTMLElement;
  label: string;
  /** Half the mark's size, measured when its label changes: it is kept whole on screen. */
  halfW: number;
  halfH: number;
}

/**
 * Screen-space room the arrows keep clear of: the turn bar above, the
 * soldier sheet and the ability bar below. A mark inside it is on screen and
 * needs no arrow (its beacon shows).
 */
const MARGIN = { top: 72, right: 36, bottom: 120, left: 36 };

/**
 * Arrows at the edge of the screen pointing at marks that are off it, with the
 * distance in tiles from the selected soldier (UX-REVISION-2 #33). Clicking
 * one centres the camera on it. Only the positioning is set here; the look
 * lives in styles.css (.edge-mark, .edge-arrow, .edge-label).
 */
export class EdgeMarks {
  readonly root = document.createElement('div');
  private shown: Shown[] = [];
  private key = '';
  private width = 0;
  private height = 0;
  private readonly ndc = new THREE.Vector3();
  private readonly local = new THREE.Vector3();

  constructor(private readonly onPick: (mark: EdgeMark) => void) {
    this.root.className = 'edge-marks';
    this.root.style.cssText = 'position:absolute;inset:0;pointer-events:none;overflow:hidden';
    new ResizeObserver(() => {
      this.width = this.root.clientWidth;
      this.height = this.root.clientHeight;
    }).observe(this.root);
  }

  /** The marks to point at; rebuilt only when they change. */
  set(marks: EdgeMark[]): void {
    const key = JSON.stringify(marks);
    if (key === this.key) return;
    this.key = key;
    for (const s of this.shown) s.el.remove();
    this.shown = marks.map((mark) => {
      const el = document.createElement('div');
      el.className = `edge-mark ${mark.kind}`;
      el.style.cssText = 'position:absolute;left:0;top:0;pointer-events:auto;cursor:pointer;display:none';
      el.title = `Centrar la cámara en: ${mark.label} (O)`;
      const arrow = document.createElement('span');
      arrow.className = 'edge-arrow';
      arrow.style.display = 'inline-block';
      arrow.textContent = '➤';
      const text = document.createElement('span');
      text.className = 'edge-label';
      el.append(arrow, text);
      el.addEventListener('click', () => this.onPick(mark));
      this.root.append(el);
      return { mark, world: tileToWorld(mark.pos, 1), el, arrow, text, label: '', halfW: 0, halfH: 0 };
    });
  }

  /** Places the arrows for this frame; `from` is where distances are measured from. */
  update(camera: THREE.Camera, from: Vec2 | null): void {
    const w = this.width;
    const h = this.height;
    if (!w || !h) return;
    const left = MARGIN.left;
    const right = w - MARGIN.right;
    const top = MARGIN.top;
    const bottom = h - MARGIN.bottom;
    const cx = (left + right) / 2;
    const cy = (top + bottom) / 2;
    for (const s of this.shown) {
      this.ndc.copy(s.world).project(camera);
      let x = ((this.ndc.x + 1) / 2) * w;
      let y = ((1 - this.ndc.y) / 2) * h;
      // Behind the camera the projection comes out mirrored.
      this.local.copy(s.world).applyMatrix4(camera.matrixWorldInverse);
      const behind = this.local.z > 0;
      if (behind) {
        x = 2 * cx - x;
        y = 2 * cy - y;
      }
      if (!behind && x >= left && x <= right && y >= top && y <= bottom) {
        s.el.style.display = 'none';
        continue;
      }
      const dx = x - cx;
      const dy = y - cy;
      const tx = dx > 0 ? (right - cx) / dx : dx < 0 ? (left - cx) / dx : Infinity;
      const ty = dy > 0 ? (bottom - cy) / dy : dy < 0 ? (top - cy) / dy : Infinity;
      const t = Math.min(tx, ty);
      s.el.style.display = '';
      const label = from ? `${s.mark.label} · ${Math.round(distance(from, s.mark.pos))} casillas` : s.mark.label;
      if (label !== s.label) {
        s.text.textContent = s.label = label;
        s.halfW = s.el.offsetWidth / 2;
        s.halfH = s.el.offsetHeight / 2;
      }
      const px = Math.min(Math.max(cx + dx * t, left + s.halfW), right - s.halfW);
      const py = Math.min(Math.max(cy + dy * t, top + s.halfH), bottom - s.halfH);
      s.el.style.transform = `translate(${px - s.halfW}px, ${py - s.halfH}px)`;
      s.arrow.style.transform = `rotate(${Math.atan2(dy, dx)}rad)`;
    }
  }
}
