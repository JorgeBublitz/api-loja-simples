using Microsoft.EntityFrameworkCore;

namespace ApiLoja;

public record Produto(
    int Id,
    string Nome,
    decimal Preco,
    int Estoque,
    string? ImagemUrl = null,
    string Categoria = "Geral");

public record ProdutoDto(
    string Nome,
    decimal Preco,
    int Estoque,
    string? ImagemUrl = null,
    string? Categoria = null);

public record AjusteEstoqueDto(int Quantidade);
public record AtualizarStatusDto(string Status);
public record ValidarCupomDto(string Codigo);
public record LoginDto(string Usuario, string Senha);
public record CupomDto(
    string Codigo,
    decimal PercentualDesconto,
    DateTime? ExpiraEm = null,
    int? LimiteUsos = null);

public record ItemPedidoDto(int ProdutoId, int Quantidade);
public record ItemPedido(int ProdutoId, string NomeProduto, int Quantidade, decimal PrecoUnitario);

public record Pedido(
    int Id,
    DateTime CriadoEm,
    List<ItemPedido> Itens,
    decimal Total,
    string Status = "Pendente",
    string? CupomAplicado = null,
    decimal Desconto = 0m);

public record Cupom(
    string Codigo,
    decimal PercentualDesconto,
    bool Ativo = true,
    DateTime? ExpiraEm = null,
    int? LimiteUsos = null,
    int UsosAtuais = 0);

public record CategoriaResumo(string Nome, int QuantidadeProdutos);
public record ResumoLoja(
    int TotalProdutos,
    int TotalUnidadesEstoque,
    int ProdutosEstoqueBaixo,
    int TotalPedidos,
    decimal FaturamentoTotal,
    Dictionary<string, int> PedidosPorStatus);

/// <summary>
/// Domínio da loja: catálogo de produtos, cupons, pedidos e métricas (em memória ou persistido em SQLite).
/// </summary>
public class Loja
{
    private static readonly string[] StatusValidos = ["Pendente", "Pago", "Enviado", "Entregue", "Cancelado"];

    private readonly LojaDbContext? _db;
    private readonly List<Produto> _produtos = [];
    private readonly List<Pedido> _pedidos = [];
    private readonly List<Cupom> _cupons =
    [
        new("BEMVINDO10", 10m, true, null, 1000, 0),
        new("TECH20", 20m, true, null, 500, 0)
    ];

    private int _proximoIdProduto = 1;
    private int _proximoIdPedido = 1;

    public Loja() { }

    public Loja(LojaDbContext db)
    {
        _db = db;
        SemearCuponsIniciais();
    }

    private void SemearCuponsIniciais()
    {
        if (_db is null || _db.Cupons.Any()) return;
        _db.Cupons.AddRange(
            new CupomEntity { Codigo = "BEMVINDO10", PercentualDesconto = 10m, Ativo = true, LimiteUsos = 1000, UsosAtuais = 0 },
            new CupomEntity { Codigo = "TECH20", PercentualDesconto = 20m, Ativo = true, LimiteUsos = 500, UsosAtuais = 0 });
        _db.SaveChanges();
    }

    private static Produto ToRecord(ProdutoEntity e) =>
        new(e.Id, e.Nome, e.Preco, e.Estoque, e.ImagemUrl, e.Categoria);

    private static Cupom ToRecord(CupomEntity e) =>
        new(e.Codigo, e.PercentualDesconto, e.Ativo, e.ExpiraEm, e.LimiteUsos, e.UsosAtuais);

    private static Pedido ToRecord(PedidoEntity e) =>
        new(e.Id,
            e.CriadoEm,
            e.Itens.Select(i => new ItemPedido(i.ProdutoId, i.NomeProduto, i.Quantidade, i.PrecoUnitario)).ToList(),
            e.Total,
            e.Status,
            e.CupomAplicado,
            e.Desconto);

