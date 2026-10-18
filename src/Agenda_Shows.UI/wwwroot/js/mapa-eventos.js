// Os eventos no mapa (a tela /mapa): todo elemento com [data-mapa-eventos] vira um mapa do Leaflet com os pinos dos
// eventos, agrupados pelo Leaflet.markercluster. O molde é o mapa-alfinete.js: o Leaflet e o cluster só são baixados
// quando a página tem um mapa (os endereços, já com a impressão digital, vêm nos atributos do próprio <script>), e os
// tiles vêm do PRÓPRIO site (/api/mapa/tiles) — o navegador nunca fala com o servidor de tiles, e a CSP segue 'self'.
//
// O contrato é só de atributos (a classe desenha, o atributo liga):
//   data-mapa-eventos="/api/mapa/eventos?nicho=shows"  de onde vêm os pinos — só caminho do próprio site
//   data-mapa-gps="um-mapa-gps"                        o id (ou "#seletor") do botão "Perto de mim"; padrão: um-mapa-gps
//   data-mapa-msg="id"                                 o nó dos avisos; padrão: o irmão com [data-mapa-msg]
// O nó dos avisos nasce VAZIO no HTML do Blazor: o módulo só escreve texto ali (nunca num nó que o Blazor desenha com
// conteúdo — trocar o texto de um nó do Blazor o desliga do diff).
// O JSON: [{ slug, titulo, nicho, nichoNome, localNome, bairro, lat, lng, quando, aoVivo, preco, foto }].
// Dado do servidor NUNCA passa por innerHTML: o popup é montado com createElement/textContent.
// Deu errado (Leaflet não veio, a API falhou): o mapa ganha data-mapa-falhou e o aviso vai para o nó irmão — a página
// tem a lista com os mesmos eventos logo abaixo.
//
// A navegação melhorada do Blazor sincroniza o DOM com o HTML novo: no /mapa → /mapa?nicho=x ela reaproveita o MESMO
// elemento do mapa (troca o atributo, apaga os filhos que o Leaflet criou, zera a classe) e tira do <head> as folhas
// que este módulo pôs lá. Por isso cada mutação relevante (e o enhancedload) passa por confere(): o mapa atropelado é
// solto e refeito, o que saiu da página é solto, e as folhas que sumiram voltam.
(function () {
  'use strict';

  var script = document.currentScript;
  var atributos = (script && script.dataset) || {};
  var LEAFLET_JS = atributos.leafletJs;
  var CLUSTER_JS = atributos.clusterJs;
  // A ordem importa: Leaflet, depois o cluster, depois o tema padrão do cluster (o app.css repinta por cima).
  var FOLHAS = [atributos.leafletCss, atributos.clusterCss, atributos.clusterCssDefault].filter(Boolean);

  var SELETOR = '[data-mapa-eventos]';
  var TILES = '/api/mapa/tiles/{z}/{x}/{y}.png';
  var CENTRO = [-25.4284, -49.2733];   // Curitiba (a Praça Tiradentes, mais ou menos)
  var ZOOM = 13;
  var ZOOM_GPS = 15;
  var ZOOM_MIN = 11;
  var ZOOM_MAX = 19;
  var ZOOM_MAX_AJUSTE = 16;            // o fitBounds de um pino só não mergulha até o nível da calçada
  // Curitiba e região, com folga. O proxy de tiles só serve o Brasil: fora daqui o mapa seria um quadro cinza.
  var LIMITES = [[-26.45, -50.25], [-24.65, -48.45]];
  var ESPERA_SCRIPT = 20000;

  var MSG = {
    leaflet: 'O mapa não carregou agora. A lista logo abaixo tem os mesmos eventos.',
    dados: 'Não deu para buscar os eventos do mapa. A lista logo abaixo tem os mesmos eventos.',
    endereco: 'O mapa ficou sem o endereço dos eventos. A lista logo abaixo tem os mesmos eventos.',
    vazio: 'Nenhum evento no mapa nos próximos 7 dias.',
    vazioFiltro: 'Nenhum evento desse nicho no mapa nos próximos 7 dias.',
    procurando: 'Procurando você…',
    fora: 'Você está fora de Curitiba e região — o mapa mostra só a cidade.',
    gps: {
      denied: 'Sem permissão para usar sua localização. Libere nas configurações do navegador e toque de novo.',
      insecure: 'A localização só funciona com conexão segura (https).',
      unsupported: 'Este navegador não informa a localização.',
      timeout: 'A localização demorou demais. Tente de novo — de preferência num lugar aberto.',
      unavailable: 'Não deu para achar você agora. Tente de novo em instantes.'
    }
  };

  var vivos = [];
  var carregando = null;

  // ─── utilidades ────────────────────────────────────────────────────────────────────────────────────────────────

  function midia(q) { return !!(window.matchMedia && window.matchMedia(q).matches); }
  function reduzido() { return midia('(prefers-reduced-motion: reduce)'); }
  // Roda do mouse dá zoom só no desktop (no celular não existe roda; no notebook com mouse, existe).
  function temMouse() { return midia('(hover: hover) and (pointer: fine)'); }

  function absoluto(u) {
    try { return new URL(u, document.baseURI).href; } catch (e) { return String(u); }
  }

  // Só caminho do próprio site: "/x" — nunca "//outro", "/\outro", "http:" nem "javascript:".
  function caminhoLocal(u) { return typeof u === 'string' && /^\/(?![\/\\])/.test(u); }

  function txt(v) { return v === null || v === undefined ? '' : String(v); }

  function porIdOuSeletor(ref) {
    if (!ref) return null;
    if (ref.charAt(0) !== '#') return document.getElementById(ref);
    try { return document.querySelector(ref); } catch (e) { return null; }
  }

  // ─── carregar o Leaflet e o cluster sob demanda ─────────────────────────────────────────────────────────────────

  function temFolha(href) {
    var alvo = absoluto(href);
    var ls = document.querySelectorAll('link[rel="stylesheet"]');
    for (var i = 0; i < ls.length; i++) if (ls[i].href === alvo) return true;
    return false;
  }

  // As folhas entram ANTES da primeira folha do site: o app.css (tokens --um-*) vence o CSS padrão do Leaflet na mesma
  // especificidade. Só escreve quando falta alguma — senão a própria escrita acordaria o observador de novo.
  function garanteFolhas() {
    var faltam = FOLHAS.filter(function (h) { return !temFolha(h); });
    if (!faltam.length) return;
    var nossas = FOLHAS.map(absoluto);
    var ancora = null;
    var ls = document.head.querySelectorAll('link[rel="stylesheet"]');
    for (var i = 0; i < ls.length; i++) {
      if (nossas.indexOf(ls[i].href) === -1) { ancora = ls[i]; break; }
    }
    faltam.forEach(function (h) {
      var l = document.createElement('link');
      l.rel = 'stylesheet';
      l.href = h;
      document.head.insertBefore(l, ancora);   // âncora nula = no fim do <head>
    });
  }

  function scriptJaPosto(src) {
    var alvo = absoluto(src);
    var ss = document.getElementsByTagName('script');
    for (var i = 0; i < ss.length; i++) if (ss[i].src === alvo) return ss[i];
    return null;
  }

  // Um <script> por vez. Se outro módulo (o mapa-alfinete) já pôs o mesmo arquivo e ele ainda está chegando, espera ele.
  function carregaScript(src, pronto) {
    if (pronto()) return Promise.resolve();
    return new Promise(function (ok, falhou) {
      var s = scriptJaPosto(src);
      var novo = !s;
      var feito = false;
      var relogio = setTimeout(function () { fim(new Error('mapa-eventos: ' + src + ' demorou demais')); }, ESPERA_SCRIPT);

      function fim(erro) {
        if (feito) return;
        feito = true;
        clearTimeout(relogio);
        if (!erro && !pronto()) erro = new Error('mapa-eventos: ' + src + ' carregou sem o que se esperava');
        if (erro) {
          // O que falhou sai do <head>: a próxima tentativa (outra navegação) põe um novo, em vez de esperar este.
          if (novo && s.parentNode) s.parentNode.removeChild(s);
          falhou(erro);
        } else {
          ok();
        }
      }

      if (novo) {
        s = document.createElement('script');
        s.src = src;
        s.async = false;
      }
      s.addEventListener('load', function () { fim(null); });
      s.addEventListener('error', function () { fim(new Error('mapa-eventos: ' + src + ' não carregou')); });
      if (novo) document.head.appendChild(s);
    });
  }

  function carregaLeaflet() {
    var temLeaflet = function () { return !!(window.L && window.L.map); };
    var temTudo = function () { return temLeaflet() && typeof window.L.markerClusterGroup === 'function'; };
    if (temTudo()) return Promise.resolve(window.L);
    if (carregando) return carregando;
    if (!LEAFLET_JS || !CLUSTER_JS) return Promise.reject(new Error('mapa-eventos: sem o endereço do Leaflet ou do cluster'));
    garanteFolhas();
    // O cluster é plugin: estende o L global, então só entra DEPOIS do Leaflet.
    carregando = carregaScript(LEAFLET_JS, temLeaflet)
      .then(function () { return carregaScript(CLUSTER_JS, temTudo); })
      .then(function () { return window.L; }, function (e) { carregando = null; throw e; });
    return carregando;
  }

  function buscaEventos(url) {
    return fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
      .then(function (r) {
        if (!r.ok) throw new Error('mapa-eventos: HTTP ' + r.status);
        return r.json();
      })
      .then(function (lista) { return Array.isArray(lista) ? lista : []; });
  }

  // ─── avisos ────────────────────────────────────────────────────────────────────────────────────────────────────

  function noDosAvisos(el) {
    var ref = el.getAttribute('data-mapa-msg');
    if (ref) return porIdOuSeletor(ref);
    var pai = el.parentNode;
    if (!pai || !pai.children) return null;
    for (var i = 0; i < pai.children.length; i++) {
      var c = pai.children[i];
      if (c !== el && c.hasAttribute('data-mapa-msg')) return c;
    }
    return null;
  }

  // Só escreve quando o texto muda: a escrita é uma mutação, e o observador não pode entrar em laço por causa dela.
  function avisa(el, texto) {
    var no = noDosAvisos(el);
    texto = texto || '';
    if (no && no.textContent !== texto) no.textContent = texto;
  }

  function falha(el, texto) {
    el.setAttribute('data-mapa-falhou', '');
    avisa(el, texto);
  }

  // ─── pinos e popup ─────────────────────────────────────────────────────────────────────────────────────────────

  function linha(texto) {
    var s = document.createElement('span');
    s.textContent = texto;
    return s;
  }

  // O popup, montado com DOM: título, local e preço são texto do servidor e NUNCA viram HTML.
  function popup(ev) {
    var caixa = document.createElement('div');
    caixa.className = 'um-mapa-popup';
    if (caminhoLocal(ev.foto)) {
      var img = document.createElement('img');
      img.src = ev.foto;
      img.alt = '';
      img.width = 320;
      img.height = 180;
      img.decoding = 'async';
      caixa.appendChild(img);
    }
    var titulo = document.createElement('b');
    titulo.textContent = txt(ev.titulo);
    caixa.appendChild(titulo);
    var quandoOnde = [txt(ev.quando), txt(ev.localNome)].filter(Boolean).join(' · ');
    if (quandoOnde) caixa.appendChild(linha(quandoOnde));
    if (txt(ev.preco)) caixa.appendChild(linha(txt(ev.preco)));
    var link = document.createElement('a');
    link.href = '/evento/' + encodeURIComponent(txt(ev.slug));
    link.textContent = 'Ver evento';
    link.setAttribute('aria-label', 'Ver evento: ' + txt(ev.titulo));
    caixa.appendChild(link);
    return caixa;
  }

  // O nome do pino para o leitor de tela: "Show tal, Ao vivo agora, Ópera de Arame". O "ao vivo" vai no texto, não só
  // na cor coral do pino.
  function rotulo(ev) {
    return [txt(ev.titulo), txt(ev.quando), txt(ev.localNome)].filter(Boolean).join(', ');
  }

  function desenha(estado, lista) {
    var L = estado.L;
    var grupo = L.markerClusterGroup({
      showCoverageOnHover: false,
      spiderfyOnMaxZoom: true,
      maxClusterRadius: 48,
      chunkedLoading: true,
      animate: !estado.reduz,
      animateAddingMarkers: false
    });
    var marcas = [];
    var pontos = [];

    lista.forEach(function (ev) {
      if (!ev || typeof ev.slug !== 'string' || !ev.slug) return;
      var lat = Number(ev.lat);
      var lng = Number(ev.lng);
      if (!isFinite(lat) || !isFinite(lng) || Math.abs(lat) > 90 || Math.abs(lng) > 180) return;
      var aoVivo = ev.aoVivo === true;

      var marca = L.marker([lat, lng], {
        icon: L.divIcon({
          className: 'um-mapa-pino' + (aoVivo ? ' is-aovivo' : ''),
          html: '<span></span>',     // constante: nenhum dado do servidor entra aqui
          iconSize: [32, 40],
          iconAnchor: [16, 38],
          popupAnchor: [0, -34]
        }),
        title: txt(ev.titulo),
        keyboard: true,
        riseOnHover: true,
        zIndexOffset: aoVivo ? 400 : 0
      });
      // O popup nasce só quando abre (dezenas de pinos, um popup por vez).
      marca.bindPopup(function () { return popup(ev); }, {
        minWidth: 210,
        maxWidth: 260,
        autoPanPaddingTopLeft: [16, 76],
        autoPanPaddingBottomRight: [16, 76]
      });
      // O cluster põe e tira o pino do mapa conforme o zoom: o rótulo vai a cada vez que o elemento nasce.
      marca.on('add', function () {
        var no = marca.getElement();
        if (no) no.setAttribute('aria-label', rotulo(ev));
      });
      marcas.push(marca);
      pontos.push([lat, lng]);
    });

    grupo.addLayers(marcas);
    grupo.addTo(estado.mapa);
    estado.grupo = grupo;
    if (pontos.length) enquadra(estado, pontos);
    return pontos.length;
  }

  // Enquadra os pinos. Com o mapa ainda sem tamanho (aba escondida, folha fechada) o fitBounds daria o zoom máximo:
  // aí o enquadramento espera o ResizeObserver dizer que o mapa ganhou tamanho.
  function enquadra(estado, pontos) {
    var t = estado.mapa.getSize();
    if (!t.x || !t.y) { estado.enquadrar = pontos; return; }
    estado.enquadrar = null;
    estado.mapa.fitBounds(pontos, { padding: [40, 40], maxZoom: ZOOM_MAX_AJUSTE, animate: false });
  }

  // ─── "Perto de mim" ────────────────────────────────────────────────────────────────────────────────────────────

  function voceEstaAqui(estado, p) {
    if (estado.voce) { estado.voce.setLatLng(p); return; }
    // A cor vem do CSS (.um-mapa-voce): o SVG do Leaflet aceita fill/stroke por classe, por cima dos atributos.
    estado.voce = estado.L.circleMarker(p, {
      radius: 8, weight: 3, fillOpacity: 1, className: 'um-mapa-voce', interactive: false
    }).bindTooltip('Você está aqui', {
      permanent: true, direction: 'top', offset: [0, -10], className: 'um-mapa-voce-rotulo'
    }).addTo(estado.mapa);
  }

  function ligaGps(el, estado) {
    var btn = porIdOuSeletor(el.getAttribute('data-mapa-gps') || 'um-mapa-gps');
    if (!btn) return;
    var aoClicar = function () {
      if (estado.morto || btn.getAttribute('aria-busy') === 'true') return;
      // O geoloc.js (window.umGeo) é a régua única do GPS: estados denied/timeout/insecure/unsupported/unavailable.
      var geo = window.umGeo;
      if (!geo || typeof geo.get !== 'function') { avisa(el, MSG.gps.unsupported); return; }
      btn.setAttribute('aria-busy', 'true');
      avisa(el, MSG.procurando);
      geo.get(true).then(function (r) {
        btn.removeAttribute('aria-busy');
        if (estado.morto) return;
        if (!r || r.status !== 'ok') { avisa(el, MSG.gps[r && r.status] || MSG.gps.unavailable); return; }
        var p = estado.L.latLng(r.lat, r.lon);
        if (!estado.L.latLngBounds(LIMITES).contains(p)) { avisa(el, MSG.fora); return; }
        avisa(el, '');
        voceEstaAqui(estado, p);
        estado.mapa.setView(p, ZOOM_GPS, { animate: !estado.reduz });
      }, function () {
        btn.removeAttribute('aria-busy');
        if (!estado.morto) avisa(el, MSG.gps.unavailable);
      });
    };
    btn.addEventListener('click', aoClicar);
    estado.gps = { btn: btn, fn: aoClicar };
  }

  // ─── ligar, conferir, soltar ───────────────────────────────────────────────────────────────────────────────────

  function liga(el, L, url, pedido) {
    if (el._leaflet_id) { try { delete el._leaflet_id; } catch (e) { el._leaflet_id = undefined; } }
    var reduz = reduzido();
    var mapa = L.map(el, {
      center: CENTRO,
      zoom: ZOOM,
      minZoom: ZOOM_MIN,
      maxZoom: ZOOM_MAX,
      maxBounds: LIMITES,
      maxBoundsViscosity: 0.8,
      zoomControl: false,
      attributionControl: true,
      scrollWheelZoom: temMouse(),
      zoomSnap: 1,
      inertia: !reduz,
      zoomAnimation: !reduz,
      fadeAnimation: !reduz,
      markerZoomAnimation: !reduz
    });
    var estado = {
      el: el, url: url, L: L, mapa: mapa, painel: mapa.getPane('mapPane'), reduz: reduz,
      morto: false, grupo: null, voce: null, gps: null, ro: null, clique: null, enquadrar: null
    };
    el.__mapaEventos = estado;
    el.removeAttribute('data-mapa-falhou');
    avisa(el, '');

    mapa.attributionControl.setPrefix(false);
    L.control.zoom({ position: 'topright', zoomInTitle: 'Aproximar', zoomOutTitle: 'Afastar' }).addTo(mapa);
    L.tileLayer(TILES, {
      minZoom: ZOOM_MIN,
      maxZoom: ZOOM_MAX,
      attribution: '© <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a>'
    }).addTo(mapa);

    mapa.on('popupopen', function (e) {
      var no = e.popup && e.popup.getElement && e.popup.getElement();
      var fechar = no && no.querySelector('.leaflet-popup-close-button');
      if (fechar) fechar.setAttribute('aria-label', 'Fechar');
    });

    // Os links internos do Leaflet ("#close" do popup, "#" do zoom) não podem chegar ao Blazor: com <base href="/">
    // ele leria "/#close" como navegação para o início. Sem el.contains(a): o "x" do popup já saiu do DOM quando o
    // clique chega aqui (o Leaflet fecha o popup no próprio botão) — se o clique passou por este elemento, veio de dentro.
    estado.clique = function (ev) {
      var a = ev.target && ev.target.closest ? ev.target.closest('a[href^="#"]') : null;
      if (a) { ev.preventDefault(); ev.stopPropagation(); }
    };
    el.addEventListener('click', estado.clique);

    ligaGps(el, estado);

    // A folha que abre, a barra do navegador que some: o Leaflet precisa saber o tamanho novo.
    if (window.ResizeObserver) {
      estado.ro = new ResizeObserver(function () {
        if (estado.morto) return;
        mapa.invalidateSize({ pan: false });
        if (estado.enquadrar) enquadra(estado, estado.enquadrar);
      });
      estado.ro.observe(el);
    }

    vivos.push(el);

    pedido.then(function (lista) {
      if (estado.morto) return;
      var n = desenha(estado, lista);
      if (!n) avisa(el, /[?&]nicho=/.test(url) ? MSG.vazioFiltro : MSG.vazio);
    }, function () {
      if (estado.morto) return;
      falha(el, MSG.dados);
    });
  }

  // Saudável = ainda na página, com o mesmo endereço, e com o painel que o Leaflet criou dentro dele. A navegação
  // melhorada que reaproveitou o elemento quebra pelo menos uma dessas três.
  function saudavel(el) {
    var e = el.__mapaEventos;
    return !!e && el.isConnected && el.getAttribute('data-mapa-eventos') === e.url &&
      !!e.painel && el.contains(e.painel) && el.classList.contains('leaflet-container');
  }

  function desliga(el) {
    var e = el.__mapaEventos;
    if (!e) return;
    e.morto = true;
    el.__mapaEventos = null;
    try { if (e.ro) e.ro.disconnect(); } catch (x) { /* nada a soltar */ }
    try { if (e.gps) e.gps.btn.removeEventListener('click', e.gps.fn); } catch (x) { /* nada a soltar */ }
    try { if (e.clique) el.removeEventListener('click', e.clique); } catch (x) { /* nada a soltar */ }
    try { e.mapa.remove(); } catch (x) { /* o DOM do mapa já tinha sido trocado */ }
    try { delete el._leaflet_id; } catch (x) { el._leaflet_id = undefined; }
  }

  function prepara(el) {
    var url = el.getAttribute('data-mapa-eventos');
    if (!caminhoLocal(url)) {
      el.__mapaEventosFalhou = url;
      falha(el, MSG.endereco);
      return;
    }
    var vez = {};
    el.__mapaEventosPendente = vez;
    // Os pinos e o Leaflet chegam em paralelo.
    var pedido = buscaEventos(url);
    pedido.catch(function () { /* tratado em liga() */ });

    carregaLeaflet().then(function (L) {
      if (el.__mapaEventosPendente !== vez) return;
      el.__mapaEventosPendente = null;
      if (!el.isConnected) return;
      // O endereço mudou enquanto o Leaflet chegava (o filtro foi trocado): recomeça com o novo.
      if (el.getAttribute('data-mapa-eventos') !== url) { varre(el); return; }
      try {
        liga(el, L, url, pedido);
      } catch (e) {
        desliga(el);
        el.__mapaEventosFalhou = url;
        falha(el, MSG.leaflet);
      }
    }, function () {
      if (el.__mapaEventosPendente !== vez) return;
      el.__mapaEventosPendente = null;
      el.__mapaEventosFalhou = url;
      if (el.isConnected) falha(el, MSG.leaflet);
    });
  }

  function varre(raiz) {
    var lista = [];
    if (raiz.nodeType === 1 && raiz.matches(SELETOR)) lista.push(raiz);
    if (raiz.querySelectorAll) lista.push.apply(lista, raiz.querySelectorAll(SELETOR));
    lista.forEach(function (el) {
      if (el.__mapaEventos || el.__mapaEventosPendente) return;
      // O que já falhou com este endereço não tenta de novo a cada mutação (tenta na próxima navegação).
      if (el.__mapaEventosFalhou !== undefined && el.__mapaEventosFalhou === el.getAttribute('data-mapa-eventos')) return;
      prepara(el);
    });
  }

  function confere() {
    vivos = vivos.filter(function (el) {
      if (saudavel(el)) return true;
      desliga(el);
      return false;
    });
    if (document.querySelector(SELETOR)) garanteFolhas();
    varre(document);
  }

  // Mutação dentro de um mapa vivo (tile chegando, popup abrindo) é do Leaflet — não interessa aqui.
  function dentroDeMapa(no) {
    var el = no && (no.nodeType === 1 ? no : no.parentElement);
    return !!(el && el.closest && el.closest('.leaflet-container'));
  }

  function comeca() {
    confere();

    new MutationObserver(function (mudancas) {
      for (var i = 0; i < mudancas.length; i++) {
        var m = mudancas[i];
        if (m.type === 'attributes' || !dentroDeMapa(m.target)) { confere(); return; }
      }
    }).observe(document.documentElement, {
      childList: true, subtree: true, attributes: true, attributeFilter: ['data-mapa-eventos']
    });

    // Na navegação melhorada a página é outra: o que falhou antes ganha uma nova chance.
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
      window.Blazor.addEventListener('enhancedload', function () {
        var els = document.querySelectorAll(SELETOR);
        for (var i = 0; i < els.length; i++) els[i].__mapaEventosFalhou = undefined;
        confere();
      });
    }
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', comeca);
  else comeca();
})();
