# MySQL da UMURA (Docker, porta 3307)

Nenhuma senha mora no repositório. Elas ficam em dois lugares fora do git:

| O quê | Onde |
|---|---|
| Senhas do MySQL (root e usuário do app) | `docker/mysql/.env` (modelo: `.env.example`) |
| String de conexão do app + senha das contas de demonstração | user-secrets do projeto UI (`secrets.json` do seu usuário) |

## Primeira vez

1. `cp docker/mysql/.env.example docker/mysql/.env` e troque as senhas (já foi feito nesta máquina).
2. Subir: `docker compose -f docker/mysql/docker-compose.yml up -d --wait`
3. Conectar o app (use a MESMA senha do `MYSQL_PASSWORD` do `.env`):
   `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "server=127.0.0.1;port=3307;user=umura;password=<MYSQL_PASSWORD>;database=umura" --project src/Agenda_Shows.UI`
4. Senha das contas de demonstração: `dotnet user-secrets set "Demo:Senha" "<uma senha>" --project src/Agenda_Shows.UI`
5. Rodar pelo Visual Studio (F5, perfil `https` ou `http`): em Development o app aplica as migrations e semeia os
   dados de demonstração sozinho.

Ver o que está configurado: `dotnet user-secrets list --project src/Agenda_Shows.UI`
(no Visual Studio: botão direito no projeto Agenda_Shows.UI → **Gerenciar Segredos do Usuário**).

## Dia a dia

- **Derrubar (mantém os dados):** `docker compose -f docker/mysql/docker-compose.yml down`
- **Resetar (apaga tudo):** `docker compose -f docker/mysql/docker-compose.yml down -v` e suba de novo
- **Entrar no banco:** `docker exec -it umura-mysql mysql -uumura -p umura` (a senha é a do `.env`)
- Só aceita conexão da própria máquina (`127.0.0.1`).