    public IReadOnlyList<Produto> Produtos =>
        _db is null ? _produtos : _db.Produtos.AsNoTracking().OrderBy(p => p.Id).Select(ToRecord).ToList();

    public IReadOnlyList<Pedido> Pedidos =>
        _db is null
            ? _pedidos
            : _db.Pedidos.AsNoTracking().Include(p => p.Itens).OrderBy(p => p.Id).Select(ToRecord).ToList();

    public IReadOnlyList<Cupom> Cupons =>
        _db is null ? _cupons : _db.Cupons.AsNoTracking().OrderBy(c => c.Codigo).Select(ToRecord).ToList();

    public IReadOnlyList<Produto> ListarProdutos(string? categoria = null, string? busca = null, string? ordenar = null)
    {
        IEnumerable<Produto> query = Produtos;

        if (!string.IsNullOrWhiteSpace(categoria) && !categoria.Equals("Todas", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.Categoria.Equals(categoria.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            query = query.Where(p => p.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase));
        }

        query = ordenar?.ToLowerInvariant() switch
        {
            "preco_asc" => query.OrderBy(p => p.Preco),
            "preco_desc" => query.OrderByDescending(p => p.Preco),
            "nome" => query.OrderBy(p => p.Nome),
            _ => query.OrderBy(p => p.Id)
        };

        return query.ToList();
    }

    public IReadOnlyList<CategoriaResumo> ListarCategorias() =>
        Produtos
            .GroupBy(p => p.Categoria)
            .Select(g => new CategoriaResumo(g.Key, g.Count()))
            .OrderBy(c => c.Nome)
            .ToList();

    public Produto? BuscarProduto(int id)
    {
        if (_db is null) return _produtos.FirstOrDefault(p => p.Id == id);
        var entity = _db.Produtos.AsNoTracking().FirstOrDefault(p => p.Id == id);
        return entity is null ? null : ToRecord(entity);
    }

    public Produto AdicionarProduto(
        string nome,
        decimal preco,
        int estoque,
        string? imagemUrl = null,
        string? categoria = null)
    {
        var cat = string.IsNullOrWhiteSpace(categoria) ? "Geral" : categoria.Trim();
        if (_db is null)
        {
            var produto = new Produto(_proximoIdProduto++, nome, preco, estoque, imagemUrl, cat);
            _produtos.Add(produto);
            return produto;
        }

        var entity = new ProdutoEntity
        {
            Nome = nome,
            Preco = preco,
            Estoque = estoque,
            ImagemUrl = imagemUrl,
            Categoria = cat
        };
        _db.Produtos.Add(entity);
        _db.SaveChanges();
        return ToRecord(entity);
    }

    public Produto? AtualizarProduto(
        int id,
        string nome,
        decimal preco,
        int estoque,
        string? imagemUrl = null,
        string? categoria = null)
    {
        if (_db is null)
        {
            var produto = _produtos.FirstOrDefault(p => p.Id == id);
            if (produto is null) return null;

            var cat = string.IsNullOrWhiteSpace(categoria) ? produto.Categoria : categoria.Trim();
            var atualizado = produto with
            {
                Nome = nome,
                Preco = preco,
                Estoque = estoque,
                ImagemUrl = imagemUrl ?? produto.ImagemUrl,
                Categoria = cat
            };
            _produtos[_produtos.IndexOf(produto)] = atualizado;
            return atualizado;
        }

        var entity = _db.Produtos.FirstOrDefault(p => p.Id == id);
        if (entity is null) return null;

        entity.Nome = nome;
        entity.Preco = preco;
        entity.Estoque = estoque;
        if (imagemUrl is not null) entity.ImagemUrl = imagemUrl;
        if (!string.IsNullOrWhiteSpace(categoria)) entity.Categoria = categoria.Trim();
        _db.SaveChanges();
        return ToRecord(entity);
    }

    public (Produto? produto, string? erro) AjustarEstoque(int id, int quantidade)
    {
        if (_db is null)
        {
            var produto = _produtos.FirstOrDefault(p => p.Id == id);
            if (produto is null) return (null, "Produto não encontrado.");

            var novoEstoque = produto.Estoque + quantidade;
            if (novoEstoque < 0)
                return (null, "Estoque resultante não pode ser negativo.");

            var atualizado = produto with { Estoque = novoEstoque };
            _produtos[_produtos.IndexOf(produto)] = atualizado;
            return (atualizado, null);
        }

        var entity = _db.Produtos.FirstOrDefault(p => p.Id == id);
        if (entity is null) return (null, "Produto não encontrado.");

        var novo = entity.Estoque + quantidade;
        if (novo < 0)
            return (null, "Estoque resultante não pode ser negativo.");

        entity.Estoque = novo;
        _db.SaveChanges();
        return (ToRecord(entity), null);
    }

    public bool RemoverProduto(int id)
    {
        if (_db is null)
        {
            var produto = _produtos.FirstOrDefault(p => p.Id == id);
            if (produto is null) return false;
            _produtos.Remove(produto);
            return true;
        }

        var entity = _db.Produtos.FirstOrDefault(p => p.Id == id);
        if (entity is null) return false;
        _db.Produtos.Remove(entity);
        _db.SaveChanges();
        return true;
    }

    public (Cupom? cupom, string? erro) CadastrarCupom(CupomDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
            return (null, "Código do cupom é obrigatório.");

        var codigo = dto.Codigo.Trim().ToUpperInvariant();
        if (dto.PercentualDesconto <= 0 || dto.PercentualDesconto > 100)
            return (null, "O desconto deve estar entre 1% e 100%.");

        if (dto.LimiteUsos is <= 0)
            return (null, "O limite de usos deve ser maior que zero.");

        if (_db is null)
        {
            if (_cupons.Any(c => c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase)))
                return (null, $"Já existe um cupom com o código '{codigo}'.");

            var cupom = new Cupom(
                codigo,
                dto.PercentualDesconto,
                Ativo: true,
                ExpiraEm: dto.ExpiraEm?.ToUniversalTime(),
                LimiteUsos: dto.LimiteUsos,
                UsosAtuais: 0);
            _cupons.Add(cupom);
            return (cupom, null);
        }

        if (_db.Cupons.Any(c => c.Codigo == codigo))
            return (null, $"Já existe um cupom com o código '{codigo}'.");

        var entity = new CupomEntity
        {
            Codigo = codigo,
            PercentualDesconto = dto.PercentualDesconto,
            Ativo = true,
            ExpiraEm = dto.ExpiraEm?.ToUniversalTime(),
            LimiteUsos = dto.LimiteUsos,
            UsosAtuais = 0
        };
        _db.Cupons.Add(entity);
        _db.SaveChanges();
        return (ToRecord(entity), null);
    }

