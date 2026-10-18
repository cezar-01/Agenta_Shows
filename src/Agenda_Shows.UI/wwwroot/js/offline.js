// Página offline do service worker: diz a causa certa e sai da frente sozinha.
//
// (1) CAUSA. O fetch de navegação rejeita por duas razões que pedem gestos opostos: o
// aparelho está sem rede, ou a rede está boa e quem não respondeu foi o servidor. A
// tela afirmava sempre a primeira, mandando quem tem internet perfeita — deploy,
// restart, processo derrubado — procurar defeito onde não tem. navigator.onLine não
// prova que existe internet (true só diz que há interface de rede), mas o FALSE é
// conclusivo: é essa assimetria que a mensagem usa.
//
// (2) SAÍDA. Esta página só aparece quando uma NAVEGAÇÃO falha, e aí a página anterior
// já morreu — não há o que preservar. O que dá pra fazer é não deixar o cliente parado
// num beco: a sonda insiste sozinha e recarrega no segundo em que o servidor responde.
// Como o service worker responde à navegação SEM redirecionar, location continua sendo
// a url pedida, então o reload devolve exatamente a página que ele queria.
(function () {
    var titulo = document.getElementById('um-off-t');
    var detalhe = document.getElementById('um-off-d');
    var acao = document.getElementById('um-off-a');
    if (!titulo || !detalhe) return;

    // Sem script, o href vazio já recarrega a url atual; com script, o clique vira uma
    // sondagem imediata (sem descartar a página caso o servidor ainda esteja fora).
    if (acao) {
        acao.setAttribute('href', window.location.href);
        acao.addEventListener('click', function (e) { e.preventDefault(); sondar(true); });
    }

    // Normalmente esta página é servida NO LUGAR da url pedida (o service worker
    // responde sem redirecionar), então voltar é recarregar a mesma url. A exceção é
    // alguém abrir /offline.html direto: aí recarregar traria a própria página offline
    // de novo, em loop — nesse caso o destino é a home.
    var DESTINO = window.location.pathname === '/offline.html'
        ? '/'
        : window.location.href;

    var ESPERAS = [2000, 3000, 5000, 8000, 10000];
    var tentativa = 0;
    var emVoo = false;
    var timer = null;

    function semRede() { return navigator.onLine === false; }

    function pintar(sondando) {
        if (semRede()) {
            document.title = 'Sem conexão · UMURA';
            titulo.textContent = 'Você está sem conexão';
            detalhe.textContent = 'Seu aparelho está sem rede — volta sozinho quando reconectar.';
            return;
        }
        document.title = 'Reconectando · UMURA';
        titulo.textContent = 'A UMURA não respondeu';
        detalhe.textContent = sondando
            ? 'Sua conexão está ok — o problema é do nosso lado. Tentando de novo…'
            : 'Sua conexão está ok — o problema é do nosso lado. Nova tentativa em instantes.';
    }

    function agendar() {
        if (timer) clearTimeout(timer);
        var espera = ESPERAS[Math.min(tentativa, ESPERAS.length - 1)];
        timer = setTimeout(sondar, espera);
    }

    function sondar(agora) {
        if (emVoo) return;
        // Sem rede não adianta gastar sonda: o evento 'online' acorda a gente.
        if (semRede()) { pintar(false); return; }

        emVoo = true;
        pintar(true);

        // GET, e não HEAD: o roteamento de Razor Components não mapeia HEAD, então
        // /produtos respondia 404 e a sonda NUNCA teria sucesso — a página ficaria
        // tentando para sempre com o servidor de pé (pego no teste, não em revisão).
        //
        // Sondar a própria url é o que dá a resposta exata de que precisamos: "a página
        // que o cliente pediu já pode ser servida?". E não bate no cache do service
        // worker — fetch tem destination vazio, que não está em STATIC_DESTINATIONS, e
        // não é navegação; sondar um asset estático voltaria do cache mesmo com o
        // servidor fora, dando recuperação falsa.
        fetch(window.location.href, { method: 'GET', cache: 'no-store' })
            .then(function (res) {
                if (res && (res.ok || res.status === 401 || res.status === 403)) {
                    // Respondeu — inclusive um "precisa logar", que também é servidor vivo.
                    // replace e não assign: a página offline não fica no histórico, então
                    // o "voltar" do cliente não cai nela.
                    window.location.replace(DESTINO);
                    return;
                }
                throw new Error('sem resposta util');
            })
            .catch(function () {
                emVoo = false;
                tentativa++;
                pintar(false);
                agendar();
            });

        if (agora) return;
    }

    pintar(false);
    agendar();

    window.addEventListener('online', function () { tentativa = 0; sondar(true); });
    window.addEventListener('offline', function () { pintar(false); });
})();
