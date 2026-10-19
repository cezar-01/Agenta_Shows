# UMURA — um mapa vivo de Curitiba

Site **PWA** (instala no celular como app, sem loja) para descobrir o que está rolando na cidade — shows, festas,
teatro, arte, feiras, oficinas, cinema, cultura de rua e sarau —, ver o que está **ao vivo agora**, e garantir o
ingresso com **pagamento simulado**. Projeto de extensão (Universidade Positivo · Tecnologia e Sociedade II) com o
parceiro Henrich. O plano completo está em [PLANO_UMURA.md](PLANO_UMURA.md).

Blazor Server (.NET 10) · EF Core 9 + MySQL 8 (Pomelo) · Leaflet + OpenStreetMap · GSAP · xUnit.

## Rodar no Visual Studio (F5)

1. **Docker Desktop ligado** e o MySQL no ar:
   `docker compose -f docker/mysql/docker-compose.yml up -d --wait`
2. **Segredos** (uma vez por máquina) — nada de senha no código; tudo nos *user-secrets* do projeto UI e no
   `docker/mysql/.env`, os dois fora do git. Passo a passo em [docker/mysql/README.md](docker/mysql/README.md).
   No Visual Studio: botão direito em **Agenda_Shows.UI → Gerenciar Segredos do Usuário**.
3. **F5** com o perfil `https` (ou `http`). No primeiro boot o app cria as tabelas, planta os dados de demonstração
   (9 nichos, 45 eventos com foto, vendas e visualizações) e, a cada boot em dev, traz as datas para hoje — sempre
   tem evento **ao vivo**, **hoje** e **em breve**.

Pela linha de comando: `dotnet run --project src/Agenda_Shows.UI --launch-profile http` · testes: `dotnet test src/Agenda_Shows.Tests`.

## Deploy temporário gratuito para demonstração (Render)

O repositório inclui `render.yaml` para criar um Web Service gratuito no Render a partir do Dockerfile de produção em `src/Dockerfile`. A compilação acontece no Render; não é necessário instalar Docker na máquina de desenvolvimento.

