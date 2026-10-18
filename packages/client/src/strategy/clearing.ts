import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { BASE_EDGE, BUILD_TEXT, buildingWorks, fellHint, pieceAt, pieceCells, pieceHint, PIECES, treeAt, type BasePiece, type BaseState, type PieceId, type Vec2 } from '@bastion/engine';
import { CameraRig } from '../game/camera';
import { STOREY } from '../game/coords';
import type { Shot, Stage, StageMood, StageSet } from './stage';

// The base in the clearing (docs/FASE12.md §11.7): the map seen as in battle,
// with the camera of battle, and building on it. This set is the screen's
// hands: the camera, the pointer on the tiles, and what building shows over
// the ground (the grid, the piece about to go down in green or red, the
// rectangle to fell, the piece picked). The ground, the pines and the pieces
// are Arte's (ClearingArt); until that is in, SketchArt draws them as blocks.

/**
 * What draws the clearing: the ground, the pines and the pieces (Arte's,
 * strategy/clearingArt.ts). As in battle, a tile is a unit and tile (x, y)
 * has its middle at (x + 0.5, 0, y + 0.5).
 */
export interface ClearingArt {
  readonly group: THREE.Group;
  /** The clearing as it stands: called again whenever it changes. */
  setBase(base: BaseState): void;
  /** A piece alone, for the one about to be built: turned `rot`, centred on its footprint at the origin. */
  pieceModel(piece: PieceId, rot: number, level?: number): THREE.Object3D;
  tick?(dt: number, time: number): void;
  /** The light after the scene (the battle's, or the snow's in winter). */
  readonly mood?: StageMood;
  /** Its sky and haze on the set's scene (its lights are in `group`). */
  dress?(scene: THREE.Scene): void;
  /** The camera it is seen with. */
  watch?(camera: THREE.Camera): void;
  /** GPU work before a frame, and whether the shadow map is drawn again (StageSet.beforeRender). */
  beforeRender?(gl: THREE.WebGLRenderer): void;
}

/** What the pointer does on the clearing. */
export type ClearingTool = { kind: 'look' } | { kind: 'place'; piece: PieceId; rot: number } | { kind: 'fell' };

export interface ClearingHandlers {
  /** Why `piece` can't go there (the place, the Bastion's level, the cost), or null. */
  blocker(piece: PieceId, x: number, y: number, rot: number): string | null;
  /** The line by the pointer with `piece` in hand: its cost, or why it doesn't fit (`blocked`). */
  placeHint(piece: PieceId, blocked: string | null): string;
  place(piece: PieceId, x: number, y: number, rot: number): void;
  fell(tiles: Vec2[]): void;
  /** A piece picked (null: let go). */
  select(id: string | null): void;
  /** The tool was let go (Esc, or a right click while placing). */
  cancel(): void;
  /** The piece in hand and where it would go, for the partner (null: none); at most ten times a second, on change. */
  cursor?(c: BaseCursor | null): void;
}

/** A piece in somebody's hand on the clearing, where it would go. */
export interface BaseCursor {
  piece: PieceId;
  x: number;
  y: number;
  rot: number;
}

/** Felling sends at most this many tiles at a time (protocol.ts). */
const FELL_MAX = 400;
const GREEN = new THREE.Color(0x5fd28a);
const RED = new THREE.Color(0xff5a4a);
const AMBER = new THREE.Color(0xf0b040);

/** A piece's tiles across and deep once turned. */
export function footprint(piece: PieceId, rot: number): [number, number] {
  const [w, h] = PIECES[piece].size;
  return rot % 2 ? [h, w] : [w, h];
}

/** The top-left tile for `piece` turned `rot` with the pointer on `tile`: the pointer holds its middle. */
export function placeAt(piece: PieceId, rot: number, tile: Vec2): Vec2 {
  const [w, h] = footprint(piece, rot);
  return { x: tile.x - Math.floor((w - 1) / 2), y: tile.y - Math.floor((h - 1) / 2) };
}

