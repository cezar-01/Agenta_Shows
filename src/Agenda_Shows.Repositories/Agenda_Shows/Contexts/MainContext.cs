using Agenda_Shows.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Contexts;

/// <summary>
/// O banco da UMURA (MySQL 8 + Pomelo). Todo acesso é por <c>IDbContextFactory&lt;MainContext&gt;</c>, um contexto curto
/// por operação — nunca um contexto por circuito do Blazor Server (o crash de concorrência que o MyPods pagou).
/// As chaves do Data Protection moram aqui também: em container sem volume, sem isso cada deploy invalidaria o cookie
/// de login e o antiforgery de todo mundo.
/// </summary>
public class MainContext(DbContextOptions<MainContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<TipoDeIngresso> TiposDeIngresso => Set<TipoDeIngresso>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemDoPedido> ItensDoPedido => Set<ItemDoPedido>();
    public DbSet<Ingresso> Ingressos => Set<Ingresso>();
    public DbSet<Favorito> Favoritos => Set<Favorito>();
    public DbSet<VisualizacaoDeEvento> Visualizacoes => Set<VisualizacaoDeEvento>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Papel).HasConversion<int>();
        });

        b.Entity<Categoria>(e => e.HasIndex(x => x.Slug).IsUnique());

        b.Entity<Evento>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            // A vitrine pergunta sempre "publicados, a partir de tal hora": o índice segue a pergunta.
            e.HasIndex(x => new { x.Status, x.InicioUtc });
            e.HasIndex(x => x.OrganizadorId);
            e.HasOne(x => x.Categoria).WithMany(c => c.Eventos).HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Organizador).WithMany(u => u.Eventos).HasForeignKey(x => x.OrganizadorId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Status).HasConversion<int>();
        });

        b.Entity<TipoDeIngresso>(e =>
        {
            e.HasOne(x => x.Evento).WithMany(ev => ev.Ingressos).HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_Ingresso_Vendidos", "`Vendidos` >= 0 AND `Vendidos` <= `Quantidade`"));
        });

        b.Entity<Pedido>(e =>
        {
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasIndex(x => new { x.UsuarioId, x.CriadoEmUtc });
            e.HasIndex(x => new { x.Status, x.ExpiraEmUtc });
            e.HasIndex(x => x.EventoId);
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Metodo).HasConversion<int>();
        });

        b.Entity<ItemDoPedido>(e =>
            e.HasOne(x => x.Pedido).WithMany(p => p.Itens).HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Cascade));

        b.Entity<Ingresso>(e =>
        {
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasIndex(x => x.EventoId);
            e.HasOne(x => x.Pedido).WithMany(p => p.IngressosEmitidos).HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Favorito>(e =>
        {
            e.HasKey(x => new { x.UsuarioId, x.EventoId });
            e.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<VisualizacaoDeEvento>(e => e.HasIndex(x => new { x.EventoId, x.QuandoUtc }));
    }
}
