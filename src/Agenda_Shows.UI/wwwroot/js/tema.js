// O modo claro/escuro escolhido à mão. Sem escolha, quem decide é o prefers-color-scheme do aparelho (o app.css
// já trata). Este arquivo carrega no <head> SEM defer de propósito: aplicar o data-theme antes da primeira pintura
// é o que evita a página piscar no modo errado. É externo porque a CSP não aceita script inline.
(function () {
    'use strict';

    var CHAVE = 'umura.tema';
    var raiz = document.documentElement;

    function lido() {
        try { return localStorage.getItem(CHAVE); } catch (e) { return null; }
    }

    function aplica(tema) {
        if (tema === 'light' || tema === 'dark') raiz.setAttribute('data-theme', tema);
        else raiz.removeAttribute('data-theme');
    }

    aplica(lido());

    function atual() {
        var t = raiz.getAttribute('data-theme');
        if (t) return t;
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    window.umTema = {
        atual: atual,
        alternar: function () {
            var novo = atual() === 'dark' ? 'light' : 'dark';
            aplica(novo);
            try { localStorage.setItem(CHAVE, novo); } catch (e) { /* aba privada: vale só nesta visita */ }
            return novo;
        },
        seguirAparelho: function () {
            aplica(null);
            try { localStorage.removeItem(CHAVE); } catch (e) { }
        }
    };

    // O botão do cabeçalho: qualquer elemento com data-um-tema alterna e marca aria-pressed.
    document.addEventListener('click', function (e) {
        var b = e.target && e.target.closest ? e.target.closest('[data-um-tema]') : null;
        if (!b) return;
        window.umTema.alternar();
        marcaBotoes();
    });

    function marcaBotoes() {
        var pressionado = atual() === 'dark' ? 'true' : 'false';
        var botoes = document.querySelectorAll('[data-um-tema]');
        for (var i = 0; i < botoes.length; i++) botoes[i].setAttribute('aria-pressed', pressionado);
    }

    function reaplica() {
        var escolhido = lido();
        if ((escolhido === 'light' || escolhido === 'dark') && raiz.getAttribute('data-theme') !== escolhido) aplica(escolhido);
        marcaBotoes();
    }

    if (window.MutationObserver) {
        new MutationObserver(reaplica).observe(raiz, { attributes: true, attributeFilter: ['data-theme'] });
    }

    document.addEventListener('DOMContentLoaded', function () {
        reaplica();
        if (window.Blazor && window.Blazor.addEventListener) window.Blazor.addEventListener('enhancedload', reaplica);
    });
})();
