// O mapa com alfinete (15/10/2026, o ponto de coleta e o ponto de retorno): todo elemento com [data-mapa-alfinete] vira um
// mapa do Leaflet, e o Leaflet só é baixado quando a página tem um mapa. Os tiles vêm do PRÓPRIO site (/api/mapa/tiles) —
// o navegador nunca fala com o servidor de tiles, e a CSP segue 'self'.
//
// O contrato é só de atributos (a classe desenha, o atributo liga):
//   data-mapa-ponto="lat,lng"     onde o alfinete está
//   data-mapa-centro="lat,lng"    sem ponto: onde o mapa abre — o alfinete nasce no primeiro toque (ou no GPS)
//   data-mapa-base="lat,lng"      a base da loja: desenha a casa e o círculo, e o alfinete não sai do círculo
//   data-mapa-raio="100"          o raio do círculo, em metros
//   data-mapa-arrastavel          o alfinete anda (arrastar, tocar no mapa, setas do teclado)
//   data-mapa-icone="base"        o marcador que anda é a casa (mover a base), não o alfinete
//   data-mapa-campo="#id"         o campo que recebe "lat,lng" a cada mudança — o mesmo que funciona sem o mapa
//   data-mapa-distancia="#id"     o texto "N m da base", vivo enquanto o alfinete anda (o Blazor o deixa VAZIO)
//   data-mapa-perto="30"          abaixo destes metros da base...
//   data-mapa-aviso-perto="#id"   ...este aviso aparece (o atributo hidden sai)
//   data-mapa-gps="#id"           o botão "Usar minha localização"
//   data-mapa-zoom="17"           o zoom inicial
// O ponto escrito no campo dispara "input" e "change": o @bind do Blazor e o formulário SSR leem do mesmo jeito. O módulo
// NUNCA escreve num nó que o Blazor desenha com conteúdo — trocar o texto de um nó do Blazor o desliga do diff.
(function () {
  'use strict';

  var script = document.currentScript;
  var JS = script && script.dataset.leafletJs;
  var CSS = script && script.dataset.leafletCss;
  var TILES = '/api/mapa/tiles/{z}/{x}/{y}.png';
  var R_TERRA = 6371000;           // o mesmo raio da GeoCuritiba.DistanciaKm, que confere os 100 m no servidor
  var FOLGA = 1;                   // o alfinete preso no círculo fica 1 m para dentro: o arredondamento não o põe fora
  var PASSO_TECLADO = 5;           // metros por toque de seta no alfinete
  var CASA = '<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d="M12 3 2 11h3v9h5v-6h4v6h5v-9h3z"/></svg>';

  var carregando = null;
  var vivos = [];

  function carregaLeaflet() {
    if (window.L && window.L.map) return Promise.resolve(window.L);
    if (carregando) return carregando;
    carregando = new Promise(function (ok, falhou) {
      if (!JS || !CSS) { falhou(new Error('mapa: sem o endereço do Leaflet')); return; }
      var folha = document.createElement('link');
      folha.rel = 'stylesheet';
      folha.href = CSS;
      document.head.appendChild(folha);
      var s = document.createElement('script');
      s.src = JS;
      s.onload = function () { ok(window.L); };
      s.onerror = function () { carregando = null; falhou(new Error('mapa: o Leaflet não carregou')); };
      document.head.appendChild(s);
    });
    return carregando;
  }

  function ponto(texto) {
    if (!texto) return null;
    var p = String(texto).split(',');
    if (p.length !== 2) return null;
    var lat = parseFloat(p[0]), lng = parseFloat(p[1]);
    if (!isFinite(lat) || !isFinite(lng) || Math.abs(lat) > 90 || Math.abs(lng) > 180) return null;
    return { lat: lat, lng: lng };
  }

  function rad(g) { return g * Math.PI / 180; }

  // A mesma fórmula do servidor (haversine), para o número da tela ser o número que o servidor confere.
  function metros(a, b) {
    var dLat = rad(b.lat - a.lat), dLng = rad(b.lng - a.lng);
    var h = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
      Math.cos(rad(a.lat)) * Math.cos(rad(b.lat)) * Math.sin(dLng / 2) * Math.sin(dLng / 2);
    return 2 * R_TERRA * Math.asin(Math.sqrt(h));
  }

  // O ponto a "m" metros de "a" no rumo de "b" — o alfinete que passou do círculo volta para a borda dele.
  function naBorda(a, b, m) {
    var y = Math.sin(rad(b.lng - a.lng)) * Math.cos(rad(b.lat));
    var x = Math.cos(rad(a.lat)) * Math.sin(rad(b.lat)) - Math.sin(rad(a.lat)) * Math.cos(rad(b.lat)) * Math.cos(rad(b.lng - a.lng));
    return andar(a, Math.atan2(y, x), m);
  }

  function andar(a, rumo, m) {
    var d = m / R_TERRA, lat1 = rad(a.lat), lng1 = rad(a.lng);
    var lat2 = Math.asin(Math.sin(lat1) * Math.cos(d) + Math.cos(lat1) * Math.sin(d) * Math.cos(rumo));
    var lng2 = lng1 + Math.atan2(Math.sin(rumo) * Math.sin(d) * Math.cos(lat1), Math.cos(d) - Math.sin(lat1) * Math.sin(lat2));
    return { lat: lat2 * 180 / Math.PI, lng: ((lng2 * 180 / Math.PI) + 540) % 360 - 180 };
  }

  function texto(p) { return p.lat.toFixed(6) + ',' + p.lng.toFixed(6); }

  function alvo(seletor) {
    if (!seletor) return null;
    try { return document.querySelector(seletor); } catch (e) { return null; }
  }

  function escreve(campo, p) {
    if (!campo) return;
    campo.value = texto(p);
    campo.dispatchEvent(new Event('input', { bubbles: true }));
    campo.dispatchEvent(new Event('change', { bubbles: true }));
  }

  // A área que o site aceita: Curitiba e região, com um respiro (a régua do servidor é AreaDoOrganizador.Validar,
  // -26,2/-49,9 a -24,9/-48,8). O mapa não sai dela — fora não há tile, o proxy só serve a região — e um GPS ou uma
  // coordenada digitada de fora não move o alfinete para um quadro cinza.
  var AREA = [[-26.3, -50.0], [-24.8, -48.7]];
  function naArea(p) { return p.lat >= AREA[0][0] && p.lat <= AREA[1][0] && p.lng >= AREA[0][1] && p.lng <= AREA[1][1]; }

  function liga(el, L) {
    if (el.__mapa) return;
    var inicio = ponto(el.dataset.mapaPonto);
    var centro = inicio || ponto(el.dataset.mapaCentro);
    if (!centro) return;
    var base = ponto(el.dataset.mapaBase);
    var raio = parseFloat(el.dataset.mapaRaio || '100');
    var perto = parseFloat(el.dataset.mapaPerto || '0');
    var anda = el.hasAttribute('data-mapa-arrastavel');
    var ehBase = el.dataset.mapaIcone === 'base';
    var campo = alvo(el.dataset.mapaCampo);
    var distancia = alvo(el.dataset.mapaDistancia);
    var avisoPerto = alvo(el.dataset.mapaAvisoPerto);
    var gps = alvo(el.dataset.mapaGps);
    var reduz = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

    var mapa = L.map(el, {
      zoomControl: anda, attributionControl: true, scrollWheelZoom: false, dragging: anda, touchZoom: anda,
      doubleClickZoom: anda, boxZoom: false, keyboard: anda, zoomSnap: 1, minZoom: 12, maxZoom: 19,
      zoomAnimation: !reduz, fadeAnimation: !reduz, markerZoomAnimation: !reduz,
      maxBounds: AREA, maxBoundsViscosity: 1.0,
    }).setView([centro.lat, centro.lng], parseInt(el.dataset.mapaZoom || '17', 10));
    mapa.attributionControl.setPrefix(false);
    L.tileLayer(TILES, {
      maxZoom: 19, minZoom: 12,
      attribution: '© <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a>',
    }).addTo(mapa);

    if (base) {
      L.circle([base.lat, base.lng], { radius: raio, className: 'um-mapa-raio', weight: 1.5, dashArray: '6 6', interactive: false }).addTo(mapa);
      L.marker([base.lat, base.lng], {
        icon: L.divIcon({ className: 'um-mapa-base', html: '<span>' + CASA + '</span>', iconSize: [30, 30], iconAnchor: [15, 15] }),
        interactive: false, keyboard: false, zIndexOffset: -100,
      }).addTo(mapa);
    }

    var icone = ehBase
      ? L.divIcon({ className: 'um-mapa-base is-movel', html: '<span>' + CASA + '</span>', iconSize: [30, 30], iconAnchor: [15, 15] })
      : L.divIcon({ className: 'um-mapa-pino', html: '<span></span>', iconSize: [32, 40], iconAnchor: [16, 38] });
    var alfinete = null;

    function mede(p) {
      if (!base) return;
      var m = Math.round(metros(base, p));
      el.classList.toggle('is-fora', m > raio);
      if (distancia) distancia.textContent = m + ' m da base';
      if (avisoPerto) avisoPerto.hidden = !(perto > 0 && m < perto);
    }

    // O alfinete não sai do círculo: passou, volta para a borda (1 m para dentro).
    function preso(p) {
      if (!base || metros(base, p) <= raio) return p;
      return naBorda(base, p, raio - FOLGA);
    }

    function cria(p) {
      alfinete = L.marker([p.lat, p.lng], {
        icon: icone, draggable: anda, keyboard: anda, autoPan: true,
        title: anda ? (ehBase ? 'A base: arraste, ou use as setas' : 'O alfinete: arraste, ou use as setas') : '',
      }).addTo(mapa);
      if (!anda) return;
      alfinete.on('drag', function (e) { mede({ lat: e.latlng.lat, lng: e.latlng.lng }); });
      alfinete.on('dragend', function () { var ll = alfinete.getLatLng(); poe({ lat: ll.lat, lng: ll.lng }, true); });
      // As setas no marcador em foco o movem de 5 em 5 m: o mapa também anda sem mouse e sem dedo.
      var no = alfinete.getElement();
      if (!no) return;
      no.setAttribute('aria-label', ehBase ? 'A base da loja. Use as setas para mover.' : 'Alfinete do ponto. Use as setas para mover.');
      no.addEventListener('keydown', function (e) {
        var rumo = { ArrowUp: 0, ArrowRight: Math.PI / 2, ArrowDown: Math.PI, ArrowLeft: -Math.PI / 2 }[e.key];
        if (rumo === undefined) return;
        e.preventDefault();
        var ll = alfinete.getLatLng();
        poe(andar({ lat: ll.lat, lng: ll.lng }, rumo, PASSO_TECLADO), true);
      });
    }

    function poe(p, escrever) {
      p = preso(p);
      if (alfinete) alfinete.setLatLng([p.lat, p.lng]); else cria(p);
      mede(p);
      if (escrever) escreve(campo, p);
    }

    if (inicio) { cria(inicio); mede(inicio); }

    if (anda) {
      mapa.on('click', function (e) { poe({ lat: e.latlng.lat, lng: e.latlng.lng }, true); });

      if (gps) gps.addEventListener('click', function () {
        if (!navigator.geolocation) return;
        gps.setAttribute('aria-busy', 'true');
        navigator.geolocation.getCurrentPosition(function (pos) {
          gps.removeAttribute('aria-busy');
          var p = { lat: pos.coords.latitude, lng: pos.coords.longitude };
          // Longe de Curitiba (viagem, VPN): o mesmo aviso de "sem localização" — marque no mapa.
          if (!naArea(p)) { gps.dispatchEvent(new CustomEvent('mapa:semgps', { bubbles: true })); return; }
          poe(p, true);
          mapa.setView([p.lat, p.lng], Math.max(mapa.getZoom(), 17));
        }, function () {
          gps.removeAttribute('aria-busy');
          gps.dispatchEvent(new CustomEvent('mapa:semgps', { bubbles: true }));
        }, { enableHighAccuracy: true, timeout: 15000, maximumAge: 60000 });
      });

      // O campo digitado à mão também move o alfinete (o caminho sem mapa continua valendo junto com ele). Fora do
      // círculo, o campo é reescrito com a borda (e avisa de novo, para o Blazor ler o ponto que vale).
      if (campo) campo.addEventListener('change', function () {
        var p = ponto(campo.value);
        if (!p || !naArea(p)) return; // fora da área: o alfinete fica; o salvar avisa "marque um ponto em Curitiba"
        var dentro = preso(p);
        if (alfinete) alfinete.setLatLng([dentro.lat, dentro.lng]); else cria(dentro);
        mede(dentro);
        if (dentro !== p) escreve(campo, dentro);
        else mapa.panTo([dentro.lat, dentro.lng]);
      });
    }

    // A aba e a folha que abrem, e o cartão que muda de tamanho: o Leaflet precisa saber o tamanho novo (no painel
    // escondido o mapa nasce 0×0).
    if (window.ResizeObserver) {
      var ro = new ResizeObserver(function () { mapa.invalidateSize(); });
      ro.observe(el);
      el.__ro = ro;
    }

    el.__mapa = { mapa: mapa, poe: poe, get alfinete() { return alfinete; } };
    el.setAttribute('data-mapa-ligado', '');
    vivos.push(el);
  }

  // O Blazor pode trocar o ponto no MESMO elemento (salvou, abriu outra saída): o alfinete acompanha o atributo.
  function atualiza(el) {
    var m = el.__mapa;
    if (!m) return;
    var p = ponto(el.dataset.mapaPonto);
    if (!p) return;
    var atual = m.alfinete && m.alfinete.getLatLng();
    if (atual && Math.abs(atual.lat - p.lat) < 1e-7 && Math.abs(atual.lng - p.lng) < 1e-7) return;
    m.poe(p, false);
    m.mapa.panTo([p.lat, p.lng]);
  }

  function solta() {
    vivos = vivos.filter(function (el) {
      if (el.isConnected) return true;
      try { if (el.__ro) el.__ro.disconnect(); el.__mapa.mapa.remove(); } catch (e) { /* já saiu */ }
      el.__mapa = null;
      return false;
    });
  }

  function varre(raiz) {
    var lista = [];
    if (raiz.matches && raiz.matches('[data-mapa-alfinete]')) lista.push(raiz);
    if (raiz.querySelectorAll) lista.push.apply(lista, raiz.querySelectorAll('[data-mapa-alfinete]'));
    lista = lista.filter(function (el) { return !el.__mapa; });
    if (!lista.length) return;
    carregaLeaflet().then(function (L) {
      lista.forEach(function (el) {
        try { liga(el, L); } catch (e) { el.setAttribute('data-mapa-falhou', ''); }
      });
    }, function () {
      lista.forEach(function (el) { el.setAttribute('data-mapa-falhou', ''); });
    });
  }

  function comeca() {
    varre(document);
    new MutationObserver(function (mudancas) {
      var saiu = false;
      mudancas.forEach(function (m) {
        if (m.type === 'attributes') { atualiza(m.target); return; }
        m.addedNodes.forEach(function (n) { if (n.nodeType === 1) { varre(n); } });
        if (m.removedNodes.length) saiu = true;
      });
      if (saiu) solta();
    }).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-mapa-ponto'] });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', comeca);
  else comeca();
})();
