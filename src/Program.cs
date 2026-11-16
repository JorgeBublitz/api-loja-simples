using ApiLoja;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<Loja>();

var app = builder.Build();

// Cabeçalhos de segurança HTTP (compatíveis com DAST / OWASP ZAP)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    if (!context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' https:; style-src 'self'; script-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

// Catálogo inicial para demonstração na interface web
var lojaInicial = app.Services.GetRequiredService<Loja>();
if (lojaInicial.Produtos.Count == 0)
{
    lojaInicial.AdicionarProduto("PlayStation 5 Slim 1TB", 3799.00m, 8, "/images/ps5.jpg", "Consoles");
    lojaInicial.AdicionarProduto("GTA VI — Edição Padrão (PS5)", 349.90m, 25, "/images/gta6.jpg", "Jogos");
    lojaInicial.AdicionarProduto("Nintendo Switch OLED", 2199.00m, 6, "/images/switch.jpg", "Consoles");
    lojaInicial.AdicionarProduto("Controle DualSense PS5", 449.90m, 15, "/images/dualsense.jpg", "Periféricos");
    lojaInicial.AdicionarProduto("iPhone 16 Pro Max 256GB", 8999.00m, 5, "/images/iphone.jpg", "Smartphones");
    lojaInicial.AdicionarProduto("MacBook Pro M4 14\"", 14499.00m, 3, "/images/macbook.jpg", "Notebooks");
    lojaInicial.AdicionarProduto("Headset Studio ANC", 589.00m, 12, "/images/headset.jpg", "Áudio");
    lojaInicial.AdicionarProduto("Monitor 4K Ultrafino 27\"", 1899.00m, 4, "/images/monitor.jpg", "Monitores");
}

static string? SanitizarImagemUrl(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return null;
    var trimmed = url.Trim();
    if (trimmed.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
        Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
    {
        return trimmed;
    }
    return null;
}

// --- Sistema / Métricas ---

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    versao = "1.2.0",
    timestamp = DateTime.UtcNow
})).WithTags("Sistema");

app.MapGet("/resumo", (Loja loja) => Results.Ok(loja.ObterResumo()))
    .WithTags("Sistema");

// --- Categorias & Produtos ---

app.MapGet("/categorias", (Loja loja) => Results.Ok(loja.ListarCategorias()))
    .WithTags("Produtos");

app.MapGet("/produtos", (string? categoria, string? busca, string? ordenar, Loja loja) =>
    Results.Ok(loja.ListarProdutos(categoria, busca, ordenar)))
    .WithTags("Produtos");

app.MapGet("/produtos/{id:int}", (int id, Loja loja) =>
    loja.BuscarProduto(id) is { } produto ? Results.Ok(produto) : Results.NotFound())
    .WithTags("Produtos");

app.MapPost("/produtos", (ProdutoDto dto, Loja loja) =>
{
    if (string.IsNullOrWhiteSpace(dto.Nome))
        return Results.BadRequest(new { erro = "Nome é obrigatório." });
    if (dto.Preco < 0 || dto.Estoque < 0)
        return Results.BadRequest(new { erro = "Preço e estoque devem ser não negativos." });

    var imagemUrl = SanitizarImagemUrl(dto.ImagemUrl);
    var produto = loja.AdicionarProduto(dto.Nome.Trim(), dto.Preco, dto.Estoque, imagemUrl, dto.Categoria);
    return Results.Created($"/produtos/{produto.Id}", produto);
}).WithTags("Produtos");

app.MapPut("/produtos/{id:int}", (int id, ProdutoDto dto, Loja loja) =>
{
    if (string.IsNullOrWhiteSpace(dto.Nome))
        return Results.BadRequest(new { erro = "Nome é obrigatório." });
    if (dto.Preco < 0 || dto.Estoque < 0)
        return Results.BadRequest(new { erro = "Preço e estoque devem ser não negativos." });

    var imagemUrl = SanitizarImagemUrl(dto.ImagemUrl);
    var produto = loja.AtualizarProduto(id, dto.Nome.Trim(), dto.Preco, dto.Estoque, imagemUrl, dto.Categoria);
    return produto is null ? Results.NotFound() : Results.Ok(produto);
}).WithTags("Produtos");

app.MapPatch("/produtos/{id:int}/estoque", (int id, AjusteEstoqueDto dto, Loja loja) =>
{
    var (produto, erro) = loja.AjustarEstoque(id, dto.Quantidade);
    if (produto is not null) return Results.Ok(produto);
    return erro == "Produto não encontrado."
        ? Results.NotFound(new { erro })
        : Results.BadRequest(new { erro });
}).WithTags("Produtos");

app.MapDelete("/produtos/{id:int}", (int id, Loja loja) =>
    loja.RemoverProduto(id) ? Results.NoContent() : Results.NotFound())
    .WithTags("Produtos");

// --- Cupons ---

app.MapGet("/cupons", (Loja loja) => Results.Ok(loja.Cupons))
    .WithTags("Cupons");

app.MapPost("/cupons", (CupomDto dto, Loja loja) =>
{
    var (cupom, erro) = loja.CadastrarCupom(dto);
    return cupom is not null
        ? Results.Created($"/cupons/{cupom.Codigo}", cupom)
        : Results.BadRequest(new { erro });
}).WithTags("Cupons");

app.MapPost("/cupons/validar", (ValidarCupomDto dto, Loja loja) =>
{
    var (cupom, erro) = loja.ValidarCupomDetalhado(dto.Codigo);
    return cupom is not null
        ? Results.Ok(cupom)
        : Results.BadRequest(new { erro });
}).WithTags("Cupons");

app.MapDelete("/cupons/{codigo}", (string codigo, Loja loja) =>
    loja.RemoverCupom(codigo) ? Results.NoContent() : Results.NotFound())
    .WithTags("Cupons");

// --- Pedidos ---

app.MapGet("/pedidos", (string? status, Loja loja) =>
{
    var lista = string.IsNullOrWhiteSpace(status)
        ? loja.Pedidos
        : loja.Pedidos.Where(p => p.Status.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    return Results.Ok(lista);
}).WithTags("Pedidos");

app.MapGet("/pedidos/{id:int}", (int id, Loja loja) =>
    loja.BuscarPedido(id) is { } pedido ? Results.Ok(pedido) : Results.NotFound())
    .WithTags("Pedidos");

app.MapPost("/pedidos", (ItemPedidoDto[] itens, string? cupom, Loja loja) =>
{
    var (pedido, erro) = loja.CriarPedido(itens, cupom);
    return pedido is not null
        ? Results.Created($"/pedidos/{pedido.Id}", pedido)
        : Results.BadRequest(new { erro });
}).WithTags("Pedidos");

app.MapPatch("/pedidos/{id:int}/status", (int id, AtualizarStatusDto dto, Loja loja) =>
{
    var (pedido, erro) = loja.AtualizarStatusPedido(id, dto.Status);
    if (pedido is not null) return Results.Ok(pedido);
    return erro == "Pedido não encontrado."
        ? Results.NotFound(new { erro })
        : Results.BadRequest(new { erro });
}).WithTags("Pedidos");

app.MapPost("/pedidos/{id:int}/cancelar", (int id, Loja loja) =>
{
    var (pedido, erro) = loja.CancelarPedido(id);
    if (pedido is not null) return Results.Ok(pedido);
    return erro == "Pedido não encontrado."
        ? Results.NotFound(new { erro })
        : Results.BadRequest(new { erro });
}).WithTags("Pedidos");

app.Run();