    public bool RemoverCupom(string codigo)
    {
        var norm = codigo.Trim().ToUpperInvariant();
        if (_db is null)
        {
            var cupom = _cupons.FirstOrDefault(c => c.Codigo.Equals(norm, StringComparison.OrdinalIgnoreCase));
            if (cupom is null) return false;
            _cupons.Remove(cupom);
            return true;
        }

        var entity = _db.Cupons.FirstOrDefault(c => c.Codigo == norm);
        if (entity is null) return false;
        _db.Cupons.Remove(entity);
        _db.SaveChanges();
        return true;
    }

    public (Cupom? cupom, string? erro) ValidarCupomDetalhado(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return (null, "Informe um código de cupom.");

        var norm = codigo.Trim().ToUpperInvariant();
        var cupom = Cupons.FirstOrDefault(c => c.Codigo.Equals(norm, StringComparison.OrdinalIgnoreCase));

        if (cupom is null || !cupom.Ativo)
            return (null, $"Cupom '{codigo.Trim()}' inválido ou inativo.");

        if (cupom.ExpiraEm is not null && cupom.ExpiraEm <= DateTime.UtcNow)
            return (null, $"Cupom '{cupom.Codigo}' expirado.");

        if (cupom.LimiteUsos is not null && cupom.UsosAtuais >= cupom.LimiteUsos)
            return (null, $"Cupom '{cupom.Codigo}' atingiu o limite de {cupom.LimiteUsos} usos.");

        return (cupom, null);
    }

