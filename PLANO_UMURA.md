# Plano UMURA — design + arquitetura do PWA de eventos culturais de Curitiba

> Plano APROVADO pelo Gabriel em 18/10/2026 (cópia do plano da sessão; a sequência de fases está na seção 13). Nada foi alterado no repo além do `Home.razor`/`EmptyLayout.razor` (conserto de build) e do `PROXIMO-CHAT.md` (checkpoint). O doc do Google Docs da faculdade **não é tocado em nenhuma fase**.

## Contexto

- Repo alvo: `C:\Users\cezar\OneDrive\Área de Trabalho\ProjetoFesta\Agenta_Shows` (`src/Agenda_Shows.slnx`, .NET 10, Blazor Web App Interactive Server). Hoje: template do Visual Studio + Bootstrap, `Agenda_Shows.Services` vazio, `Home.razor` = HTML do kit Urbura colado (sem CSS/JS/assets no `wwwroot`).
- Referência pra copiar: **MyPods** (`C:\Users\cezar\OneDrive\Área de Trabalho\project\feeefeg\MyPods`) — Blazor Server .NET 10, PWA, Leaflet, GSAP, design system por tokens, kit de gráficos SVG, 1.700 testes. Tudo abaixo que diz "copiar de" aponta pra um caminho real lá.
- Produto (doc da disciplina + reunião com Henrich): **site PWA** (não app nativo) onde o público abre e vê no **mapa** os eventos culturais/underground de Curitiba de hoje/próximos, filtra por categoria, abre o evento e vai pro ingresso; o **produtor** cadastra evento em ≤2 min colando um link e acompanha um **dashboard** de métricas.
- Marca: **UMURA** (PDF do Henrich, 01/10/2026). O kit Urbura (`Downloads/index (3).html`) só empresta a *estrutura* dos componentes (cards de evento, chips, campos, mapa, feedback); cores e fontes vêm da UMURA.

### Decisões já tomadas (respostas do Gabriel)

| Decisão | Escolha |
|---|---|
| Banco | **MySQL 8** + Pomelo (igual ao MyPods → `Repositories` copia sem adaptar) |
| Fonte de display | **Substituta livre agora** (Outfit, OFL, geométrica parecida com a Coco Gothic) + `@font-face` pronto pra trocar quando o Henrich mandar a Coco Gothic licenciada |
| Hospedagem | **Não decidida** → plano neutro: Dockerfile + GitHub Actions de build; a escolha entra na Etapa 5 do doc |
| App | PWA instalável (manifest + service worker), mobile-first a 390px |

---

## 1. Arquitetura alvo

```
src/
├── Agenda_Shows.slnx
├── Agenda_Shows.UI/            Blazor Server + Minimal API (Program.cs, Components/, Endpoints/, wwwroot/)
├── Agenda_Shows.Services/      regras de negócio: interfaces + DTOs + services (Eventos, Produtores, Extracao, Analytics)
├── Agenda_Shows.Repositories/  NOVO — EF Core 9 + Pomelo: MainContext, entidades, migrations
└── Agenda_Shows.Tests/         NOVO — xUnit + bUnit + fakes in-memory (nenhum teste toca MySQL)
```

Padrões herdados do MyPods (obrigatórios, cada um já custou um bug lá):
- **`IDbContextFactory<MainContext>`**, um contexto curto por operação — nunca `MainContext` scoped injetado em componente (crash de concorrência do Blazor Server). Molde: `MyPods.Repositories/.../CartRepository.cs`.
- Autorização **no service** (`EnsureProdutor`/`EnsureAdmin`) além do `[Authorize]` da página.
- Página de painel com `@rendermode @(new InteractiveServerRenderMode(prerender:false))`; lista pública carrega no `OnAfterRenderAsync(firstRender)` ou aceita o prerender (SEO precisa dele na página do evento).
- Assets fingerprintados (`MapStaticAssets`): mudou CSS/JS → rebuild + restart.
- **Uma `<HeadContent>` por página** (duas se apagam em silêncio) → `SeoHead.razor`.
- Migrations: auto só em Production (`Data/MigrationGate.cs`); em dev, `dotnet ef database update`.
- Bootstrap **sai** (`wwwroot/lib/bootstrap`, `app.css` do template, `MainLayout.razor.css`, `NavMenu.razor*`): tudo vira `um-*` no `app.css` próprio.
- **Nunca copiar o `Program.cs` do MyPods inteiro** (1.682 linhas, ~60 jobs, hosted services que abrem conexão no boot): nascer do esqueleto vazio e trazer bloco a bloco (~150 linhas úteis). Endpoints em `Endpoints/*Endpoints.cs` (um `MapXxxEndpoints` por módulo) + `Rotas.cs`.
- **Podar o multi-loja na cópia, não depois**: `StoreId`/`IEntidadeDaLoja`/`EscopoChave`/`ILojaAtual`/`ChaveDaLoja` saem de toda entidade, cache e serviço copiado (cada entidade copiada com `StoreId` vira migration a desfazer). `Theme/*`, `ThemeStyle.razor`, `IThemeService`, `MarcaNoHead` com `ILogoDaLoja` = **ignorar** (tema em runtime só existe porque a marca muda por loja; na UMURA os tokens são estáticos).
- `Net/CircuitLimits.cs` copiado (`MaximumReceiveMessageSize`): a lista de eventos do mapa **nunca** vai por `PersistentComponentState` (acima de 32 KB derruba o circuito em silêncio) — vai por `/api/mapa/eventos` em JSON, lido pelo JS do mapa.

