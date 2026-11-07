// Briques des clips Mue : appli hôte de démonstration, pastille et dialogues recopiés des XAML de Mue.Wpf.
// Textes : Strings.resx de Mue 0.3.0, avec {AppName} = « Notebook ». Données de démonstration uniquement.
import { E, clamp, lerp, prog, env, press, el, style, track } from './engine.js';

export const APP = 'Notebook';
export const FROM = '1.2.0', TO = '1.3.0';

// Symbole Mue (source/symbole.svg) pour la légende.
const FEATHER = `<svg viewBox="0 0 256 256"><defs><mask id="lgb"><rect width="256" height="256" fill="#fff"/><path d="M121 136 L48 100 M121 176 L56 146 M135 104 L210 72" stroke="#000" stroke-width="11"/></mask></defs>
<path fill="#D9771A" mask="url(#lgb)" d="M121 18 C 76 56, 62 150, 121 204 Z"/><path fill="#F0A23A" mask="url(#lgb)" d="M135 18 C 174 62, 184 150, 135 204 Z"/>
<rect x="121" y="208" width="14" height="32" rx="7" fill="#D9771A"/></svg>`;

export function stage() {
  const st = document.getElementById('stage');
  const camera = el('<div id="camera"></div>');
  st.appendChild(camera);
  return { st, camera };
}

/** Zoom de z : le point (fx, fy) de la scène vient se placer en (tx, ty) à l'écran. */
export function setCamera(camera, z, fx, fy, tx = fx, ty = fy) {
  camera.style.transform = `translate(${tx}px, ${ty}px) scale(${z}) translate(${-fx}px, ${-fy}px)`;
}

const CHROME = title => `<div class="tb"><span class="ttl">${title}</span><span class="cap">&#xE921;</span><span class="cap">&#xE922;</span><span class="cap">&#xE8BB;</span></div>`;
const capFont = node => node.querySelectorAll('.cap').forEach(c => { c.style.fontFamily = 'Segoe Fluent Icons, Segoe MDL2 Assets'; });

/** Fenêtre positionnée en (x, y), à l'échelle s ; la fenêtre interne porte l'apparition. */
function frame(layer, { x, y, s, w, h, title, body, cls = '' }) {
  const outer = el(`<div style="position:absolute;left:${x}px;top:${y}px;width:${w}px;height:${h}px;transform:scale(${s});transform-origin:0 0"></div>`);
  const win = el(`<div class="win ${cls}" style="left:0;top:0;width:${w}px;height:${h}px">${CHROME(title)}<div class="bd" style="position:absolute;left:0;right:0;top:32px;bottom:0">${body}</div></div>`);
  capFont(win);
  outer.appendChild(win);
  layer.appendChild(outer);
  return { outer, win, q: sel => win.querySelector(sel) };
}

const NOTES = ['Trip to Lisbon', 'Reading list', 'Garden plan', 'Recipes', 'Ideas'];

/** Appli hôte : une pastille Mue dans la barre d'outils, comme DockPad, PicaLite et AlterVNC. */
export function hostApp(layer, { x = 260, y = 110, s = 1, w = 1400, h = 800 } = {}) {
  const body = `<div class="app">
    <div class="tool"><span class="ib"></span><span class="t">${APP}</span><span class="sp"></span>
      <span class="badge mono">v${FROM}</span><span class="ib"></span><span class="ib"></span></div>
    <div class="side">${NOTES.map((n, i) => `<div class="it ${i === 0 ? 'sel' : ''}"><b>${n}</b><i style="width:${70 + (i * 13) % 25}%"></i></div>`).join('')}</div>
    <div class="doc"><h2>${NOTES[0]}</h2>${[92, 84, 88, 60, 0, 90, 76, 82, 45].map(p => p ? `<i style="width:${p}%"></i>` : '<div style="height:18px"></div>').join('')}</div>
  </div>`;
  const f = frame(layer, { x, y, s, w, h, title: APP, body });
  f.badge = f.q('.badge');
  return f;
}

/** État de la pastille : version seule, ou transition avec la teinte d'accent (HasRelease). */
export function setBadge(badge, { release = false, version = FROM, hover = false, down = false } = {}) {
  badge.textContent = release ? `v${version} → ${TO}` : `v${version}`;
  badge.classList.toggle('rel', release);
  badge.classList.toggle('hover', release && hover && !down);
  badge.classList.toggle('down', release && down);
}

