using Microsoft.EntityFrameworkCore;

namespace ApiLoja;

public class ProdutoEntity
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Estoque { get; set; }
    public string? ImagemUrl { get; set; }
    public string Categoria { get; set; } = "Geral";
}

public class PedidoEntity
{
    public int Id { get; set; }
    public DateTime CriadoEm { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pendente";
    public string? CupomAplicado { get; set; }
    public decimal Desconto { get; set; }
    public List<ItemPedidoEntity> Itens { get; set; } = [];
}

public class ItemPedidoEntity
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public int ProdutoId { get; set; }
    public string NomeProduto { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}

public class CupomEntity
{
    public string Codigo { get; set; } = string.Empty;
    public decimal PercentualDesconto { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime? ExpiraEm { get; set; }
    public int? LimiteUsos { get; set; }
    public int UsosAtuais { get; set; }
}

/// <summary>
/// Contexto EF Core SQLite para persistência de produtos, pedidos e cupons.
/// </summary>
public class LojaDbContext(DbContextOptions<LojaDbContext> options) : DbContext(options)
{
    public DbSet<ProdutoEntity> Produtos => Set<ProdutoEntity>();
    public DbSet<PedidoEntity> Pedidos => Set<PedidoEntity>();
    public DbSet<ItemPedidoEntity> ItensPedido => Set<ItemPedidoEntity>();
    public DbSet<CupomEntity> Cupons => Set<CupomEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CupomEntity>().HasKey(c => c.Codigo);
        modelBuilder.Entity<PedidoEntity>()
            .HasMany(p => p.Itens)
            .WithOne()
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
