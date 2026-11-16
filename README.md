# Api-Loja

![CI](https://github.com/JorgeBublitz/api-loja-simples/actions/workflows/ci.yml/badge.svg)

API REST de loja (produtos e pedidos) em **.NET 8** com interface web integrada e pipeline CI/CD segura (SAST + DAST) — versão 1.1.0.

Projeto de estudo com foco em: repositório no GitHub, pipeline de CI/CD segura (GitHub Actions + Semgrep + OWASP ZAP) e fluxo Gitflow.

## Stack

- .NET 8 (Minimal API) - dados em memória, sem banco externo
- Interface Web (`src/wwwroot/`) - catálogo de produtos, carrinho e pedidos em tempo real
- xUnit - testes de domínio (sem subir servidor HTTP)
- GitHub Actions - pipeline com Build/Testes, **SAST (Semgrep)** e **DAST (OWASP ZAP)**
- Gitflow - `main`, `develop`, branches `feature/*`, `release/*` e `hotfix/*`

## Endpoints

| Método | Rota                 | Descrição                              |
|--------|----------------------|----------------------------------------|
| GET    | `/`                  | Interface web da loja                  |
| GET    | `/swagger`           | Documentação interativa OpenAPI        |
| GET    | `/produtos`          | Lista todos os produtos                |
| GET    | `/produtos/{id}`     | Busca produto por id                   |
| POST   | `/produtos`          | Cadastra produto `{ nome, preco, estoque, imagemUrl? }` |
| PUT    | `/produtos/{id}`     | Atualiza produto                       |
| DELETE | `/produtos/{id}`     | Remove produto                         |
| GET    | `/pedidos`           | Lista todos os pedidos                 |
| POST   | `/pedidos`           | Cria pedido `[{ produtoId, quantidade }]` |

Regras de negócio do pedido: produto precisa existir, quantidade positiva e estoque suficiente; o estoque é decrementado no momento da venda.

## Estrutura

```
src/               -> API (.NET 8), domínio (Loja.cs) e interface web (wwwroot/)
tests/             -> ApiLoja.Tests.csproj + LojaTests.cs (testes xUnit do domínio)
.zap/              -> regras de validação DAST do OWASP ZAP
.github/workflows/ -> pipeline CI/CD segura (Build/Testes, SAST e DAST)
```

## Como executar

```bash
dotnet run --project src/ApiLoja.csproj
```

- Interface Web: `http://localhost:5000/` (ou a porta indicada no console)
- Swagger UI: `http://localhost:5000/swagger`

## Como testar

```bash
dotnet test
```

## Pipeline (Secure CI/CD)

O workflow `.github/workflows/ci.yml` é disparado a cada push em `main`/`develop` e em pull requests para essas branches, com 3 jobs:

- **Build e Testes** — checkout → setup-dotnet 8 → `restore` → `build` → `test` (8 testes)
- **SAST (Semgrep)** — análise estática de segurança (C#, JavaScript, OWASP Top 10) com falha automática em achados e upload do relatório SARIF
- **DAST (OWASP ZAP)** — executa após `build-e-testes` e `sast`, sobe a API no runner, executa `zap-api-scan.py` (OpenAPI/Swagger) e `zap-baseline.py` (interface web) com validação via `.zap/rules.tsv` e upload dos relatórios HTML/JSON

## Fluxo Gitflow

- `main`: código estável/liberado
- `develop`: integração contínua do desenvolvimento
- `feature/*`: novas funcionalidades, mescladas via pull request no `develop`
- `release/*`: preparação de versão, mesclada no `main`
- `hotfix/*`: correção urgente em produção, mesclada no `main` e de volta no `develop`
