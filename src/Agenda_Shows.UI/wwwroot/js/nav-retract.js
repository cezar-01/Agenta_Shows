/*
 * A régua ÚNICA de retração das barras que acompanham a rolagem.
 *
 * ⚠️ UMA DECISÃO, VÁRIOS ALVOS. Este arquivo é o único lugar do app que decide
 * "estamos descendo?". Ele marca a decisão em `<html class="um-retraido">` e o
 * CSS faz o resto: o cabeçalho sobe (`.um-header--hidden`) e TODA barra marcada
 * com `data-um-barra` sobe junto (`.um-bar-retratil`). Uma segunda barra com
 * listener próprio andaria fora de fase com o cabeçalho — e barra grudada a 72px
 * de um cabeçalho que já saiu de cena é uma faixa flutuando sobre o conteúdo,
 * que foi exatamente o defeito que originou este arquivo virar padrão.
 *
 * ⚠️ SÓ ESCONDE O QUE ESTÁ PRESO. Barra sticky que ainda está no fluxo (a página
 * ainda não chegou nela) não pode ser transladada: ela pularia para cima e
 * cavalgaria o conteúdo de antes. Por isso o CSS exige `.is-presa`, e quem afere
 * isso é este módulo, comparando o topo medido com o `top` que o CSS pediu —
 * sem sentinela inserida no DOM, que a sincronização do Blazor removeria.
 *
 * ⚠️ ORNAMENTO, E POR ISSO OPCIONAL. Sem este arquivo o cabeçalho e as barras
 * ficam sempre visíveis. Nada aqui esconde conteúdo nem bloqueia gesto.
 */
(function () {
    'use strict';

    var REVEAL_AT = 80;   // só some depois de a página ter descido de verdade
    var TOP_LOCK  = 8;    // no topo, nunca escondido
    var DELTA     = 5;    // ignora tremida de dedo/trackpad

    var RAIZ = document.documentElement;
    var CLASSE_RETRAIDO = 'um-retraido';
    var SELETOR_BARRA = '[data-um-barra]';

    var header = null;
    var barras = [];
    var lastY = window.pageYOffset || document.documentElement.scrollTop || 0;
    var ticking = false;

    function getHeader() {
        if (header && document.contains(header)) return header;
        header = document.querySelector('.um-header');
        return header;
    }

    function varre() {
        barras = Array.prototype.slice.call(document.querySelectorAll(SELETOR_BARRA));
        marcaPresas();
    }

    function marcaPresas() {
        for (var i = 0; i < barras.length; i++) {
            var b = barras[i];
            if (!document.contains(b)) continue;
            // O `top` do CSS resolve `var(--um-header)`, que muda de valor no
            // celular: ler o computado é o que mantém a aferição honesta em
            // qualquer largura.
            var alvo = parseFloat(window.getComputedStyle(b).top);
            if (isNaN(alvo)) alvo = 0;
            b.classList.toggle('is-presa', b.getBoundingClientRect().top <= alvo + 1);
        }
    }

    function aplica(retrair) {
        var h = getHeader();
        if (h) h.classList.toggle('um-header--hidden', retrair);
        RAIZ.classList.toggle(CLASSE_RETRAIDO, retrair);
    }

    function onFrame() {
        ticking = false;
        var y = window.pageYOffset || document.documentElement.scrollTop || 0;

        if (barras.length) marcaPresas();

        if (y <= TOP_LOCK) {
            aplica(false);
            lastY = y;
            return;
        }

        // Menu, dropdown da conta e painel do sino estão ancorados no header.
        // Retrair com um deles aberto arrastaria o dropdown para fora da tela e
        // deixaria o painel do sino flutuando preso a um header que sumiu.
        // O mesmo vale para o aviso que gruda sob o header e não pode sumir
        // (`data-segura-cabecalho`, enquanto não estiver `hidden` — a pergunta
        // "Está tudo bem?" da corrida do entregador): com o header recolhido ele
        // flutuaria a uma altura de header do topo, com o conteúdo por cima.
        if (document.querySelector('.um-menu.open, [data-segura-cabecalho]:not([hidden])')) {
            aplica(false);
            lastY = y;
            return;
        }

        var dy = y - lastY;
        if (Math.abs(dy) < DELTA) return;

        if (dy > 0 && y > REVEAL_AT) aplica(true);
        else if (dy < 0) aplica(false);

        lastY = y;
    }

    window.addEventListener('scroll', function () {
        if (!ticking) {
            ticking = true;
            window.requestAnimationFrame(onFrame);
        }
    }, { passive: true });

    /* Foco de teclado NUNCA pode cair em controle escondido: quem chega ao
       cabeçalho ou a uma barra pelo Tab traz a barra de volta na hora. */
    document.addEventListener('focusin', function (e) {
        if (!RAIZ.classList.contains(CLASSE_RETRAIDO)) return;
        var alvo = e.target;
        if (alvo && alvo.closest && alvo.closest('.um-header, ' + SELETOR_BARRA)) aplica(false);
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', varre);
    } else {
        varre();
    }

    /* ⚠️ `enhancedload` NÃO é evento de `document` — é despachado no barramento do
       próprio Blazor. Registrado em `document` ele nunca dispara. */
    if (window.Blazor && window.Blazor.addEventListener) {
        window.Blazor.addEventListener('enhancedload', varre);
    }

    /* E a rede de baixo: o diff do SignalR troca pedaços do corpo sem evento de
       navegação nenhum — trocar de aba numa página é exatamente isso. */
    new MutationObserver(function (muts) {
        for (var i = 0; i < muts.length; i++) if (muts[i].addedNodes.length) { varre(); break; }
    }).observe(document.body, { childList: true, subtree: true });

    window.addEventListener('resize', marcaPresas, { passive: true });
})();
