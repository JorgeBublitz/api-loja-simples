using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApiLoja;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiLoja.Tests;

/// <summary>
/// Testes de integração HTTP cobrindo Front-end, Autenticação Admin JWT, SQLite e Back-end.
/// </summary>
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"nexustech_test_{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = $"Data Source={_dbPath}"
                });
            });
        });
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* Ignora lock em teardown */ }
        }
    }

    [Theory]
    [InlineData("/", "text/html", "NEXUS")]
    [InlineData("/admin.html", "text/html", "Acesso Administrativo")]
    [InlineData("/styles.css", "text/css", "--bg")]
    [InlineData("/client.js", "javascript", "/produtos")]
    [InlineData("/admin.js", "javascript", "/auth/login")]
    [InlineData("/images/logo.svg", "image/svg+xml", "<svg")]
    [InlineData("/images/ps5.jpg", "image/jpeg", null)]
    [InlineData("/images/gta6.jpg", "image/jpeg", null)]
    public async Task Frontend_ServePaginasEAssetsComHeadersDeSeguranca(
        string rota,
        string contentTypeEsperado,
        string? trechoEsperado)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(rota);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(contentTypeEsperado, response.Content.Headers.ContentType?.MediaType ?? string.Empty);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());

        if (rota is "/" or "/admin.html")
        {
            Assert.True(response.Headers.Contains("Content-Security-Policy"));
        }

        if (trechoEsperado is not null)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains(trechoEsperado, body);
        }
    }

    [Fact]
    public async Task Backend_HealthESwaggerRespondem200()
    {
        using var client = _factory.CreateClient();

        var health = await client.GetAsync("/health");
        var swagger = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        var healthJson = await health.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", healthJson);
        Assert.Contains("\"banco\":\"sqlite\"", healthJson);
    }

    [Fact]
    public async Task Backend_FluxoCompletoAutenticadoProdutoCupomPedidoECancelamento()
    {
        using var client = _factory.CreateClient();

        // 1. Sem JWT, rota administrativa deve retornar 401 Unauthorized
        var semAuth = await client.PostAsJsonAsync("/produtos", new ProdutoDto("Bloqueado", 10m, 1));
        Assert.Equal(HttpStatusCode.Unauthorized, semAuth.StatusCode);

        // 2. Login Admin retorna JWT válido
        var loginRes = await client.PostAsJsonAsync("/auth/login", new LoginDto("admin", "nexus2026"));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginJson = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginJson.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 3. Catálogo inicial tem produtos e suporta filtro/ordenação
        var catalogo = await client.GetFromJsonAsync<List<Produto>>("/produtos?categoria=Consoles&ordenar=preco_asc");
        Assert.NotNull(catalogo);
        Assert.True(catalogo!.Count >= 2);

        // 4. Cadastro com URL maliciosa deve sanitizar ImagemUrl para null
        var postProd = await client.PostAsJsonAsync("/produtos", new ProdutoDto(
            "Controle Teste", 199.90m, 4, "javascript:alert(1)", "Periféricos"));
        Assert.Equal(HttpStatusCode.Created, postProd.StatusCode);
        var criado = await postProd.Content.ReadFromJsonAsync<Produto>();
        Assert.NotNull(criado);
        Assert.Null(criado!.ImagemUrl);

        // 5. Reposição de estoque via PATCH
        var patchEstoque = await client.PatchAsJsonAsync($"/produtos/{criado.Id}/estoque", new AjusteEstoqueDto(6));
        Assert.Equal(HttpStatusCode.OK, patchEstoque.StatusCode);
        var aposEstoque = await patchEstoque.Content.ReadFromJsonAsync<Produto>();
        Assert.Equal(10, aposEstoque!.Estoque);

        // 6. Cadastro de cupom com limite de 1 uso
        var codigoCupom = $"TESTE{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var postCupom = await client.PostAsJsonAsync("/cupons", new CupomDto(codigoCupom, 10m, null, 1));
        Assert.Equal(HttpStatusCode.Created, postCupom.StatusCode);

        // 7. Criação de pedido aplicando o cupom
        var postPedido = await client.PostAsJsonAsync(
            $"/pedidos?cupom={codigoCupom}",
            new[] { new ItemPedidoDto(criado.Id, 2) });
        Assert.Equal(HttpStatusCode.Created, postPedido.StatusCode);
        var pedido = await postPedido.Content.ReadFromJsonAsync<Pedido>();
        Assert.NotNull(pedido);
        Assert.Equal(codigoCupom, pedido!.CupomAplicado);
        Assert.Equal(359.82m, pedido.Total);

        // 8. Cancelamento deve estornar o estoque de volta para 10
        var cancelRes = await client.PostAsync($"/pedidos/{pedido.Id}/cancelar", null);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        var produtoFinal = await client.GetFromJsonAsync<Produto>($"/produtos/{criado.Id}");
        Assert.Equal(10, produtoFinal!.Estoque);
    }
}
