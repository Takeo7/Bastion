// The hex board of Fase 10 (strategy/hexBoard.ts) and the experience of a
// soldier's sheet (strategy/skillSheet.ts), on their own, with three soldiers
// made up from the engine's rules (skills.ts): a recruit, one with points to
// spend and a seasoned one with its board half full. arbol.html shows it:
// `?s=1…3` picks the soldier, `?ajeno` shows it as the partner's (nothing to
// unlock), `?iconos` the 169 icons on a sheet, by sector and ring, to check
// them. Unlocking here changes the made-up soldier only.
import { BOARD, barsCost, NO_GROWTH, PLAYER_COLORS, randomBoard, Rng, unlockBlocker, type CellId, type Skills } from '@bastion/engine';
import { applyUiTheme } from '../../ui/theme';
import '../../strategy/strategy.css';
import '../../strategy/hexBoard.css';
import { h } from '../../ui/dom';
import { boardIconStyle } from '../../strategy/boardIcons';
import { soldierPortrait } from '../../strategy/portraits';
import { skillsBlock, treePanel, type SkillsOf } from '../../strategy/skillSheet';

const bars = (shooting: number, melee: number, throwing: number, medicine: number, searching: number, fitness: number, extra = 0): Skills => ({
  shooting: barsCost(shooting) + extra,
  melee: barsCost(melee) + extra,
  throwing: barsCost(throwing),
  medicine: barsCost(medicine),
  searching: barsCost(searching),
  fitness: barsCost(fitness) + extra,
});

function seasoned(): SkillsOf {
  const skills = bars(5, 3, 2, 2, 4, 3, 6);
  // Half its points spent at random, the rest to spend here.
  const board = randomBoard(skills, new Rng(23)).slice(0, 9);
  return { id: 'lab-3', name: 'Erik Nilsson', skills, growth: { shooting: 34, melee: 21, throwing: 13, medicine: 15, searching: 29, fitness: 22 }, board };
}

const SOLDIERS: SkillsOf[] = [
  { id: 'lab-1', name: 'Nora Vidal', skills: bars(1, 0, 0, 1, 0, 0, 4), growth: { ...NO_GROWTH, shooting: 7, medicine: 6 }, board: [], female: true },
  { id: 'lab-2', name: 'Ana Ríos', skills: bars(2, 1, 1, 0, 1, 1, 3), growth: { shooting: 13, melee: 8, throwing: 5, medicine: 0, searching: 9, fitness: 6 }, board: ['steadyHand'], female: true },
  seasoned(),
];

const params = new URLSearchParams(location.search);
const which = Math.min(SOLDIERS.length, Math.max(1, Number(params.get('s') ?? 2))) - 1;
const editable = !params.has('ajeno');
const s = SOLDIERS[which]!;

applyUiTheme();
document.body.style.background = '#0b0d0e';

/** The 169 icons, sector by sector and ring by ring, with their names: none may be missing or look alike. */
function iconSheet(): void {
  const cell = (id: CellId, name: string, line: string) => {
    const icon = h('span.lab-icon', {}, h('i', { style: boardIconStyle(id, 'dark') }));
    return h(`div.lab-cell.sk-${line}`, { title: id }, icon, h('small', {}, name));
  };
  const style = h('style', {}, `
    .lab-sheet { display: grid; grid-template-columns: repeat(14, 1fr); gap: 6px; padding: 14px; color: #ddd; font: 11px Barlow, sans-serif; }
    .lab-cell { display: grid; justify-items: center; gap: 3px; text-align: center; }
    .lab-icon { display: grid; place-items: center; width: 52px; height: 52px; clip-path: polygon(50% 0,100% 25%,100% 75%,50% 100%,0 75%,0 25%); background: var(--cc); color: #0d0f10; }
    .lab-icon i { width: 34px; height: 34px; }
  `);
  const sorted = [...BOARD].sort((a, b) => a.sector - b.sector || a.ring - b.ring);
  document.body.replaceChildren(style, h('div.lab-sheet', {}, ...sorted.map((c) => cell(c.id, c.name, c.line ?? 'core'))));
}

function render(): void {
  const portrait = soldierPortrait('rookie', PLAYER_COLORS[0], s.female ? { sex: 'f' } as never : undefined, s.id);
  const tabs = h(
    'div',
    { style: { display: 'flex', gap: '6px', padding: '10px 14px' } },
    ...SOLDIERS.map((x, i) => h(`a.x-btn.small${i === which ? '.primary' : ''}`, { href: `?s=${i + 1}${editable ? '' : '&ajeno'}` }, x.name)),
    h('a.x-btn.small', { href: `?s=${which + 1}${editable ? '&ajeno' : ''}` }, editable ? 'Verlo como el compañero' : 'Verlo como suyo'),
    h('a.x-btn.small', { href: '?iconos' }, 'Los iconos'),
  );
  const sheet = h(
    'div.x-panel',
    { style: { width: '330px', display: 'grid', gap: '6px', alignContent: 'start' } },
    h('div.alert-kicker', {}, 'FICHA · CUARTEL'),
    h('h2.x-title', {}, s.name),
    ...skillsBlock(s, editable, () => undefined),
  );
  const panel = treePanel({
    s,
    editable,
    owner: 'Ana',
    portrait,
    onUnlock: (cell) => {
      if (unlockBlocker(s.skills, s.board, cell)) return;
      s.board = [...s.board, cell];
      render();
    },
    onClose: () => undefined,
  });
  const modal = h('div.strat-modal.show', { style: { position: 'relative', inset: 'auto', display: 'grid', placeItems: 'start center', padding: '0 14px 14px' } }, panel);
  document.body.replaceChildren(tabs, h('div.strat', { style: { position: 'relative', display: 'flex', gap: '14px', alignItems: 'start', padding: '0 14px' } }, sheet, modal));
}

if (params.has('iconos')) iconSheet();
else render();
