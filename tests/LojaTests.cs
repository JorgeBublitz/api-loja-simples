using ApiLoja;
using Xunit;

namespace ApiLoja.Tests;

/// <summary>
/// Testes do domínio da loja (sem HTTP): rodam rápido e não travam.
/// </summary>
public class LojaTests
{
    private static Loja LojaComProduto(decimal preco = 80m, int estoque = 5)
    {
        var loja = new Loja();
        loja.AdicionarProduto("Mouse", preco, estoque);
        return loja;
    }

    [Fact] // Fact é o atributo do xUnit que marca um método como teste
    public void AdicionarProduto_RegistraNoCatalogoComId()
    {
        var loja = new Loja();

        var produto = loja.AdicionarProduto("Teclado", 150m, 10);

        Assert.Equal(1, produto.Id);
        Assert.Single(loja.Produtos);
        Assert.Equal("Teclado", loja.BuscarProduto(1)!.Nome);
    }

    [Fact]
    public void BuscarProduto_IndiceInexistenteRetornaNull()
    {
        var loja = LojaComProduto();

        Assert.Null(loja.BuscarProduto(99));
    }

    [Fact]
    public void AtualizarProduto_AlteraDados()
    {
        var loja = LojaComProduto();

        var atualizado = loja.AtualizarProduto(1, "Mouse Gamer", 120m, 3);

        Assert.NotNull(atualizado);
        Assert.Equal("Mouse Gamer", atualizado!.Nome);
        Assert.Equal(120m, atualizado.Preco);
        Assert.Equal(3, atualizado.Estoque);
    }

    [Fact]
    public void AtualizarProduto_InexistenteRetornaNull()
    {
        var loja = LojaComProduto();

        Assert.Null(loja.AtualizarProduto(99, "X", 1m, 1));
    }

    [Fact]
    public void RemoverProduto_RemoveDoCatalogo()
    {
        var loja = LojaComProduto();

        Assert.True(loja.RemoverProduto(1));
        Assert.Empty(loja.Produtos);
        Assert.False(loja.RemoverProduto(1));
    }

    [Fact]
    public void CriarPedido_ValidoCalculaTotalEDiminuiEstoque()
    {
        var loja = LojaComProduto(preco: 80m, estoque: 5);

        var (pedido, erro) = loja.CriarPedido([new ItemPedidoDto(1, 2)]);

        Assert.Null(erro);
        Assert.NotNull(pedido);
        Assert.Equal(160m, pedido!.Total);
        Assert.Equal(2, pedido.Itens[0].Quantidade);
        Assert.Equal(3, loja.BuscarProduto(1)!.Estoque); // 5 - 2
    }

    [Fact]
    public void CriarPedido_EstoqueInsuficienteFalha()
    {
        var loja = LojaComProduto(estoque: 1);

        var (pedido, erro) = loja.CriarPedido([new ItemPedidoDto(1, 10)]);

        Assert.Null(pedido);
        Assert.NotNull(erro);
        Assert.Contains("Estoque insuficiente", erro);
        Assert.Equal(1, loja.BuscarProduto(1)!.Estoque); // estoque inalterado
    }

    [Fact]
    public void CriarPedido_ProdutoInexistenteFalha()
    {
        var loja = LojaComProduto();

        var (pedido, erro) = loja.CriarPedido([new ItemPedidoDto(99, 1)]);

        Assert.Null(pedido);
        Assert.NotNull(erro);
        Assert.Contains("não encontrado", erro);
    }
}