Pacotes: `Pomelo.EntityFrameworkCore.MySql 9.0.0`, `MySqlConnector 2.4.0`, `Microsoft.EntityFrameworkCore(.Design/.Tools) 9.0.0`, `Microsoft.AspNetCore.Authentication.Google 10.0.x`, `Microsoft.Extensions.Identity.Core 9.0.0` (PasswordHasher), `AngleSharp 1.5.2` (parser do link), `SixLabors.ImageSharp 3.1.12` (thumbnail da imagem do evento); testes: `xunit 2.9.3`, `bunit 2.7.2`, `Microsoft.EntityFrameworkCore.InMemory 9.0.0`.

---

## 2. Identidade UMURA → tokens (`--um-*`, em `wwwroot/app.css`)

Padrão **claro** (o PDF é claro: 40% Off White), modo **escuro** por `prefers-color-scheme` e toggle manual (`<html data-theme="light|dark">`, lembrado em `localStorage` por `js/tema.js`). O MyPods é dark-only (zero `color-scheme`/`prefers-color-scheme` no app.css dele) — o esqueleto de tokens é novo, o resto do CSS é copiado.

### Cor

| Token | Claro | Escuro | Papel |
|---|---|---|---|
| `--um-bg` | `#EDECE7` Off White | `#0B3C3F` Deep Green | fundo do body |
| `--um-surface` | `#FFFFFF`-ish `#F6F5F1` | `#0F4A4D` | card, input, header |
| `--um-surface-2` | `#E4E2DB` | `#15575A` | hover, poço de imagem |
| `--um-border` / `--um-border-soft` | `#D3D1C8` / `#E0DED6` | `#1E6366` / `#15575A` | contorno / divisor |
| `--um-ink` | `#282828` Premium Grey | `#EDECE7` | texto principal |
| `--um-ink-soft` | `#3F3F3F` | `#D6D5CF` | corpo |
| `--um-ink-muted` | `#6E6F5E` Worker Green | `#A9B3A6` | **só** rótulos/metadata ≥ 14px/600 (4,33:1 sobre Off White — reprova em parágrafo) |
| `--um-brand` | `#0B3C3F` Deep Green | `#EDECE7` | títulos, header, CTA primário no claro |
| `--um-brand-ink` | `#EDECE7` | `#0B3C3F` | tinta sobre `--um-brand` |
| `--um-accent` | `#FF5A3C` Coral | `#FF5A3C` | **acento**: "hoje/agora", pino selecionado, barra de progresso, 1 por viewport |
| `--um-accent-ink` | `#282828` | `#282828` | tinta sobre coral (Premium Grey/Coral = 4,76:1) |
| `--um-accent-bg` | `rgba(255,90,60,.12)` | idem | chip "Hoje" |
| semânticos | `--um-success #1F8A4C`, `--um-warning #B8860B`, `--um-danger #C8372D`, `--um-info #2F6F9F` + `*-bg` a 12% | versões mais claras | status: verificado / horário alterado / cancelado / info. Sempre cor + ícone/texto |

Regras que o PDF obriga: coral **nunca** como texto pequeno sobre Off White (2,62:1); coral como fundo de botão com texto Premium Grey, ou como ícone/pino/linha ≥ 3 px; Off White sobre Deep Green (10,25:1) é o par de leitura do escuro. Cor é significado (acento = agora/ação, semântico = status); categorias de evento se distinguem por **ícone + texto no chip**, não por cor (acessibilidade + paleta de 5 cores só).

### Tipografia (self-hosted, `wwwroot/fonts`, `@font-face` no topo do `app.css`, CSP `font-src 'self'`)

- Display: `--um-display: 'Coco Gothic', 'Outfit', sans-serif` — Outfit (variável 100–900, OFL) entra agora; quando a Coco Gothic chegar, só adiciona o `@font-face` dela. Títulos em caixa alta no hero, tracking +.04em (como o PDF).
- Texto: `--um-font: 'Readex Pro', system-ui, sans-serif` (variável 160–700, OFL, baixar do Google Fonts como woff2 latin + latin-ext).
- Escala (do PDF, adaptada à web): display `clamp(2.4rem, 7vw, 4.5rem)`; h1 `clamp(2rem, 4vw, 2.75rem)`; h2 `clamp(1.5rem, 3vw, 2rem)`; corpo **16px mobile / 18px desktop** (o PDF pede 18–24 "leitura contínua"; 18 no corpo de página longa, 16 em lista densa); rótulos 12–14px. `tabular-nums` em todo número (hora, contagem, KPI).
- Mono só pra código/ID (`--um-mono`).

### Forma, ritmo, camadas (copiados do MyPods, renomeados)

Raios 8/14/20/pill; `--um-tap: 44px`; `--um-gutter`; `--um-sec-pad`; `--um-header` (com `safe-area-inset-top`); breakpoints **1024 / 980 / 900 / 640 / 480** (não criar outro); `--um-z-sticky/fab/drawer/header/sheet/dropdown/float/consent/critical`. Elevação no claro = sombra suave + borda (o oposto do dark: aqui a sombra lê); no escuro = superfície mais clara + borda.

### Componentes canônicos (classes `um-*`, extraídas do `app.css` do MyPods — linhas de referência: `.mp-header` 411, `.mp-btn` 1040, `.mp-card` 1221, `.mp-kpi` 1235, `.mp-badge` 1779, `.mp-input`, `.mp-sheet`, `.mp-table`, `.mp-alert`, `.mp-switch`, `.mp-visually-hidden`, `.mp-bar-retratil`, `.mp-mapa-*` 8546+)

