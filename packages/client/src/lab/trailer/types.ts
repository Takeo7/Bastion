// What the trailer page (main.ts, Red's) expects of the shots (Arte's) and of
// the motion graphics (UI's). docs/TRAILER.md.

/**
 * One shot: shots/<id>.ts, registered in shots/index.ts and placed in time in
 * script.ts. The clock runs in real time during setup; during the take it
 * moves exactly one frame (1/60 s) at a time.
 */
export interface Shot {
  id: string;
  /** Seconds. */
  duration: number;
  /**
   * Builds the scene inside `stage` (1920×1080 CSS pixels; every canvas in it
   * is captured, in order and in place; HTML is not) and resolves once it is
   * ready to draw. A shot of the motion graphics alone leaves it empty.
   */
  setup(stage: HTMLElement): Promise<void>;
  /** Before each frame is drawn, `t` seconds into the shot: the camera, the moves. */
  frame?(t: number): void;
  teardown?(): void;
}

/**
 * The motion graphics: motion.ts exports `motionLayer()`, which returns this.
 * Its canvas is what gets captured: each frame it copies the shot's image
 * (`scene`, composed by the page; absent for a shot with no scene) and draws
 * on top. Every cut between shots happens under something it draws (black or
 * a wipe), so the clips join without crossfades.
 */
export interface MotionLayer {
  /** 1920×1080. */
  canvas: HTMLCanvasElement;
  /** Fonts (document.fonts.load) and images, before the first frame. */
  setup?(): Promise<void>;
  /** `t`: seconds into `shot`; where the shot sits in the trailer is in script.ts. */
  draw(shot: string, t: number, scene?: HTMLCanvasElement): void;
}