export class ClearingSet implements StageSet {
  readonly scene = new THREE.Scene();
  readonly ambientOcclusion = true;
  get mood(): StageMood | undefined {
    return this.art.mood;
  }
  private stage: Stage | null = null;
  private rig: CameraRig | null = null;
  private base: BaseState | null = null;
  private art: ClearingArt = new SketchArt();
  private tool: ClearingTool = { kind: 'look' };
  /** The building tools are out: the grid and the edge band show. */
  private building = false;
  private hoverTile: Vec2 | null = null;
  private selected: string | null = null;
  private readonly raycaster = new THREE.Raycaster();
  private readonly ground = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
  /** A press on the ground: where (pixels), on which tile, and whether it has become a drag. */
  private press: { x: number; y: number; tile: Vec2 | null; moved: boolean; button: number } | null = null;
  /** The rectangle being dragged to fell: its corners. */
  private fellBox: { from: Vec2; to: Vec2 } | null = null;
  // Over the ground.
  private readonly grid: THREE.LineSegments;
  private readonly edgeBand = new THREE.Group();
  private readonly cells: THREE.InstancedMesh;
  private readonly cursor: THREE.LineLoop;
  private readonly outline: THREE.LineLoop;
  private readonly proposalMark: THREE.LineLoop;
  private ghost: THREE.Object3D | null = null;
  private ghostKey = '';
  private readonly ghostMaterial = new THREE.MeshBasicMaterial({ color: GREEN, transparent: true, opacity: 0.45, depthWrite: false });
  private readonly hint = document.createElement('div');
  private proposal: { piece: PieceId; x: number; y: number; rot: number } | null = null;
  /** The proposed piece and the partner's in hand, as translucent models (in blue; in the partner's colour, with the name). */
  private proposalModel: THREE.Object3D | null = null;
  private readonly proposalMaterial = new THREE.MeshBasicMaterial({ color: 0x7ac8ff, transparent: true, opacity: 0.32, depthWrite: false });
  private partnerModel: THREE.Object3D | null = null;
  private partnerKey = '';
  private readonly partnerMaterial = new THREE.MeshBasicMaterial({ transparent: true, opacity: 0.38, depthWrite: false });
  private readonly partnerTag = new CSS2DObject(document.createElement('div'));
  /** What was last sent of this player's hand (cursor), and when. */
  private cursorKey = 'null';
  private cursorAt = 0;
  private cursorTimer = 0;

