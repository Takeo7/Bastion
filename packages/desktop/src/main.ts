// Bastión's desktop app (Electron): one window with the game and, for the
// host, the game's own server inside, so nobody needs a terminal. The server is
// packages/server bundled into one file (scripts/build.mjs) and run by
// Electron's Node in a utility process: a long AI turn or a save never stalls
// the window. The partner joins from their own app or from Chrome at the
// address the host's lobby shows. Plain http: both ends are Chromium, which
// runs it at full speed, so there are no certificates (only Safari needed https).
import { app, BrowserWindow, ipcMain, Menu, shell, utilityProcess, type IpcMainEvent, type IpcMainInvokeEvent, type MenuItemConstructorOptions, type UtilityProcess } from 'electron';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:net';
import { join } from 'node:path';
import { listen, type LanGame } from '../../server/src/lan';

// Saves, settings and red.log in a folder with a plain name: …/Application Support/Bastion, %APPDATA%\Bastion.
// BASTION_DATOS points it elsewhere (tests, a second copy on the same machine).
app.setPath('userData', process.env.BASTION_DATOS || join(app.getPath('appData'), 'Bastion'));

/**
 * Measuring only: frames as fast as the machine can draw them, with no vsync
 * (BASTION_SIN_TOPE=1, or --sin-tope). Never on for players.
 */
const UNCAPPED = process.env.BASTION_SIN_TOPE === '1' || process.argv.includes('--sin-tope');
if (UNCAPPED) {
  app.commandLine.appendSwitch('disable-frame-rate-limit');
  app.commandLine.appendSwitch('disable-gpu-vsync');
}

/** The built client and server: next to the app once packaged (extraResources), next to this file while developing. */
const RESOURCES = app.isPackaged ? process.resourcesPath : __dirname;
const CLIENT = join(RESOURCES, 'client');
const SERVER = join(RESOURCES, 'server', 'server.mjs');
/** The web game's port: a partner used to it finds the app's game at the same address. BASTION_PUERTO for tests. */
const FIRST_PORT = Number(process.env.BASTION_PUERTO) || 3000;
const SETTINGS = (): string => join(app.getPath('userData'), 'escritorio.json');

let win: BrowserWindow | null = null;
let server: UtilityProcess | null = null;
let serverPort = 0;
/** Games heard on the local network while the start screen is up, and the stop for that listening. */
let games: LanGame[] = [];
let stopListening: (() => void) | null = null;

// ------------------------------------------------------------------ server

/** Whether `port` can be listened on, on every address or only on `host`. */
function bindable(port: number, host?: string): Promise<boolean> {
  return new Promise((resolve) => {
    const s = createServer();
    s.once('error', () => resolve(false));
    s.listen({ port, host }, () => s.close(() => resolve(true)));
  });
}

/**
 * A port nothing is listening on, from `first` up. 127.0.0.1 is tried too: a
 * program holding only that address (a container's forwarded port) leaves the
 * wildcard free, and the window's localhost would then reach it, not the game.
 */
async function freePort(first: number): Promise<number> {
  for (let port = first; port <= first + 40; port++) if ((await bindable(port)) && (await bindable(port, '127.0.0.1'))) return port;
  throw new Error('No hay ningún puerto libre.');
}

/** Waits until `url` answers, for up to `ms`. */
async function answers(url: string, ms: number): Promise<boolean> {
  const until = Date.now() + ms;
  while (Date.now() < until) {
    try {
      const res = await fetch(url, { signal: AbortSignal.timeout(1500) });
      if (res.ok) return true;
    } catch {
      /* not yet */
    }
    await new Promise((r) => setTimeout(r, 150));
  }
  return false;
}