/** UpdatesDialog : 580 × 540 avec une version disponible. */
export function updatesDialog(layer, { x, y, s }) {
  const body = `<div class="mue">
    <div class="hd">
      <div class="lbl"></div><div class="h"></div>
      <div class="vers"><span class="pill">${FROM}</span><span class="arrow">→</span><span class="pill new">${TO}</span>
        <span class="meta">Released on 11/4/2026 · 12.4 MB</span><span class="gh">View on GitHub ↗</span></div>
      <div class="banner"></div>
      <div class="prog"><div class="bar"><i></i></div><div class="size"></div></div>
    </div>
    <div class="notes"><div class="lbl">What’s new</div>
      <p><b>${APP} ${TO}</b> — demo data</p>
      <ul><li>Notes can be pinned to the top of the list.</li><li>Faster search across all notebooks.</li><li>Export to Markdown keeps images.</li></ul></div>
    <div class="summary" style="margin:0 20px 12px;display:none"></div>
    <div class="ft"><div class="grow"><span class="cbx on">✓</span>Check for updates automatically<small>Checked just now</small></div>
      <span class="bt b2"></span><span class="bt p b1"></span></div>
  </div>`;
  const f = frame(layer, { x, y, s, w: 580, h: 540, title: 'Updates', body });
  return f;
}

// Présentations d'UpdatePresentation.For, en anglais.
export const STATES = {
  available: { lbl: 'AVAILABLE', h: `Version ${TO} is available`, st: `Version ${TO} is available.`, b2: 'Later', b1: 'Download and install' },
  downloading: { lbl: 'DOWNLOADING', h: `Downloading ${TO}`, b2: 'Stop', b1: 'Restart and install', off: true },
  ready: { lbl: 'READY TO INSTALL', h: 'New version ready', st: `Downloaded. ${APP} will close briefly to install it, then reopen.`, b2: 'Later', b1: 'Restart and install', ready: true },
  preparing: { lbl: 'PREPARING', h: 'Preparing the installation', st: 'Preparing the update…', b2: 'Later', b1: 'Restart and install', off: true },
  installing: { lbl: 'INSTALLING', h: 'Installing the update', st: `Closing ${APP} to install the update…`, b2: 'Later', b1: 'Restart and install', off: true },
};

export function renderUpdates(f, state, progress = 0, summary = null) {
  f.q('.summary').textContent = summary ?? '';
  f.q('.summary').style.display = summary ? 'block' : 'none';
  const p = STATES[state];
  f.q('.lbl').textContent = p.lbl;
  f.q('.h').textContent = p.h;
  f.q('.banner').textContent = state === 'downloading' ? `Downloading… ${Math.round(progress * 100)}%` : p.st;
  f.q('.banner').classList.toggle('ready', !!p.ready);
  f.q('.prog').style.display = state === 'downloading' ? 'block' : 'none';
  f.q('.bar i').style.width = `${progress * 100}%`;
  f.q('.size').textContent = `${(12.4 * progress).toFixed(1)} MB of 12.4 MB`;
  f.q('.b2').textContent = p.b2;
  f.q('.b1').textContent = p.b1;
  f.q('.b1').classList.toggle('off', !!p.off);
}

/** UpdateBlockersDialog : 700 × h (540 par défaut, la fenêtre est redimensionnable), groupes [{ name, relay, help, procs: [[nom, détail]] }]. */
export function blockersDialog(layer, { x, y, s, groups, h = 540 }) {
  const grp = (g, i) => `<div class="grp g${i}"><div class="gh2"><span class="cbx"></span><div style="flex:1">
      <div style="display:flex;align-items:center"><span class="nm">${g.name}</span><span class="cnt">${g.procs.length}</span></div>
      <div class="gh ${g.relay ? 'relay' : ''}">${g.help}</div></div><span class="chev">⌃</span></div>
      <div class="pr">${g.procs.map(([n, d]) => `<div>${n}<span>${d}</span></div>`).join('')}</div></div>`;
  const body = `<div class="mue">
    <div class="hd" style="padding-bottom:12px"><div class="lbl">PREPARING</div><div class="h">Close apps to continue</div>
      <div class="help">The installer will ask programs that are still open to quit. Programs that do not quit may be interrupted without saving their work.</div></div>
    <div style="flex:1;overflow:hidden">${groups.map(grp).join('')}</div>
    <div class="ft"><span class="bt">Postpone</span><span class="grow"></span><span class="bt">Retry</span><span class="bt p b1"></span></div>
  </div>`;
  const f = frame(layer, { x, y, s, w: 700, h, title: 'Close apps to continue', body });
  f.groups = groups.map((_, i) => f.q(`.g${i}`));
  return f;
}

/** Cases cochées par groupe ; libellé du bouton selon UpdateBlockersDialog.UpdateLabels (AskToQuit). */
export function renderBlockers(f, checked, groups) {
  let n = 0;
  f.groups.forEach((g, i) => {
    const on = checked[i];
    const c = g.querySelector('.cbx');
    c.classList.toggle('on', on); c.textContent = on ? '✓' : '';
    g.classList.toggle('ignored', !on);
    if (on) n += groups[i].procs.length;
  });
  f.q('.b1').textContent = n === 0 ? 'Install without closing them' : `Close ${n} ${n === 1 ? 'program' : 'programs'} and install`;
}

