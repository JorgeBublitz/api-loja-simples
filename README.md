# Nexus Tech — Api-Loja

![CI](https://github.com/JorgeBublitz/api-loja-simples/actions/workflows/ci.yml/badge.svg)

Plataforma de e-commerce **Nexus Tech** (API REST em **.NET 8** + Interface Dark Edition para **Cliente** e **Admin**) com pipeline **DevSecOps** (2 nós **SAST** + 1 nó **DAST**) — versão 1.2.0.

## Stack

- **.NET 8 (Minimal API)** — catálogo, categorias, estoque, cupons com validade/limite e ciclo de pedidos em memória
- **Interface Web Dark Edition (`src/wwwroot/`)**:
  - **Loja (Cliente)** em `/` — vitrine com fotos de estúdio, filtros por categoria, busca, ordenação, carrinho com cupom e acompanhamento de pedidos
  - **Painel Admin** em `/admin.html` — dashboard de faturamento/estoque, cadastro e edição de produtos, reposição rápida, gestão de pedidos e criação de cupons com validade e limite de usos
- **xUnit** — 13 testes unitários de domínio
- **GitHub Actions (Secure CI/CD)** — `Build e Testes`, `SAST 1 (Semgrep)`, `SAST 2 (Microsoft DevSkim)` e `DAST (OWASP ZAP)`
- **Gitflow** — `main`, `develop`, branches `feature/*`, `release/*` e `hotfix/*`

## Endpoints

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/` | Loja do Cliente (Dark Edition) |
| GET | `/admin.html` | Painel Administrativo |
| GET | `/swagger` | Documentação interativa OpenAPI |
| GET | `/health` | Healthcheck da aplicação |
| GET | `/resumo` | Métricas de faturamento, estoque e pedidos |
| GET | `/categorias` | Lista categorias com contagem de produtos |
| GET | `/produtos` | Lista produtos (`?categoria=&busca=&ordenar=`) |
| GET | `/produtos/{id}` | Busca produto por ID |
| POST | `/produtos` | Cadastra produto |
| PUT | `/produtos/{id}` | Atualiza produto |
| PATCH | `/produtos/{id}/estoque` | Ajusta/repõe estoque |
| DELETE | `/produtos/{id}` | Remove produto |
| GET | `/cupons` | Lista cupons cadastrados |
| POST | `/cupons` | Cadastra cupom com validade e limite de usos |
| POST | `/cupons/validar` | Valida código de cupom |
| DELETE | `/cupons/{codigo}` | Remove cupom |
| GET | `/pedidos` | Lista pedidos (`?status=`) |
| GET | `/pedidos/{id}` | Busca pedido por ID |
| POST | `/pedidos` | Cria pedido (`?cupom=`) com baixa no estoque |
| PATCH | `/pedidos/{id}/status` | Avança status (`Pendente` → `Pago` → `Enviado` → `Entregue`) |
| POST | `/pedidos/{id}/cancelar` | Cancela pedido e estorna estoque |

## Estrutura

```
src/               -> API (.NET 8), domínio (Loja.cs) e front-end (wwwroot/)
tests/             -> ApiLoja.Tests.csproj + LojaTests.cs (13 testes xUnit)
.zap/              -> regras de validação DAST do OWASP ZAP
.github/workflows/ -> pipeline DevSecOps (Build/Testes, 2x SAST e 1x DAST)
```

## Como executar

```bash
dotnet run --project src/ApiLoja.csproj
```

- **Loja (Cliente)**: `http://localhost:5000/`
- **Painel Admin**: `http://localhost:5000/admin.html`
- **Swagger UI**: `http://localhost:5000/swagger`

## Como testar

```bash
dotnet test
```

## Pipeline DevSecOps ([ci.yml](.github/workflows/ci.yml))

Disparada a cada `push` ou `pull_request` para `main` e `develop`, com 4 nós e validações automáticas:

1. **Build e Testes** — `restore` → `build` → `test` (13 testes xUnit)
2. **SAST 1 (Semgrep)** — análise estática (`p/csharp`, `p/javascript`, `p/security-audit`, `p/owasp-top-ten`), falha com `--error` e publica `sast-semgrep-report` (SARIF)
3. **SAST 2 (Microsoft DevSkim)** — análise estática .NET/Web via `Microsoft.CST.DevSkim.CLI`, falha em achados `Critical`/`Important` e publica `sast-devskim-report` (SARIF)
4. **DAST (OWASP ZAP)** — executa após os anteriores, sobe a API no runner, roda `zap-api-scan.py` (OpenAPI) e `zap-baseline.py` (Interface Web) validados por `.zap/rules.tsv`, e publica `owasp-zap-dast-reports` (HTML/JSON)
