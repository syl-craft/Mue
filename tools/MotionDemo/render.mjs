// Rendu des clips de démonstration de Mue : capture image par image dans Chromium headless, puis ffmpeg.
// Repris de DockPad tools/MotionDemo.
//
//   node render.mjs                                   tous les clips, les deux thèmes, dans ../Mue-media/videos (à côté du dépôt)
//   node render.mjs --clip 01-update-flow --theme dark
//   node render.mjs --clip 01-update-flow --stills 1.5,3.4   images clés en PNG, pour relecture
//   node render.mjs --serve                           http://localhost:4174/clips/01-update-flow.html
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const here = path.dirname(fileURLToPath(import.meta.url));

// Les rendus ne vivent pas dans ce dépôt : ils vont dans le dépôt média syl-craft/Mue-media, cloné à côté
// de celui-ci, d'où le README les cite en URL raw.
const outDir = process.env.MUE_MEDIA_DIR || path.resolve(here, '..', '..', '..', 'Mue-media', 'videos');
const FPS = 60;

const args = process.argv.slice(2);
const opt = name => { const i = args.indexOf(`--${name}`); return i < 0 ? null : (args[i + 1] ?? true); };

const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.png': 'image/png', '.svg': 'image/svg+xml' };

function serve(port) {
  const server = http.createServer((req, res) => {
    const url = decodeURIComponent(new URL(req.url, 'http://x').pathname);
    let file = null;
    const f = path.join(here, url);
    if (f.startsWith(here) && fs.existsSync(f) && fs.statSync(f).isFile()) file = f;
    if (!file) { res.writeHead(404); res.end(); return; }
    res.writeHead(200, { 'Content-Type': TYPES[path.extname(file)] || 'application/octet-stream' });
    fs.createReadStream(file).pipe(res);
  });
  return new Promise(ok => server.listen(port, () => ok(server)));
}

function ffmpeg(argv) {
  const p = spawn('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', ...argv], { stdio: ['pipe', 'inherit', 'inherit'] });
  const done = new Promise((ok, ko) => p.on('close', code => (code === 0 ? ok() : ko(new Error(`ffmpeg ${code}`)))));
  return { stdin: p.stdin, done };
}

const write = (stream, buf) => new Promise(ok => (stream.write(buf) ? ok() : stream.once('drain', ok)));

async function renderClip(browser, base, clip, theme, lang, stills) {
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1 });
  await page.goto(`${base}/clips/${clip}.html?render=1&theme=${theme}${lang ? `&lang=${lang}` : ''}`);
  await page.waitForFunction(() => window.READY === true, null, { timeout: 30000 });
  const duration = await page.evaluate(() => window.DURATION);
  const stage = page.locator('#stage');
  const name = lang ? `${clip}-${theme}-${lang}` : `${clip}-${theme}`;

  if (stills) {
    const dir = path.join(here, 'out', 'stills');
    fs.mkdirSync(dir, { recursive: true });
    for (const t of stills) {
      await page.evaluate(x => window.seek(x), t);
      const f = path.join(dir, `${name}-${t.toFixed(2)}.png`);
      await stage.screenshot({ path: f });
      console.log(`  ${f}`);
    }
    await page.close();
    return;
  }

  if (!fs.existsSync(path.dirname(outDir))) throw new Error(`dépôt média introuvable : ${path.dirname(outDir)} — git clone https://github.com/syl-craft/Mue-media à côté de Mue, ou MUE_MEDIA_DIR`);
  fs.mkdirSync(outDir, { recursive: true });
  const mp4 = path.join(outDir, `${name}.mp4`);
  const gif = path.join(outDir, `${name}.gif`);
  const frames = Math.round(duration * FPS);
  const enc = ffmpeg(['-f', 'image2pipe', '-framerate', String(FPS), '-c:v', 'png', '-i', '-',
    '-c:v', 'libx264', '-preset', 'slow', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', mp4]);
  const t0 = Date.now();
  for (let i = 0; i < frames; i++) {
    await page.evaluate(x => window.seek(x), i / FPS);
    await write(enc.stdin, await stage.screenshot({ type: 'png' }));
  }
  enc.stdin.end();
  await enc.done;
  await page.close();

  const mb = f => (fs.statSync(f).size / 1048576).toFixed(2);

  // GIF pour le README, palette calculee sur le clip lui-meme : 960 x 540 a 20 i/s ; au-dela de 10 s,
  // 720 x 405 a 10 i/s, pour rester vers 5 Mo comme les autres clips.
  const long = duration > 10;
  const g = ffmpeg(['-i', mp4, '-filter_complex',
    `fps=${long ? 10 : 20},scale=${long ? 720 : 960}:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=${long ? 128 : 192}:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle`,
    '-loop', '0', gif]);
  g.stdin.end();
  await g.done;
  console.log(`  ${name} : ${frames} images en ${((Date.now() - t0) / 1000).toFixed(0)} s — mp4 ${mb(mp4)} Mo, gif ${mb(gif)} Mo`);
}

const port = 4174;
const server = await serve(port);
const base = `http://localhost:${port}`;

if (opt('serve')) {
  console.log(`${base}/clips/  (Ctrl+C pour arreter)`);
} else {
  const all = fs.readdirSync(path.join(here, 'clips')).filter(f => f.endsWith('.html')).map(f => f.replace('.html', '')).sort();
  const clips = opt('clip') ? [opt('clip')] : all;
  const themes = opt('theme') ? [opt('theme')] : ['light', 'dark'];
  const langs = opt('lang') ? String(opt('lang')).split(',') : [null];
  const stills = opt('stills') ? String(opt('stills')).split(',').map(Number) : null;
  const browser = await chromium.launch();
  try {
    for (const c of clips) for (const th of themes) for (const lg of langs) await renderClip(browser, base, c, th, lg, stills);
  } finally {
    await browser.close();
    server.close();
  }
}