1. Envie o `render.yaml` e as alterações desejadas para o GitHub.
2. No [Render](https://dashboard.render.com/), escolha **New → Blueprint**, conecte este repositório e confirme a criação do serviço `umura-shows-demo`.
3. Na configuração inicial, informe um valor para `Demo__Senha`. Esse valor é solicitado pelo Render e não fica no repositório; ele será a senha das contas de demonstração listadas abaixo.
4. Aguarde o primeiro deploy e abra a URL `onrender.com` do serviço. No plano gratuito, o primeiro acesso depois de 15 minutos sem tráfego pode levar cerca de um minuto para acordar.

**Limitações do plano gratuito:** o serviço usa SQLite em armazenamento temporário. O Render apaga esse banco ao reiniciar, dormir ou fazer novo deploy; ao subir de novo, os dados iniciais de demonstração são semeados novamente. Contas criadas, compras e alterações feitas durante o teste não são persistentes. É adequado para uma apresentação, não para produção ou dados reais.

### Configuração que está funcionando (conferida em 18/10/2026)

| Peça | Como está |
|---|---|
| Docker Desktop | Disco das imagens em `F:\DockerDesktop` (Settings → Resources → Advanced → Disk image location). O C: tem pouco espaço livre e, quando enche, o Docker corrompe as imagens. |
| MySQL | Container `umura-mysql` (imagem `mysql:8.0`) em **`127.0.0.1:3307`**, com `restart: unless-stopped`: ele sobe sozinho quando o Docker Desktop abre. A primeira subida demora uns 2 min. |
| Senhas do MySQL | `docker/mysql/.env`, fora do git |
| Conexão do app | User-secret `ConnectionStrings:DefaultConnection` do projeto UI, apontando pra `127.0.0.1` porta `3307` |
| Senha das contas de teste | User-secret `Demo:Senha` |
| Ambiente | Os perfis `http` e `https` usam `ASPNETCORE_ENVIRONMENT=Development`. **Só em Development o app lê os user-secrets.** |
| Endereços | `http` → http://localhost:5147 · `https` → https://localhost:7123 |
| Primeiro boot | Aplica a migration `Inicial` e semeia 9 nichos, 45 eventos, 333 pedidos e 3812 visualizações |

Pra conferir se o MySQL tá no ar: `docker ps`. Tem que aparecer `umura-mysql ... (healthy)`.

### Deu erro?

| Erro | Causa | Solução |
|---|---|---|
| `Unable to connect to any of the specified MySQL hosts` / `MySQL indisponível no boot` | O MySQL não está rodando | Abra o Docker Desktop, rode `docker compose -f docker/mysql/docker-compose.yml up -d --wait` e aperte F5 de novo |
| Container reiniciando e `docker logs umura-mysql` mostra `exec format error` | A imagem baixou corrompida (arquivos zerados, sobra de disco cheio). Apagar só a imagem não resolve, porque o Docker reaproveita o cache quebrado. | `docker compose -f docker/mysql/docker-compose.yml down -v`, depois `docker system prune -a --volumes -f` (apaga **todas** as imagens e volumes do Docker), depois `docker pull mysql:8.0`, e suba de novo |
| `unhealthy` logo depois do `up --wait` | Na primeira vez o MySQL ainda está criando os arquivos do banco, e o disco é lento | Espere 1–2 min e confira com `docker ps` até aparecer `(healthy)` |
| `Falta ConnectionStrings:DefaultConnection` | O app rodou fora de Development (sem perfil), então não leu os user-secrets | Rode pelo perfil `http`/`https` do VS ou use `--launch-profile http` |
| Porta 5147 em uso | Já tem outra instância do app aberta | Feche a outra janela do `Agenda_Shows.UI.exe` |

Sem Docker, dá pra ver o site com o perfil **`previa-sem-docker`** (SQLite local em `src/Agenda_Shows.UI/umura-previa.db`, porta 5160).

## Contas de teste

A senha de todas é a chave **`Demo:Senha`** dos user-secrets do projeto UI
(`dotnet user-secrets list --project src/Agenda_Shows.UI`). Ela não está escrita em lugar nenhum do repositório.

| E-mail | Tipo | O que enxerga |
|---|---|---|
| `admin@umura.dev` | Admin | Tudo: painel geral, todos os eventos (destacar, cancelar, editar), usuários (mudar papel), pedidos |
| `cena@umura.dev` | Organizador (Coletivo Cena Livre) | Só os eventos dele: criar, editar, cancelar, vendas, validar ingresso na porta |
| `palco@umura.dev` | Organizador (Produtora Palco Aberto) | Idem, só os dele |
| `feira@umura.dev` | Organizador (Feira Viva Curitiba) | Idem, só os dele |
| `ana@umura.dev`, `bruno@umura.dev`, `carla@umura.dev` | Usuário | Vitrine, favoritos, compra de ingresso, meus ingressos |

Qualquer pessoa também pode criar conta em `/criar-conta` (e marcar "quero divulgar meus eventos" para virar organizador).

## Pagamento simulado

Nenhum valor é cobrado e nenhum dado de cartão é guardado.

- **Pix:** QR de demonstração + botão **"Simular pagamento aprovado"**.
- **Cartão:** só os números de teste — `4242 4242 4242 4242` aprova, `4000 0000 0000 0002` recusa (qualquer validade
  futura e CVV de 3 dígitos). Cartão de verdade é recusado.
- A reserva segura os ingressos por **15 minutos**; sem pagar, eles voltam para o lote.

## Instalar como app (PWA)

- **Android/Chrome:** o próprio site oferece "Instalar" depois de um tempinho de uso (ou menu ⋮ → Instalar app).
- **iPhone/Safari:** Compartilhar → **Adicionar à Tela de Início**.

O PWA precisa de HTTPS (o perfil `https` do Visual Studio já serve).

## Onde está cada coisa

| Pasta | O quê |
|---|---|
| `src/Agenda_Shows.UI` | Blazor (páginas em `Components/Pages`), `Program.cs`, `wwwroot` (CSS `app.css` com os tokens `--um-*`, ilustrações em `img/`, PWA) |
| `src/Agenda_Shows.Services` | Regras: catálogo, pedidos/pagamento simulado, área do organizador e do admin, dados da demonstração |
| `src/Agenda_Shows.Repositories` | EF Core: entidades, `MainContext`, migrations |
| `src/Agenda_Shows.Tests` | xUnit (SQLite em memória — nenhum teste precisa do MySQL) |
| `docker/mysql` | MySQL 8 para desenvolvimento |

Imagens: ilustrações flat próprias, geradas em `design/ilustracoes/` (veja o README de lá e `src/Agenda_Shows.UI/wwwroot/img/CREDITOS.md`).
Marca: diretrizes UMURA (Henrich, 01/10/2026). A fonte de título é a Outfit até chegar a Coco Gothic licenciada.
