// Small helpers the page's modules share.
export const $ = (sel) => document.querySelector(sel);

/** An element with its properties (class, text, on…, attributes) and children (nulls skipped). */
export const el = (tag, props = {}, ...kids) => {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (k === 'class') e.className = v;
    else if (k === 'text') e.textContent = v;
    else if (k.startsWith('on')) e.addEventListener(k.slice(2), v);
    else if (v !== undefined && v !== null && v !== false) e.setAttribute(k, v === true ? '' : v);
  }
  for (const kid of kids.flat()) if (kid !== null && kid !== undefined && kid !== false) e.append(kid);
  return e;
};

/** The server's JSON; its error, in Spanish, as the thrown message. */
export const api = async (path, opts) => {
  const r = await fetch(path, opts);
  if (!r.ok) throw new Error((await r.json().catch(() => ({}))).error ?? r.statusText);
  return r.json();
};

export const q = (o) => new URLSearchParams(o).toString();
export const mb = (bytes) => (bytes ? `${Math.round(bytes / 1e6).toLocaleString('es-ES')} MB` : null);
export const plural = (n, one, many) => `${n.toLocaleString('es-ES')} ${n === 1 ? one : many}`;
