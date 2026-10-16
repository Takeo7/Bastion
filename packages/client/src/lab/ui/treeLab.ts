// The «Árbol de habilidades» of Fase 9 (strategy/skillMatrix.ts) and the
// skills of a soldier's sheet (strategy/skillSheet.ts), on their own, with a
// few soldiers made up from the engine's rules (skills.ts): a recruit, one with
// two pairs to pick and one of them drafted, a seasoned one and one with
// everything. arbol.html shows it; `?s=1…4` picks the soldier, `?ajeno` shows
// it as the partner's (nothing to pick).
import { PLAYER_COLORS, SKILL_XP, type PerkId, type Skills } from '@bastion/engine';
import { applyUiTheme } from '../../ui/theme';
import '../../strategy/strategy.css';
import { h } from '../../ui/dom';
import { soldierPortrait } from '../../strategy/portraits';
import { skillsBlock, treePanel, type SkillsOf } from '../../strategy/skillSheet';

const xp = (shooting: number, melee: number, medicine: number, craft: number): Skills => ({ shooting, melee, medicine, craft });

const SOLDIERS: (SkillsOf & { draft?: PerkId[] })[] = [
  { id: 'lab-1', name: 'Nora Vidal', skills: xp(SKILL_XP[0], 1, SKILL_XP[0], 0), picks: [] },
  { id: 'lab-2', name: 'Ana Ríos', skills: xp(11, 4, 0, 9), picks: [], draft: ['aimedShot'] },
  { id: 'lab-3', name: 'Erik Nilsson', skills: xp(26, 9, 3, 15), picks: ['rapidFire', 'implacable', 'quiet'] },
  { id: 'lab-4', name: 'Dolores Pardo', skills: xp(35, 35, 24, 35), picks: ['aimedShot', 'faceoff', 'bladeCrit', 'elbowRoom', 'revivalProtocol', 'lightHands', 'goodArm', 'patchUp'] },
];

const params = new URLSearchParams(location.search);
const which = Math.min(SOLDIERS.length, Math.max(1, Number(params.get('s') ?? 2))) - 1;
const editable = !params.has('ajeno');
const s = SOLDIERS[which]!;

applyUiTheme();
document.body.style.background = '#0b0d0e';

/** The draft the soldier comes with: laid on as the tree's own clicks would. */
function seed(): void {
  if (!s.draft) return;
  const panel = treePanel({ s, editable, portrait: null, onSave: () => undefined, onClose: () => undefined });
  for (const id of s.draft) panel.querySelector<HTMLElement>(`[data-talent='${id}']`)?.closest('button')?.click();
}

function render(): void {
  const portrait = soldierPortrait('rookie', PLAYER_COLORS[0], undefined, s.id);
  const tabs = h(
    'div',
    { style: { display: 'flex', gap: '6px', padding: '10px 14px' } },
    ...SOLDIERS.map((x, i) => h(`a.x-btn.small${i === which ? '.primary' : ''}`, { href: `?s=${i + 1}${editable ? '' : '&ajeno'}` }, x.name)),
    h('a.x-btn.small', { href: `?s=${which + 1}${editable ? '&ajeno' : ''}` }, editable ? 'Verlo como el compañero' : 'Verlo como suyo'),
  );
  const sheet = h(
    'div.x-panel',
    { style: { width: '320px', display: 'grid', gap: '6px', alignContent: 'start' } },
    h('div.alert-kicker', {}, 'FICHA · CUARTEL'),
    h('h2.x-title', {}, s.name),
    ...skillsBlock(s, editable, () => undefined),
  );
  const panel = treePanel({
    s,
    editable,
    portrait,
    onSave: (picks) => {
      s.picks = picks;
      render();
    },
    onClose: () => undefined,
    toast: (text) => console.info(text),
  });
  const modal = h('div.strat-modal.show', { style: { position: 'relative', inset: 'auto', display: 'grid', placeItems: 'start center', padding: '0 14px 14px' } }, panel);
  document.body.replaceChildren(tabs, h('div.strat', { style: { position: 'relative', display: 'flex', gap: '14px', alignItems: 'start', padding: '0 14px' } }, sheet, modal));
}

seed();
render();
