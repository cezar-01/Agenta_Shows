// Registro do service worker + o convite de INSTALAR.
//
// O navegador só oferece o prompt de instalação (beforeinstallprompt) uma vez por carregamento
// e SÓ se alguém o segurar: sem o preventDefault o Chrome mostra o mini-infobar dele, e o gesto
// "Instalar o app" da tela não tem o que disparar. iOS NUNCA dispara este evento — lá o caminho
// é o passo a passo manual (Compartilhar → Adicionar à Tela de Início), que o NotificationNag e
// a central de notificações já prescrevem. Por isso `podeInstalar` é falso no iPhone, de
// propósito: um botão que abre nada é pior que nenhum botão.
(function () {
    var adiado = null;
    var instalado = false;

    function jaInstalado() {
        try {
            return instalado
                || window.navigator.standalone === true
                || (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches);
        } catch (e) { return false; }
    }

    async function verificarInstalado() {
    if (jaInstalado()) return true;

    try {
        if (typeof navigator.getInstalledRelatedApps !== "function") {
            return false;
        }

        const apps = await navigator.getInstalledRelatedApps();

        const umuraInstalada = apps.some(app =>
            app.platform === "webapp" &&
            typeof app.url === "string" &&
            new URL(app.url, window.location.origin).pathname
                .endsWith("/manifest.webmanifest")
        );

        if (umuraInstalada) {
            instalado = true;
        }

        return umuraInstalada;
    } catch {
        return false;
    }
    }

    try {
        window.addEventListener('beforeinstallprompt', function (ev) {
            ev.preventDefault();
            adiado = ev;
        });
        window.addEventListener('appinstalled', function () {
            instalado = true;
            adiado = null;
        });
    } catch (e) { }

    if ('serviceWorker' in navigator) {
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/service-worker.js').catch(function (err) {
                console.warn('Falha ao registrar o service worker:', err);
            });
        });
    }

    window.umPwa = {
        // Verdadeiro só quando o navegador OFERECEU o prompt e o site ainda não está instalado.
        podeInstalar: function () { return adiado !== null && !jaInstalado(); },
        instalado: jaInstalado,
        verificarInstalado: verificarInstalado,
        // iPhone/iPad (inclusive o iPad que se diz Mac): lá a instalação é manual, pelo Compartilhar do Safari.
        ehIos: function () {
            try {
                var ua = navigator.userAgent || '';
                return /iPad|iPhone|iPod/.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1);
            } catch (e) { return false; }
        },
        // 'accepted' | 'dismissed' | 'unavailable'. O prompt só pode ser usado UMA vez: aceito ou
        // não, o evento é descartado — o navegador dispara outro quando achar que deve.
        instalar: function () {
            var ev = adiado;
            if (!ev) return Promise.resolve('unavailable');
            adiado = null;
            try {
                ev.prompt();
                return ev.userChoice.then(function (r) {
                    var outcome = r && r.outcome === 'accepted' ? 'accepted' : 'dismissed';
                    if (outcome === 'accepted') instalado = true;
                    return outcome;
                }).catch(function () { return 'dismissed'; });
            } catch (e) { return Promise.resolve('unavailable'); }
        }
    };
})();