/** Starts the game's server (once) and returns its port. The save and red.log live in userData. */
async function startServer(): Promise<number> {
  if (server) return serverPort;
  const port = await freePort(FIRST_PORT);
  const data = app.getPath('userData');
  mkdirSync(data, { recursive: true });
  // No https port: no certificate to make or to accept. Announced on the local network, so partners find it.
  const args = ['--port', String(port), '--https-port', '0', '--save', join(data, 'save.json'), '--client', CLIENT, '--anunciar'];
  const child = utilityProcess.fork(SERVER, args, { serviceName: 'Bastión (servidor)', stdio: 'pipe' });
  child.stdout?.on('data', (d: Buffer) => process.stdout.write(d));
  child.stderr?.on('data', (d: Buffer) => process.stderr.write(d));
  child.once('exit', (code) => {
    if (server !== child) return;
    server = null;
    serverPort = 0;
    // Quitting: the app goes too, there is no start screen to come back to.
    if (quitting) return;
    // The host's game has lost its server: back to the start, saying so.
    if (win && !win.isDestroyed() && win.webContents.getURL().startsWith(`http://localhost:${port}`)) {
      void showLauncher(`El servidor de la partida se ha cerrado (código ${code}). La partida está guardada: vuelve a abrirla.`);
    }
  });
  server = child;
  serverPort = port;
  if (!(await answers(`http://127.0.0.1:${port}/`, 15_000))) {
    child.kill();
    throw new Error('El servidor de la partida no arranca.');
  }
  return port;
}

// ------------------------------------------------------------------ window

interface Settings {
  /** The address last joined: the start screen offers it again. */
  last?: string;
}

function readSettings(): Settings {
  try {
    return JSON.parse(readFileSync(SETTINGS(), 'utf8')) as Settings;
  } catch {
    return {};
  }
}

function writeSettings(s: Settings): void {
  try {
    mkdirSync(app.getPath('userData'), { recursive: true });
    writeFileSync(SETTINGS(), JSON.stringify(s, null, 2));
  } catch {
    /* only a convenience */
  }
}

/** The start screen: host a game or join one. `note` says why we are back, if something went wrong. */
async function showLauncher(note = ''): Promise<void> {
  if (!win || win.isDestroyed()) return;
  listenForGames();
  await win.loadFile(join(__dirname, 'launcher.html'), { query: note ? { aviso: note } : {} });
}

/** While the start screen is up, the games announced on the network go to it (server lan.ts); not this app's own. */
function listenForGames(): void {
  if (stopListening) return;
  stopListening = listen(
    (heard) => {
      games = heard;
      if (win && !win.isDestroyed() && win.webContents.getURL().startsWith('file:')) win.webContents.send('bastion:games', games);
    },
    (port) => port === serverPort,
  );
}

function stopListeningForGames(): void {
  stopListening?.();
  stopListening = null;
  games = [];
}

function createWindow(): BrowserWindow {
  const w = new BrowserWindow({
    width: 1600,
    height: 900,
    minWidth: 1024,
    minHeight: 640,
    title: 'Bastión',
    icon: join(__dirname, 'icon.png'),
    backgroundColor: '#0d0f12',
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      sandbox: true,
      nodeIntegration: false,
      spellcheck: false,
    },
  });
  w.once('ready-to-show', () => w.show());
  // Nothing in the game opens windows; a link that would goes to the system's browser.
  w.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https?:/.test(url)) void shell.openExternal(url);
    return { action: 'deny' };
  });
  w.on('closed', () => (win = null));
  // Esc takes the start screen out of full screen: it has no menu of its own for it (playtest 21/10 #29, Ana stuck
  // there). The game's pages keep Esc for their menu, and ask for it themselves where they have none (client net.ts).
  w.webContents.on('before-input-event', (e, input) => {
    if (input.type === 'keyDown' && input.key === 'Escape' && w.isFullScreen() && w.webContents.getURL().startsWith('file:')) {
      e.preventDefault();
      w.setFullScreen(false);
    }
  });
  // The start screen says how to leave full screen while in it.
  const fullscreen = () => {
    if (w.webContents.getURL().startsWith('file:')) w.webContents.send('bastion:fullscreen', w.isFullScreen());
  };
  w.on('enter-full-screen', fullscreen);
  w.on('leave-full-screen', fullscreen);
  return w;
}

