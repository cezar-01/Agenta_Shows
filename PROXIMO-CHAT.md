# PROXIMO-CHAT.md — checkpoint pra retomar (UMURA / Agenta_Shows)

> Atualizado em 2026-10-18 ~18:45. **Leia isto primeiro.** O plano aprovado está em `PLANO_UMURA.md` e o "como rodar" no `README.md`.

**Regras do Gabriel:**
1. O Google Doc da faculdade é SÓ LEITURA.
2. **NÃO COMMITAR NADA.**
3. **Nenhuma senha ou conexão no código.** Tudo fica nos user-secrets do projeto UI e no `docker/mysql/.env`, fora do git. Nunca leia nem imprima os valores.

## Pedidos

**17:05 (18/10):** imagens por nicho no código (não no banco), vitrine estilo iFood com eventos de exemplo, PWA, MySQL em Docker, rodar no VS, ao vivo / vai rolar, compra com pagamento SIMULADO e 3 contas: usuário, organizador (só o dele) e admin (tudo).

**18:20 (18/10):** "fotos muito zoadas, site de velho". Trocar por ilustrações limpas e minimalistas com bonequinhos, como as do conceito aprovado (`design/conceitos/home-mapa.html`). Feito.

## Feito e conferido

**Build e testes:** build com 0 avisos e 0 erros; testes **67/67**.

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

- [ ] Revisão adversarial de segurança (subagente rodando em 18/10 ~18:45). Se não houver resultado, refazer os itens:
  - papéis e IDOR;
  - pagamento;
  - CSRF e open redirect;
  - XSS no JSON-LD;
  - segredos;
  - vazamento no `/api/mapa`.
- [x] **Docker resolvido (18/10 ~19:40).** O disco do Docker foi pro `F:\DockerDesktop`. A imagem `mysql:8.0` tinha vindo com os binários zerados (sobra do disco cheio, erro "exec format error"); precisou de `docker system prune -a --volumes -f` e um pull novo. O container `umura-mysql` está healthy em 127.0.0.1:3307 com `restart: unless-stopped`. O app migrou e semeou (9 nichos, 45 eventos) e a home abriu em http://localhost:5147.
  - Se o F5 der "Unable to connect to any of the specified MySQL hosts", abra o Docker Desktop e rode `docker compose -f docker/mysql/docker-compose.yml up -d --wait`.
  - Se voltar o "exec format error", repita o prune e o pull.
  - A primeira subida do MySQL demora uns 2 min (o disco é lento).
- [ ] Opcional: aviso do EF "Skip/Take sem OrderBy" no boot. Algum `Take` está sem `OrderBy`; procurar no Catalogo e no AreaDoAdmin.
