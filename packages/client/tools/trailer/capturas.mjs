// The Bastión's screens for the store page and the trailer's shot 6
// (docs/TRAILER.md, «Capturas»): the real game at 1920×1080 in an Electron
// window, each screen opened through the client's debug handles (?debug) and
// captured whole, the 3D and the interface together. UI's.
//
//   npx electron packages/client/tools/trailer/capturas.mjs --token <seat> [--companero <the other seat>] [--url http://localhost:3360/] [--solo arbol,armeria] [--cerca 13] [--escala 2]
//
// --cerca 14: how close the camera comes to the soldier with the backpack (the game's distance);
// --hacia 0.4: the frame's middle that far from the soldier towards the nearest of the dead; --giro 1: the camera turned a quarter.
// The defaults frame Gameplay's showcase mission: the squad on the road, two of the dead in sight.
// --escala 2: the same layout at 3840×2160, into capturas/2x/ (the trailer's panels).
//
// The page must reach a server whose save gives that token a seat: Gameplay's
// showcase saves (tools/trailer/escaparate.ts), base.json for the Bastión's
// screens and mision.json for the backpack. Out: .dev-snapshots/trailer/capturas/<name>.png

import { app, BrowserWindow } from 'electron';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const SNAPSHOTS = resolve(dirname(fileURLToPath(import.meta.url)), '../../../../.dev-snapshots/trailer/capturas');
const W = 1920;
const H = 1080;

function arg(name, fallback) {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 ? process.argv[i + 1] : fallback;
}
const URL_ = new URL(arg('url', 'http://localhost:3360/'));
URL_.searchParams.set('debug', '');
const TOKEN = arg('token');
const PARTNER = arg('companero');
const ONLY = arg('solo')?.split(',');
/** The same screens drawn at twice the pixels (3840×2160), for the trailer's panels to lean in close: capturas/2x/. */
const SCALE = Number(arg('escala', 1));
const OUT = SCALE === 1 ? SNAPSHOTS : resolve(SNAPSHOTS, `${SCALE}x`);

/**
 * Each screen: what to do in the page (a function's body, `b` the base screen,
 * `g` the mission, `c` the campaign) and which phase it needs. Each waits for
 * the camera and the panels to settle before the capture.
 */
const SHOTS = {
  // The valley, the map of the campaign, with an operation open: its briefing in the panel on the right.
  valle: {
    phase: 'base',
    run: `
      await b.go('geoscape');
      const o = c.offers.find((x) => x.expiresDay && !x.story) ?? c.offers[0];
      if (o) b.focusOffer(o.id);`,
  },
  // The valley alone.
  'valle-solo': { phase: 'base', run: `await b.go('geoscape');` },
  // The Armería on its arms: the list of every weapon, one of them open with whoever carries it.
  armeria: {
    phase: 'base',
    run: `
      await b.go('engineering');
      b.engTab = 'gear';
      const w = (c.foundWeapons ?? []).find((x) => x.holder) ?? c.foundWeapons?.[0];
      b.engPick = w ? 'f:' + w.id : null;
      b.render();
      document.querySelector('.x-row.selected')?.scrollIntoView({ block: 'center' });`,
  },
  // The tree of the soldier with the most talents, opened from the Cuartel, close on its trunk.
  arbol: {
    phase: 'base',
    run: `
      const s = [...c.soldiers].filter((x) => x.alive && x.cls).sort((x, y) => Object.keys(y.talents).length - Object.keys(x.talents).length)[0];
      b.soldier = s.id;
      await b.go('barracks');
      b.promoting = true;
      b.render();`,
  },
  // The same tree, whole.
  'arbol-entero': {
    phase: 'base',
    run: `
      const s = [...c.soldiers].filter((x) => x.alive && x.cls).sort((x, y) => Object.keys(y.talents).length - Object.keys(x.talents).length)[0];
      b.soldier = s.id;
      await b.go('barracks');
      b.promoting = true;
      b.render();
      await new Promise((done) => setTimeout(done, 500));
      [...document.querySelectorAll('button')].find((x) => /árbol entero/i.test(x.textContent))?.click();`,
  },
  // The Cuartel: a soldier's sheet.
  cuartel: {
    phase: 'base',
    run: `
      b.soldier = c.soldiers.find((x) => x.alive && x.cls).id;
      await b.go('barracks');`,
  },
  // In the mission: whoever carries the most, selected and centred, the camera close; its sheet shows the backpack.
  mochila: { phase: 'mission', run: `${carrier()}` },
  // The same, with what can be done with the first thing in the backpack open.
  'mochila-menu': {
    phase: 'mission',
    run: `
      ${carrier()}
      document.querySelector('.pack-slot-btn')?.click();`,
  },
};

