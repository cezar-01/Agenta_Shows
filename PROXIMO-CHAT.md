# PROXIMO-CHAT.md — checkpoint pra retomar (UMURA / Agenta_Shows)

> Atualizado em 2026-10-26. **Leia isto primeiro.** O plano aprovado está em `PLANO_UMURA.md` e o "como rodar" no `README.md`.

**Regras do Gabriel:**
1. O Google Doc da faculdade é SÓ LEITURA.
2. **NÃO COMMITAR NADA.**
3. **Nenhuma senha ou conexão no código.** Tudo fica nos user-secrets do projeto UI e no `docker/mysql/.env`, fora do git. Nunca leia nem imprima os valores.

## Pedidos

**17:05 (18/10):** imagens por nicho no código (não no banco), vitrine estilo iFood com eventos de exemplo, PWA, MySQL em Docker, rodar no VS, ao vivo / vai rolar, compra com pagamento SIMULADO e 3 contas: usuário, organizador (só o dele) e admin (tudo).

**18:20 (18/10):** "fotos muito zoadas, site de velho". Trocar por ilustrações limpas e minimalistas com bonequinhos, como as do conceito aprovado (`design/conceitos/home-mapa.html`). Feito.

**26/10 (Henrich, via Gabriel):** "só adicionar um bloco (área banner) entre o Ao vivo e o Hoje que fizesse direcionamento ao Mapa. Apenas um CTA". Feito:
- O componente é `Components/Shared/ChamadaDoMapa.razor`, chamado na `Home.razor` entre os dois trilhos.
- O CSS é `.um-mapa-cta` + `.mc-*` no `app.css`, só com tokens.
- Ele mostra um mapa de ruas desenhado em SVG, com o pino coral para o "ao vivo" e um link só, "Abrir o mapa" → `/mapa`.
- O texto usa a contagem de ao vivo e de hoje.
- Conferido a 390 e 1440 px, no claro e no escuro, e o clique leva ao mapa.

## Feito e conferido

**Build e testes:** build com 0 avisos e 0 erros; testes **106/106**, duas rodadas seguidas (26/10, noite).

**Telas:** conferidas no navegador a 390 e 1440 px, no claro e no escuro.
- Home iFood, evento (com JSON-LD), mapa (tiles e clusters), login.
- Compra de 3 meias-entradas → Pix simulado → 3 ingressos com QR → Meus ingressos.
- Usuário comum barrado em `/organizador` e `/admin`.
- Organizador vê só os 11 eventos dele, e `/organizador/evento/4` (de outro organizador) dá "não é um dos seus".
- Admin vê os 45 eventos, edita evento de qualquer organizador e mexe em usuários.

**Ilustrações:** em `design/ilustracoes/` (veja o README de lá).
- `gerar.py` tem uma cena por evento (45) e um avatar por nicho (9), com lugares reais de Curitiba.
- `render.mjs` renderiza no Chrome headless e `webp.py` grava os `.webp` em `wwwroot/img` com os MESMOS nomes de antes. O seed não mudou.
- `CREDITOS.md`, Termos, README e os textos do organizador ("Ilustração") foram atualizados.
- Sentinela nova: `Tests/Sentinelas/ImagensDaDemoSentinelTests.cs`, todo evento e nicho tem o seu `.webp`.
- Service worker trocado para `umura-v2`, pra limpar as fotos velhas do cache.
- A capa do evento e o banner usam `object-position: 50% 68%` (os bonequinhos ficam embaixo).

**PWA:** conferido num Chrome headless de verdade. O service worker fica ativo, o cache tem o offline, e `Page.getInstallabilityErrors` vem vazio. O painel de navegador embutido do app não suporta service worker; isso é normal.

**Também:** JSON-LD `schema.org/Event` (`UI/Seo/JsonLdEvento.cs`), páginas `/privacidade` e `/termos`, rate limit `login` e `/sair` com antiforgery.

**Perfil `previa-sem-docker`** no `launchSettings.json`, também em `.claude/launch.json` como `umura-previa`:
- Roda com SQLite local (`src/Agenda_Shows.UI/umura-previa.db`, ignorado pelo git) e porta 5160.
- A senha das contas continua vindo do user-secret `Demo:Senha`.

## Falta

- [x] Revisão de segurança (18/10). Dos 5 achados, 4 foram corrigidos com testes e 1 foi aceito:
  - papel congelado no circuito → `RevalidacaoDoPapel` + `PapelNoBanco`;
  - teto de 10 ingressos por pessoa/evento + 1 reserva em aberto;
  - `/sair` com antiforgery de verdade;
  - evento cancelado não paga, libera as reservas e não valida ingresso;
  - aceito: o cadastro avisa quando o e-mail já existe.
