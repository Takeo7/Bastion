// Tells the host what goes wrong in this browser. The partner's console is on
// another computer, so errors, and the last step before the page stops
// answering, are posted to the server, which prints them in the host's
// terminal.

const URL_REPORT = `${location.origin}/__report`;
let watchdog: Worker | null = null;

/** Sends `text` to the host's terminal. */
export function post(text: string): void {
  try {
    navigator.sendBeacon(URL_REPORT, text);
  } catch {
    /* nowhere to report to */
  }
}

/** The partner's browser: the host's own one (localhost) has its console at hand. */
const REMOTE = !['localhost', '127.0.0.1', '[::1]'].includes(location.hostname);

/**
 * Records a step; if the page then stops answering, the host hears which one
 * it was. In Safari a worker's requests go through the page's thread, so
 * there the watchdog's report only gets out once the page answers again; the
 * partner's browser therefore also sends each step as it happens (a beacon
 * leaves at once, even if the page hangs right after).
 */
export function trace(label: string): void {
  watchdog?.postMessage(label);
  if (REMOTE) post(`paso: ${label}`);
}

// A worker has its own thread: while the page's thread ticks it stays quiet;
// when the ticks stop for 3 s (a hang or a very long task) it reports the
// last step, and says so again once the page answers.
const WATCHDOG = `
let last = 'inicio', seen = Date.now(), told = false, paused = false;
const report = (text) => fetch(${JSON.stringify(URL_REPORT)}, { method: 'POST', body: text, keepalive: true }).catch(() => {});
onmessage = (e) => {
  if (e.data === 'pause' || e.data === 'resume') paused = e.data === 'pause';
  else if (e.data !== 'tick') last = e.data;
  seen = Date.now();
  if (told) {
    told = false;
    report('vuelve a responder (' + last + ')');
  }
};
setInterval(() => {
  if (paused || told || Date.now() - seen < 3000) return;
  told = true;
  report('congelada más de 3 s tras: ' + last);
}, 500);
`;

/**
 * Milliseconds a small integer loop takes: a few with the browser's JavaScript
 * compiler, around a hundred without it (Safari on pages it deems insecure).
 */
function loopMs(): number {
  const a = new Int32Array(1 << 20);
  const t = performance.now();
  for (let i = 1; i < a.length; i++) a[i] = (a[i - 1]! + i) | 0;
  return Math.round(performance.now() - t);
}

export function installReport(): void {
  post(`abre el juego · bucle de prueba ${loopMs()} ms · ${isSecureContext ? 'contexto seguro' : 'contexto NO seguro'} · ${navigator.userAgent}`);
  window.addEventListener('error', (e) => post(`error: ${e.message} (${e.filename}:${e.lineno}:${e.colno})`));
  window.addEventListener('unhandledrejection', (e) => {
    const r: unknown = e.reason;
    post(`promesa rechazada: ${r instanceof Error ? (r.stack ?? r.message) : String(r)}`);
  });
  try {
    watchdog = new Worker(URL.createObjectURL(new Blob([WATCHDOG], { type: 'text/javascript' })));
  } catch {
    return; // no workers: errors are still reported
  }
  setInterval(() => watchdog?.postMessage('tick'), 500);
  // Hidden tabs barely tick: that is not a hang. A tab can also open hidden, with no change to hear.
  const visibility = () => watchdog?.postMessage(document.hidden ? 'pause' : 'resume');
  visibility();
  document.addEventListener('visibilitychange', visibility);
}