/** In the mission, the briefing closed and the soldier with the fullest backpack selected, the camera close on it. */
function carrier() {
  return `
    for (let i = 0; i < 40 && !document.querySelector('.mission-intro.open:not(.out)'); i++) await new Promise((done) => setTimeout(done, 250));
    document.querySelector('.mission-intro.open:not(.out)')?.click();
    await new Promise((done) => setTimeout(done, 1000));
    const u = g.state.units.filter((x) => x.team === 'xcom' && x.alive && x.owner === g.mySlot).sort((x, y) => (y.backpack?.length ?? 0) - (x.backpack?.length ?? 0))[0];
    g.select(u.id, true);
    // Framed between the soldier and the nearest of the dead (--hacia, 0 on the soldier), turned --giro quarter turns.
    const dead = g.state.units.filter((x) => x.team === 'alien' && x.alive).sort((x, y) => Math.hypot(x.pos.x - u.pos.x, x.pos.y - u.pos.y) - Math.hypot(y.pos.x - u.pos.x, y.pos.y - u.pos.y))[0];
    const k = dead ? ${Number(arg('hacia', 0.4))} : 0;
    if (k) g.rig.focus(g.rig.target.clone().set(u.pos.x + (dead.pos.x - u.pos.x) * k + 0.5, 0, u.pos.y + (dead.pos.y - u.pos.y) * k + 0.5));
    // Turned once per page: both shots of the mission share the camera.
    if (!window.__turned) for (let i = 0; i < ${Number(arg('giro', 1))}; i++) g.rig.rotate(1);
    window.__turned = true;
    g.rig.cinematicZoom(${Number(arg('cerca', 14))});
    await new Promise((done) => setTimeout(done, 2000));`;
}

app.commandLine.appendSwitch('force-device-scale-factor', '1');
app.commandLine.appendSwitch('autoplay-policy', 'no-user-gesture-required');

const sleep = (ms) => new Promise((done) => setTimeout(done, ms));

async function until(win, expr, ms = 30000) {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    if (await win.webContents.executeJavaScript(`!!(${expr})`).catch(() => false)) return;
    await sleep(250);
  }
  throw new Error(`No llega: ${expr}`);
}

/** A seat's token, a name and «alto», in the window's storage, before the game reads them. */
async function seat(win, token, name) {
  await win.loadURL(URL_.href);
  await win.webContents.executeJavaScript(`
    localStorage.setItem('bastion.token', ${JSON.stringify(token)});
    localStorage.setItem('bastion.name', ${JSON.stringify(name)});
    localStorage.setItem('bastion.graphics', JSON.stringify({ fps: 0, effects: 'high', shadows: true, resolution: 1, showFps: false, uiScale: 1, combatSpeed: 'normal', v: 2 }));
    localStorage.setItem('bastion.quality', JSON.stringify({ on: false, resolution: 0, ao: 0, bloom: 0, effectsChosen: true }));
  `);
}

async function main() {
  if (!TOKEN) throw new Error('Falta --token: el de un asiento de la partida.');
  await app.whenReady();
  // At twice the pixels, a window twice the size at a zoom of 2: the page still lays out at 1920×1080.
  // A session in memory: no zoom nor storage kept from a run before.
  const win = new BrowserWindow({ width: W * SCALE, height: H * SCALE, useContentSize: true, show: false, enableLargerThanScreen: true, webPreferences: { partition: 'capturas', backgroundThrottling: false, zoomFactor: SCALE } });
  win.webContents.setAudioMuted(true);
  await seat(win, TOKEN, 'Ana');
  await win.loadURL(URL_.href);
  // The radio's tips stay out of the store's pictures.
  await win.webContents.insertCSS('.radio { display: none !important; }');
  // With a name remembered the client goes straight in (main.ts), and the token takes its seat.
  await until(win, `window.__bastion?.base || window.__bastion?.game`);
  // The partner, connected after (whoever comes first keeps the command), in a window and a storage of its own.
  const partner = PARTNER ? new BrowserWindow({ width: 1280, height: 720, show: false, webPreferences: { partition: 'companero', backgroundThrottling: false } }) : null;
  if (partner) {
    partner.webContents.setAudioMuted(true);
    await seat(partner, PARTNER, 'Arturo');
    await partner.loadURL(URL_.href);
    await until(partner, `window.__bastion?.base || window.__bastion?.game`);
  }
  const phase = await win.webContents.executeJavaScript(`window.__bastion.game ? 'mission' : 'base'`);
  mkdirSync(OUT, { recursive: true });
  for (const [name, shot] of Object.entries(SHOTS)) {
    if (ONLY ? !ONLY.includes(name) : shot.phase !== phase) continue;
    // No notices nor reports in front.
    await win.webContents.executeJavaScript(`(async () => {
      const b = __bastion.base, g = __bastion.game, c = __bastion.campaign;
      if (b) {
        b.alerts.length = 0;
        b.newsOpen = false;
        try { localStorage.setItem('bastion.seenReport', b.reportKey(c)); } catch {}
        b.render();
      }
      ${shot.run}
    })()`);
    // The camera's move and the panels' entrance; no connection notice in sight.
    await sleep(3500);
    await until(win, `!document.querySelector('.connection.show')`, 20000);
    const image = await win.webContents.capturePage({ x: 0, y: 0, width: W * SCALE, height: H * SCALE }, { stayHidden: true });
    const size = image.getSize();
    const png = (size.width === W * SCALE ? image : image.resize({ width: W * SCALE, height: H * SCALE, quality: 'best' })).toPNG();
    writeFileSync(resolve(OUT, `${name}.png`), png);
    console.log(`■ ${name}.png · ${size.width}×${size.height}`);
  }
  app.quit();
}

main().catch((e) => {
  console.error(e);
  app.exit(1);
});
