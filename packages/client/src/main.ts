import './styles.css';
import type { CampaignState, PlayerInfo, ServerMessage, Slot } from '@bastion/engine';
import { Game } from './game/game';
import { Net, serverUrl, setHosts, storageKey } from './net';
import { StrategyScreen } from './strategy/screen';
import { Lobby } from './ui/lobby';

const NAME_KEY = storageKey('name');

function savedName(): string {
  try {
    return localStorage.getItem(NAME_KEY) ?? '';
  } catch {
    return '';
  }
}

const app = document.getElementById('app')!;
const net = new Net(serverUrl());
let slot: Slot | null = null;
let players: PlayerInfo[] = [];
let game: Game | null = null;
let base: StrategyScreen | null = null;
let campaign: CampaignState | null = null;
/** The dropship take-off; mission traffic is held until it is over. */
let takeoff: Promise<void> | null = null;
const held: ServerMessage[] = [];
const MISSION_TRAFFIC = new Set<ServerMessage['t']>(['snapshot', 'events', 'presence', 'ping', 'rejected']);

const lobby = new Lobby(
  app,
  {
    onJoin: (name) => {
      try {
        localStorage.setItem(NAME_KEY, name);
      } catch {
        /* private mode: the name just will not be remembered */
      }
      net.connect(name);
    },
    onStart: (mission, map, veterans) => net.send({ t: 'start', mission, map, veterans }),
    onNewCampaign: () => net.send({ t: 'newCampaign' }),
    onContinueCampaign: () => net.send({ t: 'continueCampaign' }),
  },
  savedName(),
);

function closeGame(): void {
  game?.destroy();
  game = null;
}

function closeBase(): void {
  base?.destroy();
  base = null;
}

/** The room's phase decides which screen is up: lobby, base or mission. */
function showPhase(phase: 'lobby' | 'base' | 'mission'): void {
  if (phase === 'lobby') {
    closeGame();
    closeBase();
    lobby.show();
    return;
  }
  lobby.hide();
  if (phase !== 'mission') held.length = 0;
  if (phase === 'base') {
    closeGame();
    if (!base && slot !== null) {
      base = new StrategyScreen(app, slot, {
        send: (cmd) => net.send({ t: 'ccmd', cmd }),
        onView: (view) => net.send({ t: 'baseView', view }),
        onLeave: () => net.send({ t: 'backToMenu' }),
      });
    }
    base?.setPlayers(players);
    if (campaign) base?.update(campaign);
  } else if (base) {
    // Take-off: the dropship leaves the hangar; the mission is built once the screen is black.
    const leaving = base;
    base = null;
    takeoff = leaving.launch().then(() => {
      takeoff = null;
      for (const m of held.splice(0)) handle(m);
      leaving.destroy();
    });
  }
}

net.onStatus((status) => {
  lobby.setStatus(status);
  game?.setConnection(status);
});

function handle(m: ServerMessage): void {
  if (takeoff && MISSION_TRAFFIC.has(m.t)) {
    held.push(m);
    return;
  }
  switch (m.t) {
    case 'welcome':
      slot = m.slot;
      setHosts(m.hosts ?? []);
      break;
    case 'room':
      players = m.players;
      lobby.update(players, slot, m.savedCampaign);
      game?.setPlayers(players);
      showPhase(m.phase);
      break;
    case 'campaign':
      campaign = m.state;
      base?.update(m.state);
      break;
    case 'baseView':
      base?.setPartnerView(m.view);
      break;
    case 'snapshot':
      if (slot === null) return;
      closeBase();
      if (game) game.loadSnapshot(m.state);
      else {
        lobby.hide();
        game = new Game(app, net, slot, players, m.state);
        // A fresh mission opens with its briefing.
        if (m.state.seq === 0) game.intro();
      }
      break;
    case 'events':
      game?.onEvents(m.events, m.seq, m.by);
      break;
    case 'presence':
      game?.onPresence(m.slot, m.unit, m.tile);
      break;
    case 'ping':
      game?.onPing(m.slot, m.tile, m.kind);
      break;
    case 'rejected':
      game?.onRejected(m.message);
      base?.toast(m.message);
      break;
    case 'error':
      lobby.error(m.message);
      game?.toast(m.message);
      base?.toast(m.message);
      break;
  }
}

net.onMessage(handle);

if (import.meta.env.DEV || new URLSearchParams(location.search).has('debug')) {
  // Debug handles for the console and automated checks. Frames can be stepped
  // and captured manually, which also works while the tab is hidden.
  const g = () => game as unknown as Record<string, any>;
  const step = (n = 40) => {
    for (let i = 0; i < n; i++) g().frame(0.05, i * 0.05);
    // Same path as the game's frame: post-processing, then labels.
    g().renderer.render();
  };
  const tileFrac = (x: number, y: number): [number, number] => {
    const v = g().renderer.camera.position.clone().set(x + 0.5, 0, y + 0.5).project(g().renderer.camera);
    return [(v.x + 1) / 2, (1 - v.y) / 2];
  };
  const pointer = (type: string, x: number, y: number) => {
    const [fx, fy] = tileFrac(x, y);
    const c = g().renderer.gl.domElement as HTMLCanvasElement;
    const r = c.getBoundingClientRect();
    c.dispatchEvent(new PointerEvent(type, { clientX: r.left + r.width * fx, clientY: r.top + r.height * fy, bubbles: true }));
  };
  Object.assign(window, {
    __bastion: {
      get game() {
        return game;
      },
      get base() {
        return base;
      },
      get campaign() {
        return campaign;
      },
      net,
      step,
      hover: (x: number, y: number) => (pointer('pointermove', x, y), step(1)),
      click: (x: number, y: number) => {
        pointer('pointermove', x, y);
        const [fx, fy] = tileFrac(x, y);
        const c = g().renderer.gl.domElement as HTMLCanvasElement;
        const r = c.getBoundingClientRect();
        c.dispatchEvent(new MouseEvent('click', { clientX: r.left + r.width * fx, clientY: r.top + r.height * fy, bubbles: true }));
      },
      snap: async (name = 'snap', width = 960) => {
        step(1);
        const src = g().renderer.gl.domElement as HTMLCanvasElement;
        const c = document.createElement('canvas');
        c.width = width;
        c.height = Math.round((width * src.height) / src.width);
        c.getContext('2d')!.drawImage(src, 0, 0, c.width, c.height);
        await fetch(`/__snap?name=${name}`, { method: 'POST', body: c.toDataURL('image/jpeg', 0.8) });
        return name;
      },
    },
  });
}

// Returning players (same browser) rejoin straight away.
const remembered = savedName();
if (remembered) lobby.join(remembered);
