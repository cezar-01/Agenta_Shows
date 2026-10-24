// O service worker da UMURA (molde do MyPods, sem a parte de push — ela entra na fase 2).
//
// ⚠️ Trocar o NOME do cache é obrigatório a cada mudança neste arquivo ou nos arquivos do PRECACHE: o precache só busca
// da rede no install, então mexer no conteúdo sem renomear deixaria a versão velha servida para sempre nos celulares que
// instalaram o app. (Há teste para isso: ServiceWorkerSentinelTests.)
//
// ⚠️ Blazor Server: NUNCA interceptar /_blazor (o circuito), /_framework nem documento HTML — página cacheada é tela
// zumbi com o circuito morto. E nada de /api: os tiles do mapa moram lá e são privados (a URL diz que região alguém olhou).
const CACHE = 'umura-v3'; // v3: convite de instalar o PWA no topo da Home
const PRECACHE = [
  '/offline.html',
  '/js/offline.js',
  '/icons/icon-192.png',
  '/icons/icon-512.png'
];

const STATIC_DESTINATIONS = ['script', 'style', 'image', 'font', 'manifest'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE)
      .then((cache) => cache.addAll(PRECACHE))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

// Rede de segurança para quando nem a /offline.html está no cache. Sem script (a CSP não aceita inline).
function offlineFallback() {
  return caches.match('/offline.html').then((r) =>
    r || new Response(
      '<!doctype html><html lang="pt-BR"><meta charset="utf-8">' +
      '<title>Não foi possível carregar · UMURA</title>' +
      '<body style="font-family:sans-serif;background:#EDECE7;color:#282828;display:flex;min-height:100vh;align-items:center;justify-content:center;text-align:center;">' +
      '<div><h1>Não foi possível carregar a UMURA</h1>' +
      '<p>Pode ser a sua conexão ou o nosso servidor. Tente de novo em instantes.</p>' +
      '<p><a href="/" style="color:#0B3C3F">Tentar novamente</a></p></div></body></html>',
      { headers: { 'Content-Type': 'text/html; charset=utf-8' } }));
}

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;

  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;
  if (url.pathname.startsWith('/_blazor') ||
      url.pathname.startsWith('/_framework') ||
      url.pathname.startsWith('/api')) return;

  // Navegação: sempre a rede; se ela falhar, a página offline (que tenta de novo sozinha).
  if (req.mode === 'navigate') {
    event.respondWith(fetch(req).catch(() => offlineFallback()));
    return;
  }

  const accept = req.headers.get('accept') || '';
  if (req.destination === 'document' || accept.includes('text/html')) return;
  if (!STATIC_DESTINATIONS.includes(req.destination)) return;

  // Estáticos (fingerprintados pelo MapStaticAssets): cache primeiro, atualizando por trás.
  event.respondWith(
    caches.match(req).then((cached) => {
      const fromNetwork = fetch(req).then((res) => {
        if (res && res.ok && res.type === 'basic') {
          const copy = res.clone();
          caches.open(CACHE).then((cache) => cache.put(req, copy));
        }
        return res;
      }).catch(() => cached);
      return cached || fromNetwork;
    })
  );
});