`um-container`, `um-section` (`padding-block`, nunca `padding`), `um-sec-head` (eyebrow + h2 + sub), `um-btn` (pílula; `-primary` = Deep Green/Off White no claro, `-accent` = coral só pro CTA do evento "Ver ingresso", `-outline`, `-ghost`), `um-card`, `um-input`/`um-field`/`um-hint` (label sempre acima; 16px no mobile), `um-chip` (filtro, com `aria-pressed`), `um-badge` (status), `um-evento-card` (do kit Urbura: imagem 16:9 em poço, categoria, "HOJE · 20:30", título, "⌖ Largo da Ordem · Centro", rodapé com badge + ação), `um-sheet` (bottom sheet mobile), `um-alert`, `um-empty`, `um-skeleton` (shimmer, `css/loading.css`), `um-toast`, `um-table` (vira card ≤640), `um-kpi`, `um-graf-*`, `um-mapa-*` (pino, cluster, controle).

---

## 3. Mapa de telas (MVP) — mobile-first, 390px primeiro

| Rota | Arquétipo | O que tem |
|---|---|---|
| `/` | Vitrine | **Mapa de tela cheia** com os eventos de hoje/próximos 7 dias + barra superior (busca, chips de categoria, "Hoje/Fim de semana/Esta semana") + **bottom sheet** com a lista rolável (puxa pra cima = lista cheia; toca no pino = card do evento no sheet). Botão "⌖ Perto de mim". Desktop: mapa à direita (60%), lista à esquerda, sincronizados |
| `/eventos` | Vitrine | Lista com filtros (categoria, data, bairro, gratuito), chips aplicados, ordenação (mais perto, mais cedo), "Carregar mais" com total |
| `/eventos/{slug}` | Vitrine | Imagem, categoria + badges (Gratuito/Verificado/Cancelado), título, quando ("Hoje · 20:30", via `QuandoNoFeed`), onde (nome + endereço + mini-mapa estático), descrição, produtor, **CTA coral "Ver ingresso"** (clique contado), Compartilhar (`navigator.share`), Favoritar, "Informar alteração". SEO: `SeoHead` + JSON-LD `Event` + OG com imagem |
| `/hoje` | Vitrine | Atalho = `/eventos?quando=hoje` (link do manifest e da bottom-nav) |
| `/entrar`, `/criar-conta`, `/esqueci-senha` | Formulário | Card único ≤400px, Google opcional, erro genérico anti-enumeração |
| `/produtor` | Painel | Meus eventos (cards ≤640 / tabela desktop) com status, views e cliques por evento (sparkline), "Novo evento" |
| `/produtor/novo` | Formulário | **Cadastro express**: passo 1 "Cole o link do evento" → extração → passo 2 formulário pré-preenchido (título, data/hora, local com mapa-alfinete, categoria, preço/link, imagem) → "Publicar". Meta ≤2 min, cronômetro no teste |
| `/produtor/eventos/{id}` | Formulário | Editar / cancelar / duplicar |
| `/produtor/dashboard` | Painel | KPIs (views 7d, cliques no ingresso, taxa de clique, favoritos), linha views/dia, ranking de categorias em alta na cidade, faixa etária agregada (quando houver ≥5 respostas) |
| `/admin` | Painel | Moderação: verificar evento, destacar, cancelar, categorias |
| `/minha-conta`, `/privacidade`, `/termos`, `/offline.html`, `/not-found`, `/error` | — | copiados/adaptados do MyPods (`Pages/Legal/*`, `offline.html`) |

Navegação mobile: header compacto (logo + busca + avatar) que **recolhe ao rolar** (`nav-retract.js`) + **bottom-nav** 4 destinos (Mapa · Eventos · Favoritos · Conta/Produtor). Desktop: header 72px com nav.

---

## 4. Mapa (a tela principal)