/** Only the start screen (a file of the app) may ask to host or join; never a game page from the network. */
function fromLauncher(e: IpcMainInvokeEvent | IpcMainEvent): boolean {
  return e.senderFrame?.url.startsWith('file:') ?? false;
}

/** The game this window opened: its own server's (hosting) or the partner's address (joining). */
let gameOrigin: string | null = null;
/** The app is on its way out (before-quit), waiting for its server to say goodbye. */
let quitting = false;

/**
 * The game's page may only ask to leave (quit, back to the start screen) and
 * whether it is the host's: and only the window's top frame, at the address
 * the app itself opened, never anything else it may have been led to.
 */
function fromGame(e: IpcMainEvent): boolean {
  const frame = e.senderFrame;
  if (!win || win.isDestroyed() || !frame || frame !== win.webContents.mainFrame || !gameOrigin) return false;
  try {
    return new URL(frame.url).origin === gameOrigin;
  } catch {
    return false;
  }
}

// «Salir del juego» (playtest 17/10): the page asks first, and warns a host whose partner is in (UI, lobby.ts).
// Quitting as host closes the server inside (before-quit); every change is already saved (server persistence.ts).
ipcMain.on('bastion:quit', (e) => {
  if (fromGame(e)) app.quit();
});
// With a note, it says on the start screen why we are back (the host closed the game: client net.ts).
ipcMain.on('bastion:toLauncher', (e, note: unknown) => {
  if (fromGame(e)) void showLauncher(typeof note === 'string' ? note.slice(0, 200) : '');
});
// Asked once by the preload, as the page loads: is this window the host's (its server runs here)?
ipcMain.on('bastion:hosting', (e) => {
  e.returnValue = fromGame(e) && server !== null && gameOrigin === `http://localhost:${serverPort}`;
});

