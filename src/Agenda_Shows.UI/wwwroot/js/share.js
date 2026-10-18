(function () {
    'use strict';

    // Compartilhar do evento: navigator.share onde existe (celular), senão copia o link —
    // reusa o umCopyToClipboard do clipboard.js, que já cobre o caminho legado do execCommand.
    window.umShare = {
        share: async function (title, text, url) {
            if (navigator.share) {
                try {
                    await navigator.share({ title: title, text: text, url: url });
                    return 'shared';
                } catch (e) {
                    // Fechar a folha de compartilhar é decisão, não erro — e não pode virar
                    // "link copiado" fantasma no fallback.
                    if (e && e.name === 'AbortError') return 'cancelled';
                }
            }
            var ok = window.umCopyToClipboard ? await window.umCopyToClipboard(url) : false;
            return ok ? 'copied' : 'failed';
        }
    };
})();