- **Leaflet 1.9.4** vendorizado (copiar `wwwroot/js/vendor/leaflet/*` + LICENSE **e trazer a pasta `images/` do pacote oficial** — o MyPods não tem porque só usa `divIcon`; sem ela o cluster/`L.marker` padrão dá 404) + **Leaflet.markercluster 1.5.3** (MIT, novo, vendorizar JS+CSS+LICENSE). `.gitattributes` com `wwwroot/js/vendor/** -text` (o CSS oficial tem CRLF; normalizar quebra hash entre Windows e Linux — quebrou o CI do MyPods em 16/10). Sem npm, carregado só na página com mapa (mesmo truque do `mapa-alfinete.js`: `data-leaflet-js/css` no `<script>`).
- **Tiles pelo próprio site**: copiar `MyPods.UI/Geo/TilesDoMapa.cs` + `Endpoints/MapaEndpoints.cs` + o `AddHttpClient("tiles").RemoveAllLoggers()` e as `OpcoesDosTiles` do `Program.cs` (linhas 188-202) (cache em disco 7 dias, User-Agent com contato, 2 buscas simultâneas, pausa após falha, só Brasil). Mudanças: rota pública (sem `RequireAuthorization("EquipeDaLoja")`, senão visitante vê mapa cinza); `Mapa:Tiles:Site/Contato` explícitos no `appsettings`; rate limiter `mapa` **recalibrado** (no MyPods é 300 tiles/min por IP com `QueueLimit 0` → 429 vira quadrado cinza sem retry; um mapa full-screen com pan livre estoura — começar em ~1.500/min e medir); `Mapa:Tiles:Url` configurável (OSM pra dev/demo; na produção trocar por provedor com termos claros — MapTiler/Stadia free tier — sem mudar código). CSP continua `img-src 'self' data:` — por isso a **imagem do evento é baixada na extração e servida pelo endpoint próprio** (`ImagemEndpoints` + thumbnail), nunca hotlink de Sympla/Instagram.
- Modo escuro dos tiles: filtro CSS só no `.leaflet-tile-pane` (copiar `app.css` 8523-8565); nunca animar o container do mapa com `transform` (quebra o hit-test do Leaflet) — animar um wrapper.
- O proxy de tiles **fica sob `/api/`**: o service worker ignora `/api` por prefixo (não por origem) — mover pra `/mapa/tiles` faria o SW cachear tile como imagem cache-first sem limite.
- `js/mapa-eventos.js` (novo, no padrão do `mapa-alfinete.js`: contrato por `data-*`, `MutationObserver`, `ResizeObserver`, `prefers-reduced-motion`): recebe os eventos por `data-mapa-eventos="/api/mapa/eventos?..."` (JSON: id, slug, lat, lng, categoria, hoje, título, hora, thumb), desenha pinos `um-mapa-pino` (Deep Green; **coral** = selecionado ou "agora"), cluster, popup mínimo, `mapa:selecionou` → o Blazor abre o card no sheet (interop por `CustomEvent` + `DotNetObjectReference`, ou `@onclick` em elemento espelho — decidir na implementação).
- GPS: copiar `js/geoloc.js` (`window.mpGeo` → `umGeo`), estados `denied/timeout/insecure` com mensagem; o módulo do mapa chama `umGeo.get()` em vez de repetir `navigator.geolocation` (no MyPods está duplicado).
- Cadastro do produtor: **reusar `mapa-alfinete.js` como está** (alfinete arrastável + "Usar minha localização" + campo `lat,lng`; regra de ouro mantida: o módulo nunca escreve texto em nó que o Blazor renderiza com conteúdo), mais geocodificação do endereço digitado via `NominatimGeocoder.cs` + `IGeocoder`/`GeocodeQuery` copiados (SemaphoreSlim 1 req/s, UA com contato, **só do servidor, nunca do navegador, nunca autocomplete**) com cache em `GeoCacheEntry` (entidade copiada) — o alfinete corrige o que o geocoder errar. **Não existe reverse geocoding no MyPods**: escrever `/reverse` ("cravei o pino, preencha o endereço") — pequeno, mesma política.
- `GeoCuritiba`/`BairrosDeCuritiba`/`GeoArea` do `MyPods.Service/**/Geo` — copiar só o puro (constantes, `Normaliza`, `DistanciaKm`, ponto central e raio dos bairros = fallback "evento no bairro X"), cortar a delegação pra `GeografiaDaLoja`; ignorar OSRM/rotas/frete/censo/clima.

---

## 5. PWA

- `wwwroot/manifest.webmanifest` **estático** (sem multi-loja não precisa da rota; arquivo estático em `wwwroot` sombreia qualquer rota de mesmo nome, e o MIME `.webmanifest → application/manifest+json` entra no `StaticFileOptions`): `name` "UMURA — Um mapa vivo de Curitiba", `short_name` "UMURA", `id: "/"`, `start_url` "/?source=pwa" (o `source=pwa` vira origem "app instalado" no analytics), `display` standalone, `theme_color` `#0B3C3F`, `background_color` `#EDECE7`, ícones 192/512/maskable PNG (gerar do símbolo da UMURA — pedir SVG ao Henrich; enquanto isso, exportar do PDF), `categories: ["entertainment","events"]`, `shortcuts`: Hoje, Perto de mim.
- `<head>`: `theme-color` (segue o modo claro/escuro via `media`), `apple-touch-icon`, `apple-mobile-web-app-*` com `status-bar-style` **`black`/`default`** (nunca `black-translucent` num mapa full-screen), viewport com `viewport-fit=cover, interactive-widget=resizes-content` (copiar de `App.razor`).
- `service-worker.js` adaptado e **na raiz do `wwwroot`, registrado por `/service-worker.js` fixo** (sem `@Assets`/fingerprint, senão o scope não cobre `/`): precache só `offline.html` + `offline.js` + ícones; `navigate` → rede com fallback offline; estáticos cache-first com atualização; **nunca** `/api`, `/_blazor`, `/_framework` nem documento HTML (página cacheada = tela zumbi com circuito morto), nem os tiles (são `private`). Regra de ouro: mudou o SW ou qualquer precacheado → **renomeia o `CACHE`** (`umura-v1`, `v2`…), com sentinela de teste. Sem push no MVP (os handlers `push`/`pushsubscriptionchange` saem; voltam na fase 2 com `Lib.Net.Http.WebPush` + `VapidKeyProvider`, chave VAPID que nunca rotaciona).
- `js/pwa.js` copiado (segura o `beforeinstallprompt`, expõe `umPwa.podeInstalar/instalar`); banner "Instalar o UMURA" no sheet da home após 2ª visita; iOS = instrução "Compartilhar → Adicionar à Tela de Início".
- `offline.html` + `js/offline.js` adaptados (texto honesto "pode ser sua conexão ou o nosso servidor"; a sonda usa **GET**, não HEAD — Razor Components não responde HEAD).
- Instalar no Android: HTTPS + manifest com `name/short_name/start_url/display/id` + ícones 192/512 + SW com `fetch` → o Chrome dispara `beforeinstallprompt` depois de engajamento (o botão pode demorar a aparecer; isso é normal). iOS: nunca há prompt — instrução manual "Compartilhar → Adicionar à Tela de Início" (Safari lê o manifest desde o 15.4).

---

## 6. Motion (GSAP)

Copiar `js/vendor/gsap.min.js`, `ScrollTrigger.min.js` (3.13, grátis) e `js/mypods-motion.js` → `js/umura-motion.js` (trocar prefixo `data-mp-*` → `data-um-*`, cores dos `sparkBurst` pra coral). Dial por arquétipo (receitas V/F/P da skill `mypods-motion`): hero da home com coreografia 1× (eyebrow → display com blur 6→0 → sub → CTAs), `reveal-group` no grid de eventos, `card-in` nos formulários, contadores nos KPIs com stagger 100 ms, gráficos com `graf-*`. Teto 300 ms no utilitário; só `transform`+`opacity`; `prefers-reduced-motion` respeitado via CSS condicional + `gsap.matchMedia`. Sheet mobile 400 ms `cubic-bezier(0.32,0.72,0,1)`.