    public Cupom? ValidarCupom(string? codigo) => ValidarCupomDetalhado(codigo).cupom;

    public Pedido? BuscarPedido(int id)
    {
        if (_db is null) return _pedidos.FirstOrDefault(p => p.Id == id);
        var entity = _db.Pedidos.AsNoTracking().Include(p => p.Itens).FirstOrDefault(p => p.Id == id);
        return entity is null ? null : ToRecord(entity);
    }

    /// <summary>
    /// Valida existência, estoque e cupom opcional; em sucesso cria o pedido, decrementa estoque e contabiliza uso do cupom.
    /// </summary>
    public (Pedido? pedido, string? erro) CriarPedido(IReadOnlyList<ItemPedidoDto> itens, string? codigoCupom = null)
    {
        if (itens.Count == 0)
            return (null, "O pedido deve conter ao menos um item.");

        Cupom? cupom = null;
        if (!string.IsNullOrWhiteSpace(codigoCupom))
        {
            var (cupomValido, erroCupom) = ValidarCupomDetalhado(codigoCupom);
            if (cupomValido is null)
                return (null, erroCupom);
            cupom = cupomValido;
        }

        var itensPedido = new List<ItemPedido>();
        foreach (var item in itens)
        {
            var produto = BuscarProduto(item.ProdutoId);
            if (produto is null)
                return (null, $"Produto {item.ProdutoId} não encontrado.");
            if (item.Quantidade <= 0)
                return (null, $"Quantidade inválida para o produto {produto.Nome}.");
            if (item.Quantidade > produto.Estoque)
                return (null, $"Estoque insuficiente para o produto {produto.Nome}.");

            itensPedido.Add(new ItemPedido(produto.Id, produto.Nome, item.Quantidade, produto.Preco));
        }

        var subtotal = itensPedido.Sum(i => i.Quantidade * i.PrecoUnitario);
        var desconto = cupom is not null ? Math.Round(subtotal * (cupom.PercentualDesconto / 100m), 2) : 0m;
        var total = subtotal - desconto;

        if (_db is null)
        {
            foreach (var item in itensPedido)
            {
                var produto = _produtos.First(p => p.Id == item.ProdutoId);
                _produtos[_produtos.IndexOf(produto)] = produto with { Estoque = produto.Estoque - item.Quantidade };
            }

            if (cupom is not null)
            {
                var idx = _cupons.FindIndex(c => c.Codigo == cupom.Codigo);
                _cupons[idx] = _cupons[idx] with { UsosAtuais = _cupons[idx].UsosAtuais + 1 };
            }

            var pedido = new Pedido(
                _proximoIdPedido++,
                DateTime.UtcNow,
                itensPedido,
                total,
                "Pendente",
                cupom?.Codigo,
                desconto);
            _pedidos.Add(pedido);
            return (pedido, null);
        }

        foreach (var item in itensPedido)
        {
            var pEntity = _db.Produtos.First(p => p.Id == item.ProdutoId);
            pEntity.Estoque -= item.Quantidade;
        }

        if (cupom is not null)
        {
            var cEntity = _db.Cupons.First(c => c.Codigo == cupom.Codigo);
            cEntity.UsosAtuais += 1;
        }

        var pedidoEntity = new PedidoEntity
        {
            CriadoEm = DateTime.UtcNow,
            Total = total,
            Status = "Pendente",
            CupomAplicado = cupom?.Codigo,
            Desconto = desconto,
            Itens = itensPedido.Select(i => new ItemPedidoEntity
            {
                ProdutoId = i.ProdutoId,
                NomeProduto = i.NomeProduto,
                Quantidade = i.Quantidade,
                PrecoUnitario = i.PrecoUnitario
            }).ToList()
        };
        _db.Pedidos.Add(pedidoEntity);
        _db.SaveChanges();
        return (ToRecord(pedidoEntity), null);
    }