  constructor(private readonly handlers: ClearingHandlers) {
    this.scene.background = new THREE.Color(0x1a2016);
    this.scene.add(this.art.group);

    this.grid = new THREE.LineSegments(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.12, depthWrite: false }));
    this.cells = new THREE.InstancedMesh(
      new THREE.PlaneGeometry(0.92, 0.92).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.4, depthWrite: false }),
      FELL_MAX * 2,
    );
    this.cells.count = 0;
    this.cells.frustumCulled = false;
    this.cells.position.y = 0.04;
    this.cells.renderOrder = 5;
    const loop = (color: number, opacity: number) =>
      new THREE.LineLoop(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color, transparent: true, opacity, depthTest: false }));
    this.cursor = loop(0xffffff, 0.8);
    this.outline = loop(0xffe08a, 1);
    this.proposalMark = loop(0x7ac8ff, 0.9);
    for (const o of [this.cursor, this.outline, this.proposalMark]) {
      o.renderOrder = 6;
      o.visible = false;
    }
    this.grid.visible = this.edgeBand.visible = false;
    this.scene.add(this.grid, this.edgeBand, this.cells, this.cursor, this.outline, this.proposalMark);
    this.hint.className = 'clearing-hint';
    this.hint.hidden = true;
    this.partnerTag.element.className = 'clearing-partner';
    this.partnerTag.visible = false;
    this.scene.add(this.partnerTag);
  }

  /** Arte's drawing in place of the blocks (clearingArt.ts), or another one of it (the snow's). */
  setArt(art: ClearingArt): void {
    this.scene.remove(this.art.group);
    this.art = art;
    this.scene.add(art.group);
    art.dress?.(this.scene);
    if (this.stage) art.watch?.(this.stage.camera);
    if (this.base) art.setBase(this.base);
    this.ghostKey = '';
    this.partnerKey = '';
    this.setProposal(this.proposal);
  }

  attach(stage: Stage): void {
    this.stage = stage;
    this.art.watch?.(stage.camera);
    this.rig = new CameraRig(stage.camera, () => stage.current === 'clearing' && stage.element.isConnected);
    const el = stage.element;
    el.append(this.hint);
    el.addEventListener('pointerdown', this.onDown);
    el.addEventListener('pointermove', this.onMove);
    el.addEventListener('pointerleave', () => this.setHover(null));
    el.addEventListener('contextmenu', (e) => {
      if (stage.current === 'clearing') e.preventDefault();
    });
    window.addEventListener('pointerup', this.onUp);
    window.addEventListener('keydown', this.onKey);
  }

  dispose(): void {
    window.clearTimeout(this.cursorTimer);
    this.rig?.dispose();
    window.removeEventListener('pointerup', this.onUp);
    window.removeEventListener('keydown', this.onKey);
  }

  // ------------------------------------------------------------ the base

  setBase(base: BaseState): void {
    const first = !this.base;
    this.base = base;
    this.art.setBase(base);
    if (first) {
      this.rig?.setBounds(base.width, base.height);
      const caravan = base.pieces.find((p) => p.piece === 'caravan');
      this.focusTile(caravan ? centre(caravan) : { x: base.width / 2, y: base.height / 2 }, true);
      this.drawGrid(base);
    }
    if (this.selected && !base.pieces.some((p) => p.id === this.selected)) this.select(null);
    this.refresh();
  }

  /** A piece somebody proposed to build (a vote pending): in blue, where it would go. */
  setProposal(p: { piece: PieceId; x: number; y: number; rot: number } | null): void {
    this.proposal = p;
    this.proposalMark.visible = !!p;
    if (this.proposalModel) this.scene.remove(this.proposalModel);
    this.proposalModel = p ? this.model(p, this.proposalMaterial) : null;
    if (p) setLoop(this.proposalMark, p.x, p.y, ...footprint(p.piece, p.rot), 0.07);
  }

  /** The partner's piece in hand (Construir), in their colour with their name; null: none. */
  setPartnerCursor(p: (BaseCursor & { color: string; name: string }) | null): void {
    const key = p ? `${p.piece}:${p.rot}` : '';
    if (key !== this.partnerKey) {
      if (this.partnerModel) this.scene.remove(this.partnerModel);
      this.partnerModel = p ? this.model(p, this.partnerMaterial) : null;
      this.partnerKey = key;
    }
    this.partnerTag.visible = !!p;
    if (!p) return;
    const [w, h] = footprint(p.piece, p.rot);
    this.partnerModel?.position.set(p.x + w / 2, 0.02, p.y + h / 2);
    this.partnerMaterial.color.set(p.color);
    this.partnerTag.position.set(p.x + w / 2, 0.4, p.y + h / 2);
    this.partnerTag.element.textContent = p.name;
    this.partnerTag.element.style.setProperty('--accent', p.color);
  }

  /** A piece's model alone, centred on where it would stand, with `material` on every mesh (and no shadow). */
  private model(p: { piece: PieceId; x: number; y: number; rot: number }, material: THREE.Material): THREE.Object3D {
    const m = this.art.pieceModel(p.piece, p.rot);
    m.userData.material = material;
    m.traverse((o) => {
      const mesh = o as THREE.Mesh;
      if (mesh.isMesh) {
        mesh.material = material;
        mesh.castShadow = false;
      }
    });
    const [w, h] = footprint(p.piece, p.rot);
    m.position.set(p.x + w / 2, 0.02, p.y + h / 2);
    this.scene.add(m);
    return m;
  }

  /** The camera, where it is now: the stage cuts to it (Stage.show). */
  shot(): Shot {
    this.rig?.update(1);
    const camera = this.stage!.camera;
    return { pos: camera.position.clone(), target: this.rig!.focusPoint.clone(), fov: 40 };
  }

  /** The camera goes to the middle of tile `t` (fractions allowed). */
  focusTile(t: { x: number; y: number }, instant = false): void {
    this.rig?.focus(new THREE.Vector3(t.x + 0.5, 0, t.y + 0.5), instant);
  }

  /** The camera to the caravan, where La Lata stands (its take-off and its return: screen.ts). */
  focusCaravan(instant = false): void {
    const caravan = this.base?.pieces.find((p) => p.piece === 'caravan');
    if (caravan) this.focusTile(centre(caravan), instant);
  }

  /** Goes to a piece and picks it (nextSteps' «Repara X»). */
  focusPiece(id: string): void {
    const p = this.base?.pieces.find((x) => x.id === id);
    if (!p) return;
    this.focusTile(centre(p));
    this.select(id);
  }

  /** Another screen takes the stage: nothing stays under a pointer that is gone. */
  leave(): void {
    this.press = null;
    this.fellBox = null;
    this.setHover(null);
  }

  // ------------------------------------------------------------ tools

  setBuilding(on: boolean): void {
    this.building = on;
    this.grid.visible = this.edgeBand.visible = on;
    if (!on) this.setTool({ kind: 'look' });
    this.refresh();
  }

  setTool(tool: ClearingTool): void {
    this.tool = tool;
    this.fellBox = null;
    if (tool.kind !== 'look') this.select(null, false);
    this.refresh();
  }

  get currentTool(): ClearingTool {
    return this.tool;
  }

  /** R: the piece about to go down turns a quarter, clockwise (Mayús: the other way). */
  turn(dir = 1): void {
    if (this.tool.kind !== 'place') return;
    this.tool = { ...this.tool, rot: (this.tool.rot + dir + 4) % 4 };
    this.refresh();
  }

  select(id: string | null, tell = true): void {
    if (this.selected === id) return;
    this.selected = id;
    this.refresh();
    if (tell) this.handlers.select(id);
  }

  // ------------------------------------------------------------ StageSet

  hits(): THREE.Object3D[] {
    return [];
  }

  /** A left drag pans, except while felling (it draws the rectangle). */
  drag(dx: number, dy: number): boolean {
    if (this.tool.kind === 'fell') return true;
    this.rig?.panPixels(dx, dy);
    return true;
  }

  zoom(factor: number): void {
    this.rig?.zoom((factor - 1) * 1000);
  }

  beforeRender(gl: THREE.WebGLRenderer): void {
    if (this.art.beforeRender) this.art.beforeRender(gl);
    else gl.shadowMap.needsUpdate = true;
  }

  tick(dt: number, time: number): void {
    this.rig?.update(dt);
    this.art.tick?.(dt, time);
    if (this.outline.visible) (this.outline.material as THREE.LineBasicMaterial).opacity = 0.7 + Math.sin(time * 4) * 0.3;
  }

  // ------------------------------------------------------------ input

  private tileAt(e: PointerEvent): Vec2 | null {
    const stage = this.stage;
    if (!stage || !this.base) return null;
    const rect = stage.element.getBoundingClientRect();
    this.raycaster.setFromCamera(new THREE.Vector2(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1), stage.camera);
    const p = this.raycaster.ray.intersectPlane(this.ground, new THREE.Vector3());
    if (!p) return null;
    const t = { x: Math.floor(p.x), y: Math.floor(p.z) };
    return t.x >= 0 && t.y >= 0 && t.x < this.base.width && t.y < this.base.height ? t : null;
  }

  private readonly onDown = (e: PointerEvent): void => {
    if (this.stage?.current !== 'clearing' || e.target !== this.stage.element.querySelector('canvas')) return;
    this.press = { x: e.clientX, y: e.clientY, tile: this.tileAt(e), moved: false, button: e.button };
  };

  private readonly onMove = (e: PointerEvent): void => {
    if (this.stage?.current !== 'clearing') return;
    const press = this.press;
    if (press && Math.abs(e.clientX - press.x) + Math.abs(e.clientY - press.y) > 4) {
      // A right drag pans (the left one comes through the stage's drag).
      if (press.button === 2) this.rig?.panPixels(e.movementX, e.movementY);
      press.moved = true;
    }
    const tile = this.tileAt(e);
    if (press?.moved && press.button === 0 && this.tool.kind === 'fell' && press.tile && tile) this.fellBox = { from: press.tile, to: tile };
    this.setHover(tile, e);
  };

  private readonly onUp = (e: PointerEvent): void => {
    const press = this.press;
    this.press = null;
    if (!press || this.stage?.current !== 'clearing') return;
    if (press.button === 2) {
      // A right click without dragging lets go of the piece in hand.
      if (!press.moved && this.tool.kind !== 'look') this.handlers.cancel();
      return;
    }
    if (press.button !== 0) return;
    const tile = this.tileAt(e) ?? press.tile;
    if (this.tool.kind === 'fell') {
      const box = this.fellBox ?? (tile ? { from: tile, to: tile } : null);
      this.fellBox = null;
      if (box) {
        const trees = this.treesIn(box);
        if (trees.length) this.handlers.fell(trees.slice(0, FELL_MAX));
      }
      this.refresh();
      return;
    }
    if (press.moved || !tile) return;
    if (this.tool.kind === 'place') {
      const at = placeAt(this.tool.piece, this.tool.rot, tile);
      if (!this.handlers.blocker(this.tool.piece, at.x, at.y, this.tool.rot)) this.handlers.place(this.tool.piece, at.x, at.y, this.tool.rot);
      return;
    }
    const p = this.base ? pieceAt(this.base, tile.x, tile.y) : undefined;
    this.select(p?.id ?? null);
  };

  private readonly onKey = (e: KeyboardEvent): void => {
    if (this.stage?.current !== 'clearing' || (e.target as HTMLElement | null)?.closest?.('input, textarea')) return;
    if (e.code === 'KeyR' && this.tool.kind === 'place') {
      e.preventDefault();
      this.turn(e.shiftKey ? -1 : 1);
    }
  };

  // ------------------------------------------------------------ drawing

  private setHover(tile: Vec2 | null, e?: PointerEvent): void {
    this.hoverTile = tile;
    this.refresh(e);
  }

  /** The trees inside a dragged rectangle. */
  private treesIn(box: { from: Vec2; to: Vec2 }): Vec2[] {
    const base = this.base;
    if (!base) return [];
    const out: Vec2[] = [];
    const [x0, x1] = [Math.min(box.from.x, box.to.x), Math.max(box.from.x, box.to.x)];
    const [y0, y1] = [Math.min(box.from.y, box.to.y), Math.max(box.from.y, box.to.y)];
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) if (treeAt(base, x, y)) out.push({ x, y });
    return out;
  }

  /** Everything over the ground, as the tool, the pointer and the pick stand now. */
  private refresh(e?: PointerEvent): void {
    const base = this.base;
    const tile = this.hoverTile;
    let n = 0;
    const m = new THREE.Matrix4();
    const mark = (x: number, y: number, color: THREE.Color) => {
      if (n >= this.cells.instanceMatrix.count) return;
      m.makeTranslation(x + 0.5, 0, y + 0.5);
      this.cells.setMatrixAt(n, m);
      this.cells.setColorAt(n++, color);
    };
    let hint: { text: string; bad?: boolean } | null = null;
    this.cursor.visible = false;
    let ghostAt: { piece: PieceId; rot: number; x: number; y: number; ok: boolean } | null = null;

    if (base && tile && this.tool.kind === 'place') {
      const { piece, rot } = this.tool;
      const at = placeAt(piece, rot, tile);
      const why = this.handlers.blocker(piece, at.x, at.y, rot);
      for (const c of pieceCells(piece, at.x, at.y, rot)) {
        const off = c.x < BASE_EDGE || c.y < BASE_EDGE || c.x >= base.width - BASE_EDGE || c.y >= base.height - BASE_EDGE;
        const bad = !!why && (off || treeAt(base, c.x, c.y) || !!pieceAt(base, c.x, c.y) || !/árbol|borde|ya hay/i.test(why));
        mark(c.x, c.y, bad ? RED : GREEN);
      }
      ghostAt = { piece, rot, x: at.x, y: at.y, ok: !why };
      hint = { text: this.handlers.placeHint(piece, why), bad: !!why };
    } else if (base && this.tool.kind === 'fell') {
      const box = this.fellBox ?? (tile ? { from: tile, to: tile } : null);
      if (box) {
        const trees = this.treesIn(box);
        for (const t of trees.slice(0, FELL_MAX)) mark(t.x, t.y, AMBER);
        const [x0, x1] = [Math.min(box.from.x, box.to.x), Math.max(box.from.x, box.to.x)];
        const [y0, y1] = [Math.min(box.from.y, box.to.y), Math.max(box.from.y, box.to.y)];
        setLoop(this.cursor, x0, y0, x1 - x0 + 1, y1 - y0 + 1, 0.06);
        this.cursor.visible = true;
        const k = Math.min(trees.length, FELL_MAX);
        hint = k ? { text: fellHint(k) } : { text: BUILD_TEXT.noTrees, bad: true };
      }
    } else if (base && tile) {
      const p = pieceAt(base, tile.x, tile.y);
      if (p) {
        hint = { text: pieceLine(p) };
        setLoop(this.cursor, p.x, p.y, ...footprint(p.piece, p.rot), 0.06);
      } else setLoop(this.cursor, tile.x, tile.y, 1, 1, 0.05);
      this.cursor.visible = this.building || !!p;
    }
    this.cells.count = n;
    this.cells.instanceMatrix.needsUpdate = true;
    if (this.cells.instanceColor) this.cells.instanceColor.needsUpdate = true;

    const sel = this.selected ? base?.pieces.find((p) => p.id === this.selected) : undefined;
    this.outline.visible = !!sel;
    if (sel) setLoop(this.outline, sel.x, sel.y, ...footprint(sel.piece, sel.rot), 0.08);

    this.setGhost(ghostAt);
    this.showHint(hint, e);
    this.sendCursor(ghostAt);
  }

  /** This player's hand, for the partner: only on change, at most every 100 ms (the last one always goes). */
  private sendCursor(g: { piece: PieceId; rot: number; x: number; y: number } | null): void {
    const c = g ? { piece: g.piece, x: g.x, y: g.y, rot: g.rot } : null;
    const key = c ? `${c.piece}:${c.x}:${c.y}:${c.rot}` : 'null';
    window.clearTimeout(this.cursorTimer);
    if (key === this.cursorKey) return;
    const go = () => {
      this.cursorKey = key;
      this.cursorAt = performance.now();
      this.handlers.cursor?.(c);
    };
    const wait = this.cursorAt + 100 - performance.now();
    if (wait <= 0) go();
    else this.cursorTimer = window.setTimeout(go, wait);
  }

  private setGhost(g: { piece: PieceId; rot: number; x: number; y: number; ok: boolean } | null): void {
    const key = g ? `${g.piece}:${g.rot}` : '';
    if (key !== this.ghostKey) {
      if (this.ghost) this.scene.remove(this.ghost);
      this.ghost = null;
      this.ghostKey = key;
      if (g) {
        this.ghost = this.art.pieceModel(g.piece, g.rot);
        // Models still loading come in with this material (clearingArt.ts).
        this.ghost.userData.material = this.ghostMaterial;
        this.ghost.traverse((o) => {
          const mesh = o as THREE.Mesh;
          if (mesh.isMesh) {
            mesh.material = this.ghostMaterial;
            mesh.castShadow = false;
          }
        });
        this.scene.add(this.ghost);
      }
    }
    if (!g || !this.ghost) return;
    const [w, h] = footprint(g.piece, g.rot);
    this.ghost.position.set(g.x + w / 2, 0.02, g.y + h / 2);
    this.ghostMaterial.color.copy(g.ok ? GREEN : RED);
  }

  private showHint(hint: { text: string; bad?: boolean } | null, e?: PointerEvent): void {
    this.hint.hidden = !hint;
    if (!hint) return;
    this.hint.textContent = hint.text;
    this.hint.classList.toggle('bad', !!hint.bad);
    if (e && this.stage) {
      const rect = this.stage.element.getBoundingClientRect();
      this.hint.style.left = `${e.clientX - rect.left + 16}px`;
      this.hint.style.top = `${e.clientY - rect.top + 18}px`;
    }
  }

  /** The tile grid over what can be built on, and the edge band where the dead come in, darker. */
  private drawGrid(base: BaseState): void {
    const pts: number[] = [];
    const [a, b] = [BASE_EDGE, base.width - BASE_EDGE];
    const [c, d] = [BASE_EDGE, base.height - BASE_EDGE];
    for (let x = a; x <= b; x++) pts.push(x, 0.03, c, x, 0.03, d);
    for (let y = c; y <= d; y++) pts.push(a, 0.03, y, b, 0.03, y);
    this.grid.geometry.dispose();
    this.grid.geometry = new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
    this.edgeBand.clear();
    const band = new THREE.MeshBasicMaterial({ color: 0x2a0c08, transparent: true, opacity: 0.35, depthWrite: false });
    const strip = (x: number, y: number, w: number, h: number) => {
      const mesh = new THREE.Mesh(new THREE.PlaneGeometry(w, h).rotateX(-Math.PI / 2), band);
      mesh.position.set(x + w / 2, 0.025, y + h / 2);
      this.edgeBand.add(mesh);
    };
    strip(0, 0, base.width, BASE_EDGE);
    strip(0, base.height - BASE_EDGE, base.width, BASE_EDGE);
    strip(0, BASE_EDGE, BASE_EDGE, base.height - BASE_EDGE * 2);
    strip(base.width - BASE_EDGE, BASE_EDGE, BASE_EDGE, base.height - BASE_EDGE * 2);
  }
}