---

## 7. Dados (MySQL 8, EF Core 9, Pomelo)

Entidades (em `Agenda_Shows.Repositories/Entities`):
- `Produtor` (Id, Nome, Email único, SenhaHash, GoogleSub?, Papel Produtor|Admin, CriadoEm, EmailConfirmadoEm?)
- `Categoria` (Id, Slug, Nome, Icone) — seed: Shows, Festas, Teatro, Exposições, Feiras, Oficinas, Cinema, Cultura de rua, Literatura, Outros
- `Evento` (Id, Slug único, Titulo, Descricao, InicioUtc, FimUtc?, LocalNome, Endereco, Bairro, Lat, Lng, CategoriaId, Gratuito, PrecoTexto?, LinkIngresso?, ImagemId?, ProdutorId, Status Rascunho|Publicado|Verificado|Cancelado, Destaque bool, OrigemUrl?, CriadoEm, AtualizadoEm; índices: `(Status, InicioUtc)`, `Slug`, `ProdutorId`)
- `ImagemDoEvento` (Id, EventoId, Dados LONGBLOB, Tipo, Largura/Altura; thumbnail via `ImageThumbnailer` + endpoint com cache/ETag copiado de `Endpoints/ImagemEndpoints.cs`)
- `Favorito` (ProdutorId? ou AnonId cookie, EventoId, CriadoEm)
- `VisualizacaoDeEvento` (EventoId, QuandoUtc, Origem Mapa|Lista|Link|Share, EhProdutor bool) — **sem PII**
- `CliqueNoIngresso` (EventoId, QuandoUtc)
- `SiteSetting` (chave-valor, copiado) · `PerfilDoPublico` (opcional: FaixaEtaria enum + consentimento; agregado só com n ≥ 5)
- `PushSubscription` fica pra fase 2.

Infra copiada: `Contexts/Base/DbContextBase.cs` (FKs em `Restrict`, índices por `[Index]`/`[UniqueIndex]` — **num namespace próprio**, sem o `global::` que o MyPods precisou), `MainContext` implementando `IDataProtectionKeyContext` (**DataProtection persistida no banco é obrigatória em container sem volume**, senão cada deploy invalida cookie e antiforgery de todo mundo), `MainContextFactory` de design-time **sem** o fallback hard-coded de senha do MyPods, `ExtensionMethods.cs` (UseMySql + pooling + `EnableRetryOnFailure` + `CommandTimeout`, factory **scoped**), `MigrationGate.cs` (sem `PlataformaContext`), `GET_LOCK` na migration de boot, `SiteSetting` + repositório, `GeoCacheEntry`. Regras: toda data `DateTime.UtcNow` no banco; "hoje" é o dia de **Brasília** (`BrazilTime.DataEmBrasilia`) — copiar `MyPods.UI/Time/*` **pro projeto Services** (uma régua só; o MyPods tem duplicata no Service) e usar em TODO filtro hoje/amanhã/fim de semana, com teste "às 22h de Brasília ainda é hoje" (o processo roda em UTC: entre 21h e 0h o `UtcNow.Date` já é amanhã — é o bug mais provável do domínio, na hora de pico de quem procura rolê); exactly-once vai no `WHERE` do `ExecuteUpdate` (`TrySetStatusAsync`), nunca read-check em C#; service de edição oferece `GetBySlugAsync` (cache 5 min da vitrine) **e** `GetBySlugFreshAsync` (banco) — quem vai gravar nunca lê do cache; janela de consulta sempre `[since, until)` com `if (since is {} d) q = q.Where(...)` (o `|| since == null` dentro do WHERE derruba o índice).

Dev: MySQL 8 em Docker (`docker/mysql/docker-compose.yml`, porta 3307, igual ao `mypods-banco-teste`), connection string em user-secrets; seed de ~25 eventos plausíveis de Curitiba (Largo da Ordem, Rebouças, São Francisco, Batel) pra demo e prints.

---

## 8. Auth (sem 2FA no MVP)

