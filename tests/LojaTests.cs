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

    [Fact]
    public void CriarPedido_ComCupomValidoAplicaDesconto()
    {
        var loja = LojaComProduto(preco: 100m, estoque: 5);

        var (pedido, erro) = loja.CriarPedido([new ItemPedidoDto(1, 2)], "BEMVINDO10");

        Assert.Null(erro);
        Assert.NotNull(pedido);
        Assert.Equal("BEMVINDO10", pedido!.CupomAplicado);
        Assert.Equal(20m, pedido.Desconto);
        Assert.Equal(180m, pedido.Total);
    }

    [Fact]
    public void CancelarPedido_EstornaEstoqueEAtualizaStatus()
    {
        var loja = LojaComProduto(preco: 50m, estoque: 5);
        var (pedido, _) = loja.CriarPedido([new ItemPedidoDto(1, 3)]);
        Assert.Equal(2, loja.BuscarProduto(1)!.Estoque);

        var (cancelado, erro) = loja.CancelarPedido(pedido!.Id);

        Assert.Null(erro);
        Assert.Equal("Cancelado", cancelado!.Status);
        Assert.Equal(5, loja.BuscarProduto(1)!.Estoque);
    }

    [Fact]
    public void AjustarEstoque_SomaERecusaNegativo()
    {
        var loja = LojaComProduto(estoque: 4);

        var (reposto, erroOk) = loja.AjustarEstoque(1, 6);
        Assert.Null(erroOk);
        Assert.Equal(10, reposto!.Estoque);

        var (invalido, erroNeg) = loja.AjustarEstoque(1, -20);
        Assert.Null(invalido);
        Assert.NotNull(erroNeg);
    }

    [Fact]
    public void ListarProdutos_FiltraPorCategoriaEOrdenaPorPreco()
    {
        var loja = new Loja();
        loja.AdicionarProduto("Teclado", 300m, 5, categoria: "Periféricos");
        loja.AdicionarProduto("Mouse", 150m, 5, categoria: "Periféricos");
        loja.AdicionarProduto("Headset", 500m, 5, categoria: "Áudio");

        var perifericos = loja.ListarProdutos(categoria: "Periféricos", ordenar: "preco_asc");

        Assert.Equal(2, perifericos.Count);
        Assert.Equal("Mouse", perifericos[0].Nome);
        Assert.Equal("Teclado", perifericos[1].Nome);
    }

    [Fact]
    public void CadastrarCupom_RespeitaLimiteDeUsosEExpiracao()
    {
        var loja = LojaComProduto(preco: 100m, estoque: 10);

        var (cupomLimite, errLimite) = loja.CadastrarCupom(new CupomDto("PRIMEIRO1", 15m, null, 1));
        Assert.Null(errLimite);
        Assert.NotNull(cupomLimite);

        var (pedido1, err1) = loja.CriarPedido([new ItemPedidoDto(1, 1)], "PRIMEIRO1");
        Assert.Null(err1);
        Assert.Equal(85m, pedido1!.Total);

        // Segundo uso deve falhar porque o limite era 1
        var (pedido2, err2) = loja.CriarPedido([new ItemPedidoDto(1, 1)], "PRIMEIRO1");
        Assert.Null(pedido2);
        Assert.Contains("limite", err2);

        // Cupom já expirado deve falhar na validação
        loja.CadastrarCupom(new CupomDto("EXPIRADO", 10m, DateTime.UtcNow.AddMinutes(-5), null));
        var (cupomExp, errExp) = loja.ValidarCupomDetalhado("EXPIRADO");
        Assert.Null(cupomExp);
        Assert.Contains("expirado", errExp);
    }
}