/** The middle of a piece, in tiles. */
function centre(p: BasePiece): { x: number; y: number } {
  const [w, h] = footprint(p.piece, p.rot);
  return { x: p.x + (w - 1) / 2, y: p.y + (h - 1) / 2 };
}

/** A rectangle of tiles on the ground, as a closed line `lift` over it. */
function setLoop(loop: THREE.LineLoop, x: number, y: number, w: number, h: number, lift: number): void {
  const pts = [x, lift, y, x + w, lift, y, x + w, lift, y + h, x, lift, y + h];
  loop.geometry.dispose();
  loop.geometry = new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
}

/** A piece under the pointer: its name and how it is (out of use, hurt), or its level. */
function pieceLine(p: BasePiece): string {
  const def = PIECES[p.piece];
  const state = def.kind === 'building' && !buildingWorks(p) ? 'broken' : def.charges && !(p.charges ?? 0) ? (p.piece === 'turret' ? 'silent' : 'spent') : undefined;
  const level = def.kind === 'building' && p.piece !== 'caravan' && (p.level ?? 1) > 1 ? p.level : undefined;
  return pieceHint(p.piece, { state, hp: p.hp < def.hp ? [p.hp, def.hp] : undefined, level });
}

// ------------------------------------------------------------ provisional drawing