O MyPods usa JWT HS256 em cookie + refresh token com rotação + `IClaimsTransformation` lendo o papel do banco — mais do que o MVP precisa. A UMURA usa o **cookie de autenticação do ASP.NET** (`AddAuthentication().AddCookie()`, HttpOnly, SameSite=Lax, Secure por ambiente, sliding 7 d) com `Events.OnRedirectToLogin` devolvendo **401 em `/api`** e redirect em página (sem isso o `fetch` do PWA recebe HTML de login) + Google como scheme externo opcional (igual ao `googleEnabled` do MyPods: sem chaves, o botão some e o app sobe; e-mail do Google só vale com `email_verified=true`; e-mail já ligado a outro `sub` é recusado). Senha com `PasswordHasher<Produtor>` + `PasswordPolicy` copiada (≥8, letra+número). Endpoints Minimal API copiados e enxugados de `Endpoints/AuthEndpoints.cs` (login 44-211, logout 213-240, reset 242-310, forgot 325-381), `GoogleAuthEndpoints.cs`, `Rotas.cs` (`SafeReturnUrl` que recusa `//`, `\` e caractere de controle, `MesmaOrigem`, `FixedTimeEquals`): `POST /api/auth/login|register|logout|forgot|reset`, `GET /api/auth/google`; login sempre roda `VerifyHashedPassword` (contra hash dummy do boot se a conta não existe) e responde o mesmo `error=invalid` — anti-enumeração e timing igual; reset com cooldown por conta (2 min) além do teto por IP. Rate limiter por IP copiado (`ClientPartitionKey` + `ClientIpResolver` honrando `X-Forwarded-For` só de proxy declarado em `Network:KnownProxies/TrustedProxyHops`), policies `login`/`recuperacao`/`google`/`img`/`mapa`/`extracao`/`csp`, antiforgery. `RevalidatingServerAuthenticationStateProvider` (30 min) + `QuedaDaSessao` copiado (circuito que perde papel recarrega a página — `<Routes/>` é estático e não sai sozinho). Papéis por claim `Produtor|Admin`. Confirmação de e-mail: copiar só `SendForNewAccountAsync/ConfirmAsync/ConfirmByCodeAsync/ResendAsync` do `EmailConfirmationService` (os outros 2/3 são troca de e-mail) e a fila `BackgroundEmailSender → EmailDispatchWorker → RetryingEmailSender → SmtpEmailSender` (sem SMTP configurado, loga o código — testável sem servidor).

---

## 9. Cadastro express (o critério "≤2 minutos" e "pouca dependência de terceiros")

`Agenda_Shows.Services/Extracao/ExtratorDeEvento`: recebe a URL → `HttpClient` com guarda anti-SSRF (só http/https, resolve DNS e recusa IP privado, 2 MB, 8 s, 2 redirects) → `AngleSharp` lê **JSON-LD `Event`** (Sympla, Eventbrite e a maioria emitem), senão **OpenGraph** (`og:title/description/image`, `event:start_time`), senão `<title>` + primeira imagem → `EventoExtraido` com `Confianca` por campo. **Sem terceiro pago**: Firecrawl/Microlink saem; Gemini API fica como **opcional** (fase 2) só pra preencher categoria/resumo quando faltar — isso mantém a dependência em "pouca/nenhuma", que é o que a rubrica da Etapa 4 pede. Testes com HTMLs gravados de 3 páginas reais (fixtures).

Fluxo na tela: cole o link → skeleton 1–3 s → formulário pré-preenchido com os campos de baixa confiança destacados → mapa-alfinete pro local → Publicar. Cronômetro de UX nos testes de validação com o Henrich.

---

## 10. Dashboard do produtor + analytics

- Registro **fire-and-forget** copiado de `MyPods.UI/Catalog/ProductAnalytics.cs` (singleton + `IServiceScopeFactory`, nunca bloqueia a página, exclui admin/produtor dono por claim) → `IEventoAnalytics.RegistrarView(eventoId, origem)`; chamado **só em `OnAfterRenderAsync(firstRender)`** (com prerender o `OnInitialized` roda 2× e conta em dobro — `VisitaDaVitrine.cs` é a régua). Modelo de `VisualizacaoDeEvento` = o `PageView` do MyPods: sem FK de pessoa, sem IP, sem user-agent, origem como enum, nunca URL com query/UTM.
- **Clique no ingresso é sombra do gesto**: `GET /ir/{slug}` conta (balde `ContadorMensal` em memória + descarrego por `UPDATE +=`, copiado) e responde **302 com `Cache-Control: no-store`** (301 em cache pula o contador); o redirect nunca depende da contagem.
- Consentimento LGPD: `CookieConsent.razor` + `Consent/ConsentCookie.cs` + `ConsentimentoDoCircuito.cs` + `js/consent.js` copiados — **duas fontes** (cookie do request no prerender + estado do circuito, porque `IHttpContextAccessor` é null dentro do circuito; o banner chama `Circuito.Definir()` ao aceitar, senão só mede no próximo load), cookie nunca HttpOnly, barra inferior com Aceitar/Recusar do mesmo peso; exige `/privacidade` e `/cookies`. Sem consentimento registra só contagem anônima sem cookie.
- **Job runner copiado**, não reinventado: `MyPods.UI/Jobs/JobDispatcher.cs` (BackgroundService, `PeriodicTimer` 10 s) + `MyPods.Service/**/Jobs/*` (`IJob`, `ExecutorDeJobs`, `EstadoDoJob`, `EscopoDoJob.Unico` com claim no banco) + um `CatalogoDeJobs` **escrito do zero** com 5 jobs (`eventos.encerrar-passados`, `eventos.publicar-agendado`, `analytics.descarrego`, `analytics.rollup`, `contas.expurgo-nao-confirmadas`) — nunca o catálogo de ~60 jobs do MyPods.
- Série mensal: `ComportamentoMensal` + rollup diário pelo job runner (o mês é substituído inteiro, nunca somado por cima; piso de 2 meses de bruto).
- "N pessoas olhando agora" (fase 2, ~60 linhas): `Presence/PresenceTracker.cs` + `PresenceCircuitHandler.cs` enxutos (conta circuitos vivos; descartar `PixThroughput` e campos de vendedor).
- Kit de gráficos **copiado inteiro** de `Components/Shared/Graficos/*` (SVG inline, sem lib, com estados carregando/vazio/falha e animação `graf-*`): `GraficoLinha` views/dia 30 d, `GraficoBarras` horizontal = ranking de categorias (cidade), `Sparkline` no card de cada evento, `Medidor` opcional. `TomDoGrafico` vira Deep Green / Coral / Worker Green.
- KPIs em `um-kpi` (2×2 no mobile), contador animado.

---

## 11. SEO e compartilhamento

Copiar `MyPods.UI/Seo/*` (`JsonLd`, `SeoUrls`, `SeoTexto`, `SitemapXml`, `SeoRobots`) e `Components/Shared/SeoHead.razor`. Novo `JsonLd.Evento` (schema.org `Event`: name, startDate, endDate, location Place+PostalAddress+geo, image, offers com url/price, organizer, eventStatus Scheduled|Cancelled). OG com imagem 1200×630 gerada da capa (ImageSharp) pra preview no WhatsApp/Instagram. `share.js` + `clipboard.js` copiados. Sitemap com os eventos publicados; canonical só com `App:PublicBaseUrl` (`PublicUrlProvider`).

---

## 12. Segurança, infra, CI, testes

- Imagem do evento: entidade com bytes + thumb + `ContentType` (molde `Banner.cs`), `ImageThumbnailer.cs` copiado como está (WebP 480 px, qualidade 75, `AutoOrient`, `MaxFrames=1` — GIF animado não estoura memória), um `MapGet` copiado de `ImagemEndpoints.cs` (`/api/produtos/img/{id}` → `/api/eventos/img/{id}?size=thumb|full`) com `IMemoryCache` + ETag + policy **`img` com dois baldes por IP** (thumb 400/min, full 120/min — `Program.cs` linha 808).
- `Program.cs` reescrito no molde do MyPods: cabeçalhos + **CSP** (`default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' (+ ws localhost em dev); manifest-src 'self'; worker-src 'self'; frame-ancestors 'none'; report-uri /api/csp-report`), `Permissions-Policy: geolocation=(self)`, rate limiter (`login`, `mapa`, `img`, `extracao` 10/min), `MapHealthChecks /health/live|ready`, `MapStaticAssets`, `UseAntiforgery`, forwarded headers por config.
- `Dockerfile` multi-stage copiado (non-root, 8080, `$PORT`), `.dockerignore`, `.github/workflows/build.yml` enxuto (test → build → imagem no GHCR, sem deploy automático), `.claude/launch.json` com `umura` (porta 5147) e `umura-banco-teste`.
- Testes desde a fase 0: services com fakes (`Fakes/InMemory*Repository`), `ExtratorDeEvento` com fixtures, `QuandoNoFeed`, `TilesDoMapa.Valido`; **sentinelas** copiadas/adaptadas: `FontesSelfHostedTests` (nenhum `fonts.g*`), `TokensSemLiteralTests` (hex em `.razor` = falha), `UmaHeadContentPorPaginaTests`, `ServiceWorkerPrecacheTests` (mudou precache → nome do cache muda).

---

## 13. Ordem de implementação

Cada tela passa pelo **gate de design** antes do código (seção 14). Fases, cada uma termina com build 0/0 + testes verdes + print no navegador a 390 e 1440:

**F0 — Fundação (sem tela nova ainda)**
Criar `Agenda_Shows.Repositories` e `Agenda_Shows.Tests`; remover Bootstrap/template; `app.css` com tokens `--um-*` claro/escuro + reset + componentes canônicos (portados do `app.css` do MyPods por blocos, nunca o arquivo inteiro de 8.790 linhas); fontes em `wwwroot/fonts` (+ OFL); `App.razor` (head com manifest/ícones/theme-color — **sem service worker ainda** —, ordem dos scripts: gsap → ScrollTrigger → umura-motion → blazor.web.js → defer: nav-retract, pagina-recua, nav-progress, img-loading, sheet, share, clipboard, geoloc, consent, tema); `MainLayout` (header fixo + main com `padding-top: var(--um-header)` + bottom-nav + footer); `Program.cs` mínimo na ordem certa (status pages → headers/CSP → `UseStaticFiles`+`MapStaticAssets` → `UseRouting` → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → `UseAntiforgery` → endpoints) com `ClientIpResolver`, antiforgery, health; `Time/*` no Services + `Autorizacao` (`EnsureAdmin/EnsureProdutor/EnsureDonoDoEvento`); `launch.json`; `Dockerfile`; CI. Checkpoint no `PROXIMO-CHAT.md`.

**F1 — Dados**
`DbContextBase` (só `Restrict` + índices por atributo), `MainContext` + DataProtection no EF, `ExtensionMethods` (Pomelo, factory scoped), `MigrationGate`, `SiteSetting` + `TryClaimAsync`, entidades, migration inicial, `docker/mysql`, seed de eventos, repositórios por `IDbContextFactory`, services `IEventoService`/`ICategoriaService` + fakes + testes (inclui o teste do "hoje" às 22h de Brasília).

**F2 — Vitrine pública (a demo da Etapa 6)**
Conceito aprovado → `/` (mapa + sheet + chips + GPS: `TilesDoMapa`, `MapaEndpoints`, `/api/mapa/eventos`, `mapa-eventos.js`, markercluster), `/eventos` (filtros + chips + carregar mais), `/eventos/{slug}` (SEO + JSON-LD + share + favorito + clique no ingresso), `/hoje`, legais, 404. Motion V1/V2.

**F3 — Produtor**
Auth (cookie + Google opcional + endpoints + rate limit + fila de e-mail em modo log), `/entrar`, `/criar-conta`, `/produtor` (meus eventos), imagem do evento (entidade + `ImageThumbnailer` + endpoint `img`), `/produtor/novo` (extração + formulário + `mapa-alfinete.js` + `NominatimGeocoder` só no submit + upload), editar/cancelar. Testes do extrator e do auth.

**F4 — Jobs, analytics e dashboard**
Job runner (5 jobs) → `VisualizacaoDeEvento`/`CliqueNoIngresso` fire-and-forget + `/ir/{slug}` 302 no-store + consentimento (+ `/cookies`) + `ContadorMensal`/rollup → kit de gráficos + `/produtor/dashboard` + sparkline nos cards. Motion P1/P2.

**F5 — PWA completo, admin, polimento, entrega**
Service worker + `offline.html` + `pwa.js` + banner "Instalar o UMURA" **só agora, com rotas e assets estáveis** (instalar cedo em testadores fixa versão velha na mão deles; cada mudança de precache = renomear o cache); `/admin` (verificar/destacar/cancelar/categorias); auditoria de acessibilidade (foco visível, 44 px, contraste); Lighthouse PWA ≥ 90; teste em staging atrás de proxy com `Network__TrustedProxyHops=1` (sem isso o 2º visitante já vê tile 429); README do repo; roteiro do vídeo de demonstração (exigência da Etapa 7/8); decisão de hospedagem.

Fase 2 (fora do MVP, já com o lugar reservado): Web Push "evento perto de você hoje", Gemini pra completar extração, recomendação por preferências, faixa etária no perfil, destaques pagos (Ads), tema em runtime.

---

## 14. Fluxo de design por tela (gate obrigatório)

1. Classificar a tela no arquétipo (vitrine / formulário / painel) e ler a referência correspondente da skill `mypods-design` (`references/vitrine-conversao.md`, `formulario-conta.md`, `painel-dados.md`, `craft.md`) — valem as regras, não as cores.
2. Montar o conceito em `design/conceitos/<tela>.html` a partir de um **`template-conceito-umura.html`** novo (copiado de `.claude/skills/mypods-design/assets/template-conceito.html` com os tokens `--um-*` claro/escuro, fontes Outfit/Readex Pro e grain removido) + `umura-motion` inline; conteúdo real (eventos do seed, bairros de Curitiba), estados vazio/erro/carregando.
3. Prints com Playwright a **390 px primeiro** e 1440 px (`fullPage`), mais o HTML navegável; apresentar e esperar o **"aprovado"** explícito. Ajuste pedido = volta no conceito.
4. Implementar no Blazor: `<style>` local só com `var(--um-*)`, zero hex; reutilizável vai pro `app.css`; rebuild + restart; comparar com o conceito no navegador.
5. Registrar a tela em `DESIGN_ESCOPO.md` do repo (mapa de rotas por arquétipo e estado), como o MyPods faz.

Entregável adicional desta fase: `.claude/skills/umura-design/` (SKILL.md + `references/identidade.md` com a tabela de tokens/contraste da seção 2) pra que todo chat futuro desenhe na identidade certa.

---

## 15. Verificação

- `dotnet build src/Agenda_Shows.slnx` 0 erros/0 avisos; `dotnet test src/Agenda_Shows.Tests` verde.
- Rodar `umura` pelo `launch.json` e abrir no navegador da sessão: `/`, `/eventos`, `/eventos/{slug}`, `/produtor/novo` a 390 (preset mobile) e 1440; checar tiles carregando em `/api/mapa/tiles/…` (sem violação de CSP no console), pinos/cluster, "Perto de mim", sheet, chips.
- PWA: `/manifest.webmanifest` 200, SW registrado, `beforeinstallprompt` no Chrome (desktop simula), `offline.html` com a rede desligada; Lighthouse PWA/Acessibilidade.
- SEO: `view-source` da página do evento com uma `HeadContent`, JSON-LD válido (Rich Results Test), OG com imagem.
- Cadastro express: 3 links reais (Sympla + 2) extraídos em < 5 s, formulário publicado em < 2 min cronometrados.
- Dashboard: views/cliques registrados sem bloquear a página; admin/produtor dono excluídos; sem PII no banco.
- Acessibilidade: Tab de ponta a ponta com foco visível; alvos ≥ 44 px; contraste conferido com os pares da seção 2; `prefers-reduced-motion` ligado = tudo visível sem animação.

---

## 16. Riscos e pendências

- **Fonte**: Outfit é provisória; pedir ao Henrich os arquivos + licença web da Coco Gothic (e o SVG do símbolo pros ícones do PWA).
- **Tiles OSM** em produção: a política do OSM não aceita uso pesado — trocar `Mapa:Tiles:Url` por provedor com free tier antes de divulgar.
- **Nominatim**: 1 req/s e UA com contato; cache por endereço; o alfinete é o plano B.
- **Blazor Server + PWA offline**: offline só mostra a casca (`offline.html`); conteúdo offline real exigiria WebAssembly/cache de API — fora do MVP, dizer isso no doc.
- **Doc da disciplina**: a stack da ideia (FlutterFlow/Supabase/Microlink/Apify/Mapbox/PostHog) não bate com este plano (Blazor/MySQL/Leaflet/extrator próprio). Vale alinhar na Etapa 5 pra não perder ponto — e a "dependência de terceiros" passa a ser **pouca**, o que corrige o risco da Etapa 4.
- **Hospedagem** aberta: o plano é neutro (Docker + GHCR); Railway/Render/VPS decidem na Etapa 5.
- A varredura paralela do MyPods (workflow `wf_7596fdf0-400`, 5 leitores + crítico) está incorporada: o crítico mudou a ordem (PWA por último), trouxe o job runner, o `ImageThumbnailer`, a policy `img`, o `CircuitLimits` e os riscos de fuso/proxy/tiles sob `/api`. Resultado bruto em `C:\Users\cezar\AppData\Local\Temp\claude\C--\545c3a1e-81c4-4d77-9d6e-80b38798df2a\tasks\ws94da6tf.output`.
- Alinhamento com a Etapa 5 do doc: registrar lá que o stack real é Blazor/MySQL/Leaflet/extrator próprio (não FlutterFlow/Supabase/Microlink), e que a dependência de terceiros é "pouca".

## Checkpoint pra retomar

`Agenta_Shows/PROXIMO-CHAT.md` (raiz do repo) tem o inventário do MyPods com caminhos; este plano é a sequência. Se o token acabar no meio de uma fase: anotar lá a fase e o último arquivo tocado.