- [x] **Segurança, 2ª e 3ª verificação (26/10).** Corrigido com teste e conferido no navegador. Os workflows são `wf_2df6d93d-4ff`, `wf_b0605b11-822` e `wf_ec63942e-dd3` (journal em `~/.claude/projects/C--/545c3a1e-.../subagents/workflows/`).
  - **Papel lido do banco** (`Services/Auth/PapelNoBanco.cs`) também nas LEITURAS:
    - admin: painel, eventos, pedidos e contas;
    - organizador: `MeusAsync` decide pelo papel do banco;
    - `Pedidos.ObterAsync` só abre pedido alheio se o banco confirmar o admin;
    - o botão "datas da demo" passou para o serviço (`AreaDoAdmin.AtualizarDatasDaDemoAsync`).
  - **`Shared/SessaoViva.razor`** está em todas as páginas e ilhas interativas:
    - recarrega a página quando o circuito perde a identidade ou o papel diverge do banco;
    - testado: rebaixar o admin no banco com `/admin/usuarios` aberto leva a aba para "Acesso negado" sozinha em ~35 s.
  - **Abas:** `<meta name="um-sessao">` (App.razor) + `js/sessao.js`. Sair numa aba recarrega as outras (testado: a aba do admin caiu em /entrar).
  - **`Contas`:** o hash falso do login é calculado uma vez por processo (antes era um PBKDF2 por página).
  - **`RevalidacaoDoPapel`:** banco fora não desloga ninguém.
  - **Pedidos:**
    - `ChangeTracker.Clear()` no retry;
    - o evento é reconferido no UPDATE do lote;
    - o pagamento reconfere prazo e teto na hora da troca (2 pedidos de 6 não viram 12).
  - **Evento:**
    - salvar e cancelar não se atropelam (o salvar trava a linha);
    - o pedido pago de evento cancelado diz "Não vale: evento cancelado" (Pedido e Meus ingressos).
  - **`/sair`:**
    - o cookie antiforgery é Lax;
    - sem sessão, vai para a home;
    - com token velho, vai para `/minha-conta?sair=refazer`, que mostra um aviso.
  - **Senha do admin separada:** `Demo:SenhaAdmin`, e `Demo__SenhaAdmin` no render.yaml. No Development, sem ela, o admin usa a `Demo:Senha`.
  - **Tiles:**
    - só Curitiba e região;
    - fila de 256;
    - limite de buscas novas: 240/min por IP + orçamento global;
    - teto de 512 MB no cache em disco;
    - o mapa do alfinete fica preso na área.
  - **Visualizações:**
    - uma por visitante por evento a cada 30 min;
    - a chave é a conta, ou IP + navegador, guardada como HMAC só na memória;
    - o texto da Privacidade foi ajustado.
  - **Receita somada em long.**
  - **Aceito e documentado:** o teto de 10 é por CONTA (fechar exigiria confirmar e-mail) e o cadastro avisa quando o e-mail já existe.
  - **4ª verificação** (`wf_ec63942e-dd3`), também corrigida:
    - o `sessao.js` acompanha a navegação aprimorada;
    - a trava do salvar é o 1º comando da transação;
    - tiles: valida antes de cobrar, 120/min por IP fica abaixo do orçamento global de 180/min, e sem vaga serve a cópia velha;
    - o cache cheio ainda regrava o tile que já existe;
    - visualizações: teto de 20 por rede por evento a cada 30 min;
    - a limpeza da dedupe só remove a entrada que continua vencida;
    - README: num serviço do Render que já existe, criar `Demo__SenhaAdmin` à mão.
  - **Banner do Mapa:** prints em `design/prints/banner-mapa-{390,1440}-{claro,escuro}.png`.
- [x] **Docker resolvido (18/10 ~19:40).** O disco do Docker foi pro `F:\DockerDesktop`. A imagem `mysql:8.0` tinha vindo com os binários zerados (sobra do disco cheio, erro "exec format error"); precisou de `docker system prune -a --volumes -f` e um pull novo. O container `umura-mysql` está healthy em 127.0.0.1:3307 com `restart: unless-stopped`. O app migrou e semeou (9 nichos, 45 eventos) e a home abriu em http://localhost:5147.
  - Se o F5 der "Unable to connect to any of the specified MySQL hosts", abra o Docker Desktop e rode `docker compose -f docker/mysql/docker-compose.yml up -d --wait`.
  - Se voltar o "exec format error", repita o prune e o pull.
  - A primeira subida do MySQL demora uns 2 min (o disco é lento).
- [ ] Opcional: aviso do EF "Skip/Take sem OrderBy" no boot. Algum `Take` está sem `OrderBy`; procurar no Catalogo e no AreaDoAdmin.
