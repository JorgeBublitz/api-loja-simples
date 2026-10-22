using ApiLoja;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<Loja>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// --- Produtos ---

app.MapGet("/produtos", (Loja loja) => Results.Ok(loja.Produtos));

app.MapGet("/produtos/{id:int}", (int id, Loja loja) =>
    loja.BuscarProduto(id) is { } produto ? Results.Ok(produto) : Results.NotFound());

app.MapPost("/produtos", (ProdutoDto dto, Loja loja) =>
{
    if (string.IsNullOrWhiteSpace(dto.Nome))
        return Results.BadRequest(new { erro = "Nome é obrigatório." });
    if (dto.Preco < 0 || dto.Estoque < 0)
        return Results.BadRequest(new { erro = "Preço e estoque devem ser não negativos." });

    var produto = loja.AdicionarProduto(dto.Nome.Trim(), dto.Preco, dto.Estoque);
    return Results.Created($"/produtos/{produto.Id}", produto);
});

app.MapPut("/produtos/{id:int}", (int id, ProdutoDto dto, Loja loja) =>
{
    if (string.IsNullOrWhiteSpace(dto.Nome))
        return Results.BadRequest(new { erro = "Nome é obrigatório." });

    var produto = loja.AtualizarProduto(id, dto.Nome.Trim(), dto.Preco, dto.Estoque);
    return produto is null ? Results.NotFound() : Results.Ok(produto);
});

app.MapDelete("/produtos/{id:int}", (int id, Loja loja) =>
    loja.RemoverProduto(id) ? Results.NoContent() : Results.NotFound());

// --- Pedidos ---

app.MapGet("/pedidos", (Loja loja) => Results.Ok(loja.Pedidos));

app.MapPost("/pedidos", (ItemPedidoDto[] itens, Loja loja) =>
{
    var (pedido, erro) = loja.CriarPedido(itens);
    return pedido is not null
        ? Results.Created($"/pedidos/{pedido.Id}", pedido)
        : Results.BadRequest(new { erro });
});

app.Run();
