using Agenda_Shows.Contexts;
using Agenda_Shows.Services.Demo;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Agenda_Shows.UI.Data;

/// <summary>
/// O que o app faz com o banco ao subir: aplica as migrations, planta os dados de demonstração e (em dev) traz as datas
/// para hoje. É isso que faz o F5 do Visual Studio funcionar sem comando nenhum.
/// </summary>
public static class Boot
{
    public const string ChaveMigrar = "Database:MigrateOnBoot";
    public const string ChaveSemear = "Demo:Semear";
    public const string ChaveDatasRolantes = "Demo:DatasRolantes";

    public static async Task PrepararBancoAsync(WebApplication app)
    {
        var dev = app.Environment.IsDevelopment();
        var migrar = app.Configuration.GetValue<bool?>(ChaveMigrar) ?? true;
        var semear = app.Configuration.GetValue<bool?>(ChaveSemear) ?? dev;
        var rolar = app.Configuration.GetValue<bool?>(ChaveDatasRolantes) ?? dev;

        await using var escopo = app.Services.CreateAsyncScope();
        var fabrica = escopo.ServiceProvider.GetRequiredService<IDbContextFactory<MainContext>>();

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                if (migrar)
                {
                    await using var db = await fabrica.CreateDbContextAsync();
                    // As migrations são do MySQL; a pré-visualização em SQLite cria o esquema direto do modelo.
                    if (Banco.EhPreviaSqlite(app.Configuration.GetConnectionString("DefaultConnection")))
                        await db.Database.EnsureCreatedAsync();
                    else
                        await db.Database.MigrateAsync();
                }
                if (semear)
                {
                    var semeadura = escopo.ServiceProvider.GetRequiredService<Semeadura>();
                    await semeadura.SemearAsync(app.Configuration[DadosDaDemo.ChaveDaSenha]);
                    if (rolar) await semeadura.AtualizarDatasAsync();
                }
                return;
            }
            catch (Exception ex) when (EhBancoFora(ex) && tentativa < 4)
            {
                app.Logger.LogWarning("MySQL indisponível no boot (tentativa {T}/4) — nova tentativa em 5s…", tentativa);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) when (EhBancoFora(ex))
            {
                // Sobe mesmo assim: as páginas mostram o erro, e o log diz o que fazer. Derrubar o processo esconderia a mensagem.
                app.Logger.LogError("Não consegui falar com o MySQL. Ele está rodando? `docker compose -f docker/mysql/docker-compose.yml up -d --wait` " +
                                    "e confira a ConnectionStrings:DefaultConnection nos user-secrets (docker/mysql/README.md). Erro: {Erro}", ex.Message);
                return;
            }
        }
    }

    private static bool EhBancoFora(Exception ex) =>
        ex is MySqlException || ex.InnerException is MySqlException || ex is InvalidOperationException { InnerException: MySqlException }
        || ex.GetType().Name.Contains("RetryLimitExceeded");
}
