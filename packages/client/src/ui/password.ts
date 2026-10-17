import '@fontsource/barlow/latin-500.css';
import '@fontsource/barlow/latin-600.css';
import '@fontsource/oswald/latin-600.css';
import { WEB_TEXT } from '@bastion/engine';
import { h } from './dom';
import './password.css';

// The game's password, on a website (server access.ts, docs/WEB.md), asked
// by boot.ts before the game loads. Given once, the server answers with a
// cookie the browser keeps for a year, and the page starts again as one that
// may come in. Alone on the page, so it carries its own look (password.css,
// UI's to restyle); the words are Lore's (WEB_TEXT).

export function showPasswordScreen(container: HTMLElement): void {
  const input = h('input.password-input', {
    type: 'password',
    name: 'password',
    autocomplete: 'current-password',
    placeholder: WEB_TEXT.passwordField,
    autofocus: true,
    required: true,
  });
  const error = h('p.password-error', { role: 'alert' });
  const button = h('button.password-button', { type: 'submit' }, WEB_TEXT.enter);
  const form = h(
    'form.password-panel',
    {},
    h('h1.password-title', {}, WEB_TEXT.passwordTitle),
    h('p.password-line', {}, WEB_TEXT.passwordLine),
    // For the browser's password manager: one game, one password.
    h('input', { type: 'text', name: 'username', autocomplete: 'username', value: 'Bastión', hidden: true }),
    input,
    error,
    button,
  );
  const send = async (): Promise<void> => {
    if (!input.value) return;
    button.disabled = true;
    error.textContent = '';
    let status = 0;
    try {
      status = (await fetch('/__access', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ password: input.value }) })).status;
    } catch {
      /* no answer: said below */
    }
    if (status === 204) return location.reload();
    button.disabled = false;
    error.textContent = status === 401 ? WEB_TEXT.wrong : status === 429 ? WEB_TEXT.tooMany : WEB_TEXT.noAnswer;
    input.select();
  };
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    void send();
  });
  container.append(h('div.password-screen', {}, form));
  queueMicrotask(() => input.focus());
}
