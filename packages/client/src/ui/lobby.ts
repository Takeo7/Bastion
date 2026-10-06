import * as THREE from 'three';
import { MISSION_NAMES, type MissionKind, type PlayerInfo, type SkirmishMap, type Slot } from '@bastion/engine';
import { inviteLinks, type NetStatus } from '../net';
import { GeoscapeSet } from '../strategy/geoscape';
import { Stage } from '../strategy/stage';
import '../strategy/strategy.css';
import { append, clear, h } from './dom';
import { graphicsSettings } from './settingsPanel';

export interface LobbyHandlers {
  onJoin(name: string): void;
  onStart(mission: MissionKind, map: SkirmishMap, veterans: boolean): void;
  onNewCampaign(): void;
  onContinueCampaign(): void;
}

const MISSIONS: { id: MissionKind; text: string }[] = [
  { id: 'elimination', text: 'Abatid a todos los hostiles del mapa.' },
  { id: 'recovery', text: 'Recuperad los datos y evacuad antes de que se acabe el tiempo.' },
  { id: 'sabotage', text: 'Derribad el retransmisor antes de que termine la transmisión.' },
  { id: 'hack', text: 'Hackead el terminal. Si falla, salta la alarma.' },
  { id: 'rescue', text: 'Llegad hasta el VIP y sacadlo con vida.' },
];

const MAPS: { id: SkirmishMap; name: string; text: string }[] = [
  { id: 'city', name: 'Ciudad', text: 'Edificios, azoteas y calles' },
  { id: 'wilds', name: 'Afueras', text: 'Bosque, cabañas y colinas' },
  { id: 'facility', name: 'Imprenta', text: 'Cadenas de impresión y archivos' },
  { id: 'plaza', name: 'Distrito Comercial', text: 'El mapa hecho a mano' },
];

/**
 * Main menu, XCOM-style: the Earth turning in the background and a column of
 * options. Skirmish settings open in their own panel.
 */
export class Lobby {
  private readonly root = h('div.menu');
  private readonly stage = new Stage();
  private readonly column = h('div.menu-column');
  private readonly squadBox = h('div.menu-squad.x-panel');
  private readonly errorEl = h('div.menu-error');
  private joined = false;
  private status: NetStatus = 'closed';
  private players: PlayerInfo[] = [];
  private mySlot: Slot | null = null;
  private savedCampaign: { day: number; doom: number } | null = null;
  private page: 'main' | 'skirmish' | 'settings' = 'main';
  private mission: MissionKind = 'elimination';
  private map: SkirmishMap = 'city';
  private veterans = true;
  /** Asking before a new campaign replaces the saved one, or starts without the partner. */
  private confirming = false;

  constructor(container: HTMLElement, private readonly handlers: LobbyHandlers, private defaultName: string) {
    const globe = new GeoscapeSet(() => {});
    this.stage.add('geo', globe);
    const shot = globe.shot();
    void this.stage.show('geo', { ...shot, pos: shot.pos.clone().add(new THREE.Vector3(-4.2, 0, 1)), target: shot.target.clone().add(new THREE.Vector3(-4.2, 0, 0)) }, true);
    this.root.append(this.stage.element, h('div.menu-shade'), this.column, this.squadBox);
    container.append(this.root);
    this.render();
  }

  join(name: string): void {
    this.joined = true;
    this.handlers.onJoin(name);
    this.render();
  }

  update(players: PlayerInfo[], mySlot: Slot | null, savedCampaign: { day: number; doom: number } | null = null): void {
    this.players = players;
    this.mySlot = mySlot;
    this.savedCampaign = savedCampaign;
    this.render();
  }

  setStatus(status: NetStatus): void {
    this.status = status;
    this.render();
  }

  error(message: string): void {
    this.errorEl.textContent = message;
  }

  show(): void {
    this.root.classList.remove('hidden');
  }