/** «192.168.1.20», «192.168.1.20:3002» or a pasted «http://…/» → «192.168.1.20:3000». */
function normalise(address: string): string | null {
  const bare = address.trim().replace(/^[a-z]+:\/\//i, '').replace(/\/.*$/, '');
  if (!/^[\w.-]+(:\d{1,5})?$/.test(bare)) return null;
  return bare.includes(':') ? bare : `${bare}:${FIRST_PORT}`;
}

ipcMain.handle('bastion:info', (e) => {
  if (!fromLauncher(e)) return null;
  return { last: readSettings().last ?? '', version: app.getVersion(), uncapped: UNCAPPED, games, fullscreen: win?.isFullScreen() ?? false };
});

// «Salir de Bastión» on the start screen (playtest 21/10 #29): the app, and the server with it if it hosts.
ipcMain.on('bastion:quitLauncher', (e) => {
  if (fromLauncher(e)) app.quit();
});

// Out of full screen, asked by a page with nothing else for Esc (client net.ts, the host-closed screen).
ipcMain.on('bastion:leaveFullscreen', (e) => {
  if ((fromGame(e) || fromLauncher(e)) && win?.isFullScreen()) win.setFullScreen(false);
});

ipcMain.handle('bastion:host', async (e): Promise<string | null> => {
  if (!fromLauncher(e) || !win) return null;
  try {
    const port = await startServer();
    stopListeningForGames();
    gameOrigin = `http://localhost:${port}`;
    await win.loadURL(`http://localhost:${port}/`);
    return null;
  } catch (err) {
    return err instanceof Error ? err.message : String(err);
  }
});

ipcMain.handle('bastion:join', async (e, address: unknown): Promise<string | null> => {
  if (!fromLauncher(e) || !win) return null;
  const target = typeof address === 'string' ? normalise(address) : null;
  if (!target) return 'Escribe la dirección que aparece en la pantalla de quien aloja la partida; por ejemplo, 192.168.1.20:3000.';
  if (!(await answers(`http://${target}/`, 4000))) {
    return `No responde nadie en ${target}. ¿Tiene abierta la partida (Ser anfitrión) y estáis en la misma red o en Tailscale?`;
  }
  writeSettings({ ...readSettings(), last: target });
  stopListeningForGames();
  gameOrigin = new URL(`http://${target}/`).origin;
  try {
    // `?http`: a host started with `npm start` sends pages from other computers over to its https port, whose
    // certificate the app doesn't take; the app is fast on http.
    await win.loadURL(`http://${target}/?http`);
  } catch (err) {
    // The launcher is already gone and its buttons would never come back: back to it, saying why.
    void showLauncher(`No se pudo abrir la partida de ${target}: ${err instanceof Error ? err.message : String(err)}`);
  }
  return null;
});

// ------------------------------------------------------------------ menu

function buildMenu(): void {
  const mac = process.platform === 'darwin';
  const game: MenuItemConstructorOptions = {
    label: 'Partida',
    submenu: [
      { label: 'Volver al inicio', accelerator: 'CmdOrCtrl+Shift+H', click: () => void showLauncher() },
      { label: 'Recargar', accelerator: 'CmdOrCtrl+R', click: () => win?.webContents.reload() },
      { type: 'separator' },
      { label: 'Pantalla completa', role: 'togglefullscreen' },
      ...(!app.isPackaged || UNCAPPED ? [{ label: 'Herramientas de desarrollo', role: 'toggleDevTools' } as MenuItemConstructorOptions] : []),
      // Said outright, so it works in full screen too, with the menu bar hidden (playtest 21/10 #29).
      ...(mac ? [] : [{ type: 'separator' } as MenuItemConstructorOptions, { label: 'Salir', role: 'quit', accelerator: process.platform === 'win32' ? 'Alt+F4' : 'Ctrl+Q' } as MenuItemConstructorOptions]),
    ],
  };
  // Without an Edit menu, macOS gives text fields no copy and paste.
  const edit: MenuItemConstructorOptions = {
    label: 'Edición',
    submenu: [
      { label: 'Deshacer', role: 'undo' },
      { label: 'Rehacer', role: 'redo' },
      { type: 'separator' },
      { label: 'Cortar', role: 'cut' },
      { label: 'Copiar', role: 'copy' },
      { label: 'Pegar', role: 'paste' },
      { label: 'Seleccionar todo', role: 'selectAll' },
    ],
  };
  const appMenu: MenuItemConstructorOptions = {
    label: 'Bastión',
    submenu: [
      { label: 'Acerca de Bastión', role: 'about' },
      { type: 'separator' },
      { label: 'Ocultar Bastión', role: 'hide' },
      { label: 'Ocultar los demás', role: 'hideOthers' },
      { label: 'Mostrar todo', role: 'unhide' },
      { type: 'separator' },
      { label: 'Salir de Bastión', role: 'quit', accelerator: 'Cmd+Q' },
    ],
  };
  Menu.setApplicationMenu(Menu.buildFromTemplate(mac ? [appMenu, game, edit] : [game, edit]));
}

// ------------------------------------------------------------------ life

// One Bastión at a time: a second launch brings the first one forward (and never starts a second server).
if (!app.requestSingleInstanceLock()) app.quit();
else {
  app.on('second-instance', () => {
    if (!win) return;
    if (win.isMinimized()) win.restore();
    win.focus();
  });
  void app.whenReady().then(async () => {
    app.setAboutPanelOptions({ applicationName: 'Bastión', applicationVersion: app.getVersion() });
    // Packaged, the bundle carries the icon; running from the sources, the Dock would show Electron's.
    if (!app.isPackaged) app.dock?.setIcon(join(__dirname, 'icon.png'));
    buildMenu();
    win = createWindow();
    await showLauncher();
  });
  app.on('activate', () => {
    if (win) return;
    win = createWindow();
    void showLauncher();
  });
  app.on('window-all-closed', () => app.quit());
  app.on('before-quit', (e) => {
    stopListeningForGames();
    const child = server;
    if (!child || quitting) return;
    // As host: the server saves and tells the partner it is closing (server index.ts shutdown) before the app
    // goes. Asked by message, which works on Windows too; never more than a second and a half.
    e.preventDefault();
    quitting = true;
    const go = () => app.quit();
    child.once('exit', go);
    child.postMessage('cerrar');
    setTimeout(() => {
      child.kill();
      go();
    }, 1500);
  });
}
