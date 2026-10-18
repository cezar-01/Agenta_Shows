// Renderiza os SVGs de svg/ em PNG no Chrome headless (porta de depuração já aberta).
// Uso: node render.mjs <porta> <pasta-de-saida> [arquivo.html largura altura saida.png]
import { readdirSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const aqui = dirname(fileURLToPath(import.meta.url));
const [porta = '9333', saida = join(aqui, 'png'), html, lw, lh, lout] = process.argv.slice(2);
const alvo = await (await fetch(`http://127.0.0.1:${porta}/json/new?about:blank`, { method: 'PUT' })).json();
const ws = new WebSocket(alvo.webSocketDebuggerUrl);
let id = 0; const pend = new Map();
const call = (method, params = {}) => new Promise(r => { const i = ++id; pend.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
ws.onmessage = m => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { pend.get(d.id)(d); pend.delete(d.id); } };
await new Promise(r => ws.onopen = r);
await call('Page.enable');

async function foto(url, w, h, arquivo) {
  await call('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
  await call('Page.navigate', { url });
  await new Promise(r => setTimeout(r, 350));
  const shot = await call('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: w, height: h, scale: 1 } });
  writeFileSync(arquivo, Buffer.from(shot.result.data, 'base64'));
}

if (html) {
  await foto(pathToFileURL(html).href, +lw, +lh, lout);
} else {
  for (const [sub, w, h] of [['eventos', 1200, 675], ['nichos', 320, 320]]) {
    mkdirSync(join(saida, sub), { recursive: true });
    for (const f of readdirSync(join(aqui, 'svg', sub)).filter(f => f.endsWith('.svg'))) {
      await foto(pathToFileURL(join(aqui, 'svg', sub, f)).href, w, h, join(saida, sub, f.replace('.svg', '.png')));
    }
  }
}
await call('Target.closeTarget', { targetId: alvo.id }).catch(() => {});
ws.close(); process.exit(0);