  hide(): void {
    this.root.classList.add('hidden');
  }

  private render(): void {
    clear(this.column);
    this.column.append(h('div.menu-title', {}, h('h1', {}, 'BASTIÓN'), h('p', {}, 'Táctica por turnos cooperativa')));
    if (!this.joined) {
      const input = h('input.menu-input', { value: this.defaultName, maxLength: 20, placeholder: 'Tu nombre', autofocus: true });
      const submit = () => {
        const name = input.value.trim();
        if (name) this.join(name);
      };
      input.addEventListener('keydown', (e) => e.key === 'Enter' && submit());
      append(this.column, h('label.menu-label', {}, 'Nombre de comandante'), input, h('button.menu-item', { onclick: submit }, 'Entrar'), this.errorEl);
      this.squadBox.classList.add('hidden');
      queueMicrotask(() => input.focus());
      return;
    }
    this.squadBox.classList.remove('hidden');
    this.renderSquad();
    if (this.page === 'skirmish') this.renderSkirmish();
    else if (this.page === 'settings') this.renderSettings();
    else this.renderMain();
    this.column.append(this.errorEl);
    this.renderConfirm();
  }

  /** In-game confirmation (replaces the browser's confirm()). */
  private renderConfirm(): void {
    this.root.querySelector('.menu-modal')?.remove();
    const saved = this.savedCampaign;
    const alone = this.players.filter((p) => p.connected).length < 2;
    if (!saved && !alone) this.confirming = false;
    if (!this.confirming) return;
    const close = () => {
      this.confirming = false;
      this.render();
    };
    this.root.append(
      h(
        'div.menu-modal',
        {},
        h(
          'div.x-panel',
          {},
          h('div.alert-kicker', {}, 'Nueva campaña'),
          h('h2.x-title', {}, saved ? '¿Empezar de cero?' : '¿Empezar sin tu compañero?'),
          saved ? h('p.x-text', {}, `La campaña guardada (día ${saved.day}) se sustituye por una nueva.`) : null,
          alone ? h('p.x-text', {}, 'Tu compañero aún no ha entrado. Puedes empezar igualmente: cuando entre, tomará el mando de 4 soldados.') : null,
          h(
            'div.alert-actions',
            {},
            h('button.x-btn', { onclick: close }, alone && !saved ? 'Esperar' : 'Cancelar'),
            h(
              'button.x-btn.primary',
              {
                onclick: () => {
                  this.confirming = false;
                  this.render();
                  this.handlers.onNewCampaign();
                },
              },
              saved ? 'Empezar de cero' : 'Empezar',
            ),
          ),
        ),
      ),
    );
  }

  private canPlay(): boolean {
    return this.status === 'open' && this.players.some((p) => p.connected);
  }

  private renderMain(): void {
    const saved = this.savedCampaign;
    const ok = this.canPlay();
    append(
      this.column,
      saved ? h('button.menu-item.primary', { disabled: !ok, onclick: () => this.handlers.onContinueCampaign() }, 'Continuar campaña', h('small', {}, `Día ${saved.day} · Proyecto ${saved.doom}/12`)) : null,
      h(
        'button.menu-item',
        {
          disabled: !ok,
          onclick: () => {
            const alone = this.players.filter((p) => p.connected).length < 2;
            if (!saved && !alone) return this.handlers.onNewCampaign();
            this.confirming = true;
            this.render();
          },
        },
        'Nueva campaña',
        h('small', {}, 'La base compartida, de principio a fin'),
      ),
      h(
        'button.menu-item',
        {
          disabled: !ok,
          onclick: () => {
            this.page = 'skirmish';
            this.render();
          },
        },
        'Escaramuza',
        h('small', {}, 'Una misión suelta, sin consecuencias'),
      ),
      h(
        'button.menu-item',
        {
          onclick: () => {
            this.page = 'settings';
            this.render();
          },
        },
        'Ajustes',
        h('small', {}, 'Gráficos y rendimiento'),
      ),
      ok ? null : h('p.menu-hint', {}, 'Conectando con el servidor…'),
    );
  }

