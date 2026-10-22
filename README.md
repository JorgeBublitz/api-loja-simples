# Api-Loja

![CI](https://github.com/JorgeBublitz/api-loja-simples/actions/workflows/ci.yml/badge.svg)

API REST simples de loja (produtos e pedidos) em **.NET 8** — versão 1.0.1.

Projeto de estudo com foco em: repositório no GitHub, pipeline de CI (GitHub Actions) e fluxo Gitflow.

## Stack

- .NET 8 (Minimal API) - dados em memória, sem banco externo
- xUnit - testes de domínio (sem subir servidor HTTP)
- GitHub Actions - pipeline de build e testes
- Gitflow - `main`, `develop`, branches `feature/*` e `release/*`

## Endpoints

| Método | Rota                 | Descrição                              |
|--------|----------------------|----------------------------------------|
| GET    | `/produtos`          | Lista todos os produtos                |
| GET    | `/produtos/{id}`     | Busca produto por id                   |
| POST   | `/produtos`          | Cadastra produto `{ nome, preco, estoque }` |
| PUT    | `/produtos/{id}`     | Atualiza produto                       |
| DELETE | `/produtos/{id}`     | Remove produto                         |
| GET    | `/pedidos`           | Lista todos os pedidos                 |
| POST   | `/pedidos`           | Cria pedido `[{ produtoId, quantidade }]` |

Regras de negócio do pedido: produto precisa existir, quantidade positiva e estoque suficiente; o estoque é decrementado no momento da venda.

## Estrutura

```
src/         -> API (ApiLoja.csproj, Program.cs: endpoints, Loja.cs: dominio e modelos)
tests/       -> ApiLoja.Tests.csproj + LojaTests.cs (testes xUnit do dominio)
.github/workflows/ -> pipeline CI
```

## Como executar

```bash
dotnet run --project src/ApiLoja.csproj
```

A API sobe com Swagger em `http://localhost:5000/swagger` (ou a porta indicada no console).

## Como testar

```bash
dotnet test
```

## Pipeline

O workflow `.github/workflows/ci.yml` roda `restore`, `build` e `test` a cada push em `main`/`develop` e em pull requests para essas branches.

## Fluxo Gitflow

- `main`: código estável/liberado
- `develop`: integração contínua do desenvolvimento
- `feature/*`: novas funcionalidades, mescladas via pull request no `develop`
- `release/*`: preparação de versão, mesclada no `main`
