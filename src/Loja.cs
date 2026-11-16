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
/// Domínio da loja: catálogo de produtos, cupons, pedidos e métricas em memória.
/// </summary>
public class Loja
{
    private static readonly string[] StatusValidos = ["Pendente", "Pago", "Enviado", "Entregue", "Cancelado"];

    private readonly List<Produto> _produtos = [];
    private readonly List<Pedido> _pedidos = [];
    private readonly List<Cupom> _cupons =
    [
        new("BEMVINDO10", 10m, true, null, 1000, 0),
        new("TECH20", 20m, true, null, 500, 0)
    ];

    private int _proximoIdProduto = 1;
    private int _proximoIdPedido = 1;

    public IReadOnlyList<Produto> Produtos => _produtos;
    public IReadOnlyList<Pedido> Pedidos => _pedidos;
    public IReadOnlyList<Cupom> Cupons => _cupons;

    public IReadOnlyList<Produto> ListarProdutos(string? categoria = null, string? busca = null, string? ordenar = null)
    {
        IEnumerable<Produto> query = _produtos;

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
        _produtos
            .GroupBy(p => p.Categoria)
            .Select(g => new CategoriaResumo(g.Key, g.Count()))
            .OrderBy(c => c.Nome)
            .ToList();

    public Produto? BuscarProduto(int id) => _produtos.FirstOrDefault(p => p.Id == id);

    public Produto AdicionarProduto(
        string nome,
        decimal preco,
        int estoque,
        string? imagemUrl = null,
        string? categoria = null)
    {
        var cat = string.IsNullOrWhiteSpace(categoria) ? "Geral" : categoria.Trim();
        var produto = new Produto(_proximoIdProduto++, nome, preco, estoque, imagemUrl, cat);
        _produtos.Add(produto);
        return produto;
    }

    public Produto? AtualizarProduto(
        int id,
        string nome,
        decimal preco,
        int estoque,
        string? imagemUrl = null,
        string? categoria = null)
    {
        var produto = BuscarProduto(id);
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

    public (Produto? produto, string? erro) AjustarEstoque(int id, int quantidade)
    {
        var produto = BuscarProduto(id);
        if (produto is null) return (null, "Produto não encontrado.");

        var novoEstoque = produto.Estoque + quantidade;
        if (novoEstoque < 0)
            return (null, "Estoque resultante não pode ser negativo.");

        var atualizado = produto with { Estoque = novoEstoque };
        _produtos[_produtos.IndexOf(produto)] = atualizado;
        return (atualizado, null);
    }

    public bool RemoverProduto(int id)
    {
        var produto = BuscarProduto(id);
        if (produto is null) return false;

        _produtos.Remove(produto);
        return true;
    }

    public (Cupom? cupom, string? erro) CadastrarCupom(CupomDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
            return (null, "Código do cupom é obrigatório.");

        var codigo = dto.Codigo.Trim().ToUpperInvariant();
        if (_cupons.Any(c => c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase)))
            return (null, $"Já existe um cupom com o código '{codigo}'.");

        if (dto.PercentualDesconto <= 0 || dto.PercentualDesconto > 100)
            return (null, "O desconto deve estar entre 1% e 100%.");

        if (dto.LimiteUsos is <= 0)
            return (null, "O limite de usos deve ser maior que zero.");

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

    public bool RemoverCupom(string codigo)
    {
        var cupom = _cupons.FirstOrDefault(c => c.Codigo.Equals(codigo.Trim(), StringComparison.OrdinalIgnoreCase));
        if (cupom is null) return false;
        _cupons.Remove(cupom);
        return true;
    }

    public (Cupom? cupom, string? erro) ValidarCupomDetalhado(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return (null, "Informe um código de cupom.");

        var cupom = _cupons.FirstOrDefault(c =>
            c.Codigo.Equals(codigo.Trim(), StringComparison.OrdinalIgnoreCase));

        if (cupom is null || !cupom.Ativo)
            return (null, $"Cupom '{codigo.Trim()}' inválido ou inativo.");

        if (cupom.ExpiraEm is not null && cupom.ExpiraEm <= DateTime.UtcNow)
            return (null, $"Cupom '{cupom.Codigo}' expirado.");

        if (cupom.LimiteUsos is not null && cupom.UsosAtuais >= cupom.LimiteUsos)
            return (null, $"Cupom '{cupom.Codigo}' atingiu o limite de {cupom.LimiteUsos} usos.");

        return (cupom, null);
    }

    public Cupom? ValidarCupom(string? codigo) => ValidarCupomDetalhado(codigo).cupom;

    public Pedido? BuscarPedido(int id) => _pedidos.FirstOrDefault(p => p.Id == id);

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

        foreach (var item in itensPedido)
        {
            var produto = BuscarProduto(item.ProdutoId)!;
            _produtos[_produtos.IndexOf(produto)] = produto with { Estoque = produto.Estoque - item.Quantidade };
        }

        if (cupom is not null)
        {
            var idx = _cupons.IndexOf(cupom);
            _cupons[idx] = cupom with { UsosAtuais = cupom.UsosAtuais + 1 };
        }

        var subtotal = itensPedido.Sum(i => i.Quantidade * i.PrecoUnitario);
        var desconto = cupom is not null ? Math.Round(subtotal * (cupom.PercentualDesconto / 100m), 2) : 0m;
        var total = subtotal - desconto;

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

    public (Pedido? pedido, string? erro) AtualizarStatusPedido(int id, string novoStatus)
    {
        var pedido = BuscarPedido(id);
        if (pedido is null) return (null, "Pedido não encontrado.");

        var statusNormalizado = StatusValidos.FirstOrDefault(s =>
            s.Equals(novoStatus?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (statusNormalizado is null)
            return (null, $"Status inválido. Use: {string.Join(", ", StatusValidos)}.");

        if (pedido.Status == "Cancelado")
            return (null, "Não é possível alterar o status de um pedido cancelado.");

        if (statusNormalizado == "Cancelado")
            return CancelarPedido(id);

        var atualizado = pedido with { Status = statusNormalizado };
        _pedidos[_pedidos.IndexOf(pedido)] = atualizado;
        return (atualizado, null);
    }

    public (Pedido? pedido, string? erro) CancelarPedido(int id)
    {
        var pedido = BuscarPedido(id);
        if (pedido is null) return (null, "Pedido não encontrado.");
        if (pedido.Status == "Cancelado")
            return (null, "Este pedido já está cancelado.");

        foreach (var item in pedido.Itens)
        {
            var produto = BuscarProduto(item.ProdutoId);
            if (produto is not null)
            {
                _produtos[_produtos.IndexOf(produto)] = produto with { Estoque = produto.Estoque + item.Quantidade };
            }
        }

        var cancelado = pedido with { Status = "Cancelado" };
        _pedidos[_pedidos.IndexOf(pedido)] = cancelado;
        return (cancelado, null);
    }

    public ResumoLoja ObterResumo()
    {
        var porStatus = StatusValidos.ToDictionary(
            s => s,
            s => _pedidos.Count(p => p.Status == s));

        var faturamento = _pedidos
            .Where(p => p.Status != "Cancelado")
            .Sum(p => p.Total);

        return new ResumoLoja(
            TotalProdutos: _produtos.Count,
            TotalUnidadesEstoque: _produtos.Sum(p => p.Estoque),
            ProdutosEstoqueBaixo: _produtos.Count(p => p.Estoque <= 5),
            TotalPedidos: _pedidos.Count,
            FaturamentoTotal: faturamento,
            PedidosPorStatus: porStatus);
    }
}