    public (Pedido? pedido, string? erro) AtualizarStatusPedido(int id, string novoStatus)
    {
        var statusNormalizado = StatusValidos.FirstOrDefault(s =>
            s.Equals(novoStatus?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (statusNormalizado is null)
            return (null, $"Status inválido. Use: {string.Join(", ", StatusValidos)}.");

        if (statusNormalizado == "Cancelado")
            return CancelarPedido(id);

        if (_db is null)
        {
            var pedido = _pedidos.FirstOrDefault(p => p.Id == id);
            if (pedido is null) return (null, "Pedido não encontrado.");
            if (pedido.Status == "Cancelado")
                return (null, "Não é possível alterar o status de um pedido cancelado.");

            var atualizado = pedido with { Status = statusNormalizado };
            _pedidos[_pedidos.IndexOf(pedido)] = atualizado;
            return (atualizado, null);
        }

        var entity = _db.Pedidos.Include(p => p.Itens).FirstOrDefault(p => p.Id == id);
        if (entity is null) return (null, "Pedido não encontrado.");
        if (entity.Status == "Cancelado")
            return (null, "Não é possível alterar o status de um pedido cancelado.");

        entity.Status = statusNormalizado;
        _db.SaveChanges();
        return (ToRecord(entity), null);
    }

    public (Pedido? pedido, string? erro) CancelarPedido(int id)
    {
        if (_db is null)
        {
            var pedido = _pedidos.FirstOrDefault(p => p.Id == id);
            if (pedido is null) return (null, "Pedido não encontrado.");
            if (pedido.Status == "Cancelado")
                return (null, "Este pedido já está cancelado.");

            foreach (var item in pedido.Itens)
            {
                var produto = _produtos.FirstOrDefault(p => p.Id == item.ProdutoId);
                if (produto is not null)
                    _produtos[_produtos.IndexOf(produto)] = produto with { Estoque = produto.Estoque + item.Quantidade };
            }

            var cancelado = pedido with { Status = "Cancelado" };
            _pedidos[_pedidos.IndexOf(pedido)] = cancelado;
            return (cancelado, null);
        }

        var entity = _db.Pedidos.Include(p => p.Itens).FirstOrDefault(p => p.Id == id);
        if (entity is null) return (null, "Pedido não encontrado.");
        if (entity.Status == "Cancelado")
            return (null, "Este pedido já está cancelado.");

        foreach (var item in entity.Itens)
        {
            var pEntity = _db.Produtos.FirstOrDefault(p => p.Id == item.ProdutoId);
            if (pEntity is not null)
                pEntity.Estoque += item.Quantidade;
        }

        entity.Status = "Cancelado";
        _db.SaveChanges();
        return (ToRecord(entity), null);
    }

    public ResumoLoja ObterResumo()
    {
        var listaProdutos = Produtos;
        var listaPedidos = Pedidos;

        var porStatus = StatusValidos.ToDictionary(
            s => s,
            s => listaPedidos.Count(p => p.Status == s));

        var faturamento = listaPedidos
            .Where(p => p.Status != "Cancelado")
            .Sum(p => p.Total);

        return new ResumoLoja(
            TotalProdutos: listaProdutos.Count,
            TotalUnidadesEstoque: listaProdutos.Sum(p => p.Estoque),
            ProdutosEstoqueBaixo: listaProdutos.Count(p => p.Estoque <= 5),
            TotalPedidos: listaPedidos.Count,
            FaturamentoTotal: faturamento,
            PedidosPorStatus: porStatus);
    }
}