/** How tall each kind of tile stands, as a block. */
const BLOCK: Partial<Record<string, number>> = { wall: 1.6, door: 1.5, low: 0.55, high: 2.6, floor: 0.08, ladder: 0.08 };
const KIND_COLOR = { building: 0xa08f74, defence: 0x8a7458, trap: 0x6a5e52 } as const;

/**
 * The clearing in blocks until Arte's drawing is in: the grass, a cone for each
 * pine, and each piece as a block per tile, as tall as it stands in a fight.
 */
class SketchArt implements ClearingArt {
  readonly group = new THREE.Group();
  private readonly pines: THREE.InstancedMesh;
  private readonly pieces = new THREE.Group();
  private groundKey = '';
  private readonly materials = new Map<number, THREE.MeshStandardMaterial>();

  constructor() {
    this.group.add(new THREE.HemisphereLight(0xdfe8f0, 0x3a3428, 1.6));
    const sun = new THREE.DirectionalLight(0xfff0dc, 2.2);
    sun.position.set(-18, 30, 12);
    sun.target.position.set(20, 0, 20);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    Object.assign(sun.shadow.camera, { left: -32, right: 32, top: 32, bottom: -32, near: 1, far: 90 });
    this.group.add(sun, sun.target);
    this.pines = new THREE.InstancedMesh(new THREE.ConeGeometry(0.42, 1.9, 7).translate(0, 1.15, 0), new THREE.MeshStandardMaterial({ color: 0x24402a, roughness: 0.9 }), 1600);
    this.pines.castShadow = true;
    this.pines.count = 0;
    this.group.add(this.pines, this.pieces);
  }

