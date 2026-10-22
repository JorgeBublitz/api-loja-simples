namespace ApiLoja;

public record Produto(int Id, string Nome, decimal Preco, int Estoque);
public record ProdutoDto(string Nome, decimal Preco, int Estoque);
public record ItemPedidoDto(int ProdutoId, int Quantidade);
public record ItemPedido(int ProdutoId, string NomeProduto, int Quantidade, decimal PrecoUnitario);
public record Pedido(int Id, DateTime CriadoEm, List<ItemPedido> Itens, decimal Total);

/// <summary>
/// Domínio da loja: catálogo de produtos e pedidos, tudo em memória.
/// </summary>
public class Loja
{
    private readonly List<Produto> _produtos = [];
    private readonly List<Pedido> _pedidos = [];
    private int _proximoIdProduto = 1;
    private int _proximoIdPedido = 1;

    public IReadOnlyList<Produto> Produtos => _produtos;
    public IReadOnlyList<Pedido> Pedidos => _pedidos;

    public Produto? BuscarProduto(int id) => _produtos.FirstOrDefault(p => p.Id == id);

    public Produto AdicionarProduto(string nome, decimal preco, int estoque)
    {
        var produto = new Produto(_proximoIdProduto++, nome, preco, estoque);
        _produtos.Add(produto);
        return produto;
    }

    public Produto? AtualizarProduto(int id, string nome, decimal preco, int estoque)
    {
        var produto = BuscarProduto(id);
        if (produto is null) return null;

        var atualizado = produto with { Nome = nome, Preco = preco, Estoque = estoque };
        _produtos[_produtos.IndexOf(produto)] = atualizado;
        return atualizado;
    }

    public bool RemoverProduto(int id)
    {
        var produto = BuscarProduto(id);
        if (produto is null) return false;

        _produtos.Remove(produto);
        return true;
    }

    /// <summary>
    /// Valida existência e estoque; em sucesso cria o pedido e decrementa estoque.
    /// </summary>
    public (Pedido? pedido, string? erro) CriarPedido(IReadOnlyList<ItemPedidoDto> itens)
    {
        if (itens.Count == 0)
            return (null, "O pedido deve conter ao menos um item.");

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

        var pedido = new Pedido(
            _proximoIdPedido++,
            DateTime.UtcNow,
            itensPedido,
            itensPedido.Sum(i => i.Quantidade * i.PrecoUnitario));
        _pedidos.Add(pedido);

        return (pedido, null);
    }
}
