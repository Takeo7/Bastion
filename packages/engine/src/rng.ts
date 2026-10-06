/**
 * Seeded sfc32 PRNG. Only the authoritative host rolls dice; clients receive
 * outcomes inside events, so they never need the RNG.
 */
export class Rng {
  private s: [number, number, number, number];

  constructor(seed: number | [number, number, number, number]) {
    if (Array.isArray(seed)) {
      this.s = [...seed];
    } else {
      this.s = [0x9e3779b9, 0x243f6a88, 0xb7e15162, seed >>> 0];
      for (let i = 0; i < 16; i++) this.next();
    }
  }

  /** Float in [0, 1). */
  next(): number {
    let [a, b, c, d] = this.s;
    let t = (a + b) | 0;
    a = b ^ (b >>> 9);
    b = (c + (c << 3)) | 0;
    c = (c << 21) | (c >>> 11);
    d = (d + 1) | 0;
    t = (t + d) | 0;
    c = (c + t) | 0;
    this.s = [a, b, c, d];
    return (t >>> 0) / 4294967296;
  }

  /** Integer in [0, n). */
  int(n: number): number {
    return Math.floor(this.next() * n);
  }

  /** Integer in [min, max]. */
  range(min: number, max: number): number {
    return min + this.int(max - min + 1);
  }

  pick<T>(items: readonly T[]): T {
    return items[this.int(items.length)]!;
  }

  get state(): [number, number, number, number] {
    return [...this.s];
  }
}
