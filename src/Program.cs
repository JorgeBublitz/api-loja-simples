using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ApiLoja;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=nexus_loja.db";
builder.Services.AddDbContext<LojaDbContext>(opt => opt.UseSqlite(connectionString));
builder.Services.AddScoped<Loja>();

var signingKeyBytes = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY") is { Length: >= 32 } envKey
    ? Encoding.UTF8.GetBytes(envKey)
    : RandomNumberGenerator.GetBytes(32);
var signingKey = new SymmetricSecurityKey(signingKeyBytes);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "NexusTech",
            ValidAudience = "NexusTechAdmin",
            IssuerSigningKey = signingKey
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Admin via POST /auth/login. Exemplo: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

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

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

// Garante criação do banco SQLite e catálogo inicial na primeira execução
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LojaDbContext>();
    db.Database.EnsureCreated();

    var lojaInicial = scope.ServiceProvider.GetRequiredService<Loja>();
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

// --- Autenticação Admin ---

app.MapPost("/auth/login", (LoginDto dto) =>
{
    var expectedUser = Environment.GetEnvironmentVariable("ADMIN_USER") ?? "admin";
    var expectedAccess = Environment.GetEnvironmentVariable("ADMIN_ACCESS") ?? "nexus2026";

    if (!string.Equals(dto.Usuario?.Trim(), expectedUser, StringComparison.Ordinal) ||
        !string.Equals(dto.Senha, expectedAccess, StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    var jwt = new JwtSecurityToken(
        issuer: "NexusTech",
        audience: "NexusTechAdmin",
        claims: [new Claim(ClaimTypes.Name, expectedUser), new Claim(ClaimTypes.Role, "Admin")],
        expires: DateTime.UtcNow.AddHours(8),
        signingCredentials: credentials);

    var token = new JwtSecurityTokenHandler().WriteToken(jwt);
    return Results.Ok(new { token, usuario = expectedUser });
}).WithTags("Autenticação");

// --- Sistema / Métricas ---

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    versao = "1.2.0",
    banco = "sqlite",
    timestamp = DateTime.UtcNow
})).WithTags("Sistema");

app.MapGet("/resumo", (Loja loja) => Results.Ok(loja.ObterResumo()))
    .RequireAuthorization()
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
}).RequireAuthorization().WithTags("Produtos");

app.MapPut("/produtos/{id:int}", (int id, ProdutoDto dto, Loja loja) =>
{
    if (string.IsNullOrWhiteSpace(dto.Nome))
        return Results.BadRequest(new { erro = "Nome é obrigatório." });
    if (dto.Preco < 0 || dto.Estoque < 0)
        return Results.BadRequest(new { erro = "Preço e estoque devem ser não negativos." });

    var imagemUrl = SanitizarImagemUrl(dto.ImagemUrl);
    var produto = loja.AtualizarProduto(id, dto.Nome.Trim(), dto.Preco, dto.Estoque, imagemUrl, dto.Categoria);
    return produto is null ? Results.NotFound() : Results.Ok(produto);
}).RequireAuthorization().WithTags("Produtos");

app.MapPatch("/produtos/{id:int}/estoque", (int id, AjusteEstoqueDto dto, Loja loja) =>
{
    var (produto, erro) = loja.AjustarEstoque(id, dto.Quantidade);
    if (produto is not null) return Results.Ok(produto);
    return erro == "Produto não encontrado."
        ? Results.NotFound(new { erro })
        : Results.BadRequest(new { erro });
}).RequireAuthorization().WithTags("Produtos");

app.MapDelete("/produtos/{id:int}", (int id, Loja loja) =>
    loja.RemoverProduto(id) ? Results.NoContent() : Results.NotFound())
    .RequireAuthorization().WithTags("Produtos");

// --- Cupons ---

app.MapGet("/cupons", (Loja loja) => Results.Ok(loja.Cupons))
    .WithTags("Cupons");

app.MapPost("/cupons", (CupomDto dto, Loja loja) =>
{
    var (cupom, erro) = loja.CadastrarCupom(dto);
    return cupom is not null
        ? Results.Created($"/cupons/{cupom.Codigo}", cupom)
        : Results.BadRequest(new { erro });
}).RequireAuthorization().WithTags("Cupons");

app.MapPost("/cupons/validar", (ValidarCupomDto dto, Loja loja) =>
{
    var (cupom, erro) = loja.ValidarCupomDetalhado(dto.Codigo);
    return cupom is not null
        ? Results.Ok(cupom)
        : Results.BadRequest(new { erro });
}).WithTags("Cupons");

app.MapDelete("/cupons/{codigo}", (string codigo, Loja loja) =>
    loja.RemoverCupom(codigo) ? Results.NoContent() : Results.NotFound())
    .RequireAuthorization().WithTags("Cupons");

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
}).RequireAuthorization().WithTags("Pedidos");

app.MapPost("/pedidos/{id:int}/cancelar", (int id, Loja loja) =>
{
    var (pedido, erro) = loja.CancelarPedido(id);
    if (pedido is not null) return Results.Ok(pedido);
    return erro == "Pedido não encontrado."
        ? Results.NotFound(new { erro })
        : Results.BadRequest(new { erro });
}).RequireAuthorization().WithTags("Pedidos");

app.Run();

public partial class Program { }