  setBase(base: BaseState): void {
    const key = `${base.width}x${base.height}`;
    if (key !== this.groundKey) {
      this.groundKey = key;
      const ground = new THREE.Mesh(new THREE.PlaneGeometry(base.width, base.height).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x55603f, roughness: 1 }));
      ground.position.set(base.width / 2, 0, base.height / 2);
      ground.receiveShadow = true;
      this.group.add(ground);
    }
    const m = new THREE.Matrix4();
    const q = new THREE.Quaternion();
    const n = Math.min(base.trees.length, this.pines.instanceMatrix.count);
    for (let i = 0; i < n; i++) {
      const t = base.trees[i]!;
      const x = t % base.width;
      const y = Math.floor(t / base.width);
      const k = 0.8 + ((Math.sin(t * 12.9898) * 43758.5453) % 1 + 1) % 1 * 0.6;
      m.compose(new THREE.Vector3(x + 0.5, 0, y + 0.5), q, new THREE.Vector3(k, k, k));
      this.pines.setMatrixAt(i, m);
    }
    this.pines.count = n;
    this.pines.instanceMatrix.needsUpdate = true;
    this.pieces.clear();
    for (const p of base.pieces) {
      const model = this.pieceModel(p.piece, p.rot, p.level, p.hp / PIECES[p.piece].hp);
      const [w, h] = footprint(p.piece, p.rot);
      model.position.set(p.x + w / 2, 0, p.y + h / 2);
      this.pieces.add(model);
    }
  }

  pieceModel(piece: PieceId, rot: number, level = 1, health = 1): THREE.Object3D {
    const def = PIECES[piece];
    const g = new THREE.Group();
    const color = new THREE.Color(piece === 'caravan' ? 0xd8d0b8 : KIND_COLOR[def.kind]).multiplyScalar(health > 0.5 ? 1 : 0.6);
    const material = this.material(color.getHex());
    const [w, h] = footprint(piece, rot);
    g.position.set(-w / 2, 0, -h / 2);
    for (const c of pieceCells(piece, 0, 0, rot)) {
      const tall = (BLOCK[c.tile] ?? 1) * (def.kind === 'building' ? 1.25 + (level - 1) * 0.25 : 1);
      const lift = (c.elev ?? 0) * STOREY;
      const mesh = new THREE.Mesh(new THREE.BoxGeometry(0.96, c.elev ? 0.25 : tall, 0.96), material);
      mesh.position.set(c.dx + 0.5, c.elev ? lift : tall / 2, c.dy + 0.5);
      mesh.castShadow = mesh.receiveShadow = true;
      g.add(mesh);
      if (c.elev) {
        // The deck's legs.
        const leg = new THREE.Mesh(new THREE.BoxGeometry(0.12, lift, 0.12), material);
        leg.position.set(c.dx + 0.5, lift / 2, c.dy + 0.5);
        g.add(leg);
      }
    }
    // Centred on its footprint: the group inside is offset by half of it.
    const out = new THREE.Group();
    out.add(g);
    return out;
  }

  private material(hex: number): THREE.MeshStandardMaterial {
    let m = this.materials.get(hex);
    if (!m) this.materials.set(hex, (m = new THREE.MeshStandardMaterial({ color: hex, roughness: 0.85 })));
    return m;
  }
}