  private renderSettings(): void {
    append(
      this.column,
      h('div.x-panel.menu-panel', {}, h('div.x-head', {}, 'Ajustes'), graphicsSettings(() => this.render())),
      h(
        'button.menu-item.back',
        {
          onclick: () => {
            this.page = 'main';
            this.render();
          },
        },
        'Volver',
      ),
    );
  }

  private renderSkirmish(): void {
    const option = <T extends string | boolean>(current: T, value: T, set: (v: T) => void, name: string, text: string) =>
      h(
        `button.menu-option${current === value ? '.on' : ''}`,
        {
          onclick: () => {
            set(value);
            this.render();
          },
        },
        h('b', {}, name),
        h('small', {}, text),
      );
    const connected = this.players.filter((p) => p.connected).length;
    append(
      this.column,
      h(
        'div.x-panel.menu-panel',
        {},
        h('div.x-head', {}, 'Misión'),
        h('div.menu-options', {}, ...MISSIONS.map((m) => option(this.mission, m.id, (v) => (this.mission = v), MISSION_NAMES[m.id], m.text))),
        h('div.x-head', {}, 'Mapa'),
        h('div.menu-options.two', {}, ...MAPS.map((m) => option(this.map, m.id, (v) => (this.map = v), m.name, m.text))),
        h('div.x-head', {}, 'Escuadra'),
        h(
          'div.menu-options.two',
          {},
          option(this.veterans, true, (v) => (this.veterans = v), 'Veteranos', 'Sargentos a capitanes con habilidades'),
          option(this.veterans, false, (v) => (this.veterans = v), 'Novatos', 'Sin rango ni habilidades'),
        ),
      ),
      h(
        'button.menu-item.primary',
        { disabled: !this.canPlay(), onclick: () => this.handlers.onStart(this.mission, this.map, this.veterans) },
        connected < 2 ? 'Empezar en solitario' : 'Empezar escaramuza',
        h('small', {}, connected < 2 ? 'Controlarás a todos los soldados' : 'Cada uno manda a la mitad'),
      ),
      h(
        'button.menu-item.back',
        {
          onclick: () => {
            this.page = 'main';
            this.render();
          },
        },
        'Volver',
      ),
    );
  }

  /** Who is in the room, and how the partner joins. */
  private renderSquad(): void {
    clear(this.squadBox);
    const seats = [0, 1].map((slot) => {
      const p = this.players.find((pl) => pl.slot === slot);
      return h(
        `div.menu-seat${p ? '' : '.empty'}${p && !p.connected ? '.offline' : ''}`,
        { style: { '--accent': p?.color ?? '#3a4652' } },
        h('span.dot'),
        h('b', {}, p ? `${p.name}${slot === this.mySlot ? ' (tú)' : ''}` : 'Esperando jugador…'),
        h('small', {}, p ? (p.connected ? 'Conectado' : 'Desconectado') : ''),
      );
    });
    append(
      this.squadBox,
      h('div.x-head', {}, this.status === 'open' ? 'Comandantes' : 'Conectando…'),
      ...seats,
      ...this.invite(),
    );
  }

  /** How the partner joins: this game's network addresses (never localhost, which only works here). */
  private invite(): HTMLElement[] {
    if (this.players.filter((p) => p.connected).length >= 2) return [];
    const links = inviteLinks();
    if (!links.length) return [h('p.menu-hint', {}, 'Tu compañero necesita la dirección de red de este ordenador: está en la ventana del servidor.')];
    return [
      h('p.menu-hint', {}, 'Tu compañero entra abriendo en su navegador:'),
      ...links.map((l) => h('div.menu-invite', {}, h('small', {}, l.label), h('code.menu-address', {}, l.url))),
    ];
  }
}