/** UpdateBlockersDialog en confirmation : aucun bloqueur, un résumé de l'hôte (Confirming). */
export function confirmDialog(layer, { x, y, s, summary, h = 540 }) {
  const body = `<div class="mue">
    <div class="hd" style="padding-bottom:12px"><div class="lbl">PREPARING</div><div class="h">Confirm installation</div>
      <div class="help">${summary}</div></div>
    <div class="fill"></div>
    <div class="ft"><span class="bt">Postpone</span><span class="grow"></span><span class="bt">Retry</span><span class="bt p b1">Confirm and install</span></div>
  </div>`;
  return frame(layer, { x, y, s, w: 700, h, title: 'Confirm installation', body });
}

/** Touche affichée à l'écran (Échap), enfoncée entre down[0] et down[1]. */
export function keycap(st, label) {
  const k = el(`<div class="key">${label}</div>`);
  st.appendChild(k);
  return (t, a, b, down) => {
    const o = env(t, a, b, .15), d = t >= down[0] && t < down[1];
    k.classList.toggle('down', d);
    style(k, { opacity: o, transform: `translateY(${(d ? 4 : 0) + (1 - o) * 12}px)` });
  };
}

/** Écran pendant que l'appli se ferme, s'installe et se relance. */
export function restartScreen(layer, { x, y, w, h }) {
  const r = el(`<div class="restart" style="left:${x}px;top:${y}px;width:${w}px;height:${h}px"><div class="spin"></div><span>Closing ${APP} to install the update…</span></div>`);
  layer.appendChild(r);
  return r;
}

export function renderRestart(r, t, a, b) {
  style(r, { opacity: env(t, a, b, .2) });
  r.querySelector('.spin').style.transform = `rotate(${t * 400}deg)`;
}

const CURSOR_SVG = `<svg viewBox="0 0 30 44" width="30" height="44"><path d="M3 3 L3 33 L10.5 26 L15.5 38 L20.5 36 L15.5 24.5 L25.5 24.5 Z" fill="#fff" stroke="#111" stroke-width="2" stroke-linejoin="round"/></svg>`;

export function cursor(layer) {
  const c = el(`<div class="cursor">${CURSOR_SVG}</div>`);
  const r = el('<div class="ripple"></div>');
  layer.appendChild(r); layer.appendChild(c);
  return { c, r };
}

/** Curseur : path = [[t, x, y], ...] en coordonnées de scène, clicks = [t, ...], visible = [a, b]. */
export function renderCursor(cur, t, path, clicks = [], visible = [0, 999]) {
  const [x, y] = track(t, path.map(([tt, px, py]) => [tt, [px, py]]));
  let down = 0;
  for (const tc of clicks) down = Math.max(down, press(t, tc, .08));
  const op = env(t, visible[0], visible[1], .2);
  style(cur.c, { transform: `translate(${x - 3}px, ${y - 3}px) scale(${1 - down * .14})`, opacity: op });
  let rp = null;
  for (const tc of clicks) if (t >= tc && t < tc + .45) rp = (t - tc) / .45;
  if (rp == null) cur.r.style.opacity = 0;
  else style(cur.r, { left: `${x}px`, top: `${y}px`, opacity: (1 - rp) * .8 * op, transform: `scale(${.3 + rp * .9})` });
  return { x, y };
}

/** Légende en bas de l'écran, précédée du symbole Mue. items = [[a, b, texte], ...]. */
export function legend(st) {
  const box = el(`<div class="legend"><span>${FEATHER}<b style="font-weight:600"></b></span></div>`);
  st.appendChild(box);
  const txt = box.querySelector('b');
  return (t, items) => {
    const it = items.find(([a, b]) => t >= a && t < b);
    if (!it) { box.style.opacity = 0; return; }
    const [a, b, text] = it;
    const o = env(t, a, b, .3);
    txt.textContent = text;
    style(box, { opacity: o, transform: `translateY(${(1 - o) * 16}px)` });
  };
}

export function centerOf(node) {
  const r = node.getBoundingClientRect(), s = document.getElementById('stage').getBoundingClientRect();
  return { x: r.left - s.left + r.width / 2, y: r.top - s.top + r.height / 2, w: r.width, h: r.height, l: r.left - s.left, t: r.top - s.top };
}

/** Apparition d'une fenêtre : échelle from → 1 et fondu, puis fermeture. */
export function renderPop(node, t, open, close, from = .96) {
  const o = prog(t, open[0], open[1], E.out) * (1 - prog(t, close[0], close[1], E.in));
  style(node, { opacity: o, transform: `scale(${from + (1 - from) * o})`, visibility: o > 0.001 ? 'visible' : 'hidden' });
  return o;
}

export { clamp, lerp };
