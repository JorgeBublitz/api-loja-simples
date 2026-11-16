const { test, expect } = require("@playwright/test");

test.describe("Nexus Tech — Fluxos E2E (Cliente e Admin)", () => {
  test("Cliente navega, aplica cupom TECH20 e finaliza pedido", async ({ page }) => {
    await page.goto("/");

    await expect(page.locator(".brand-text")).toContainText("NEXUS");
    await expect(page.locator(".product-card")).toHaveCount(8);

    // Filtra por categoria Consoles
    await page.getByRole("button", { name: /Consoles/i }).click();
    await expect(page.locator(".product-card")).toHaveCount(2);

    // Adiciona o primeiro console ao carrinho
    await page.locator(".product-card").first().getByRole("button", { name: /Adicionar ao Carrinho/i }).click();
    await expect(page.locator(".cart-item")).toHaveCount(1);

    // Aplica cupom TECH20
    await page.locator("#cupom-input").fill("TECH20");
    await page.locator("#btn-aplicar-cupom").click();
    await expect(page.locator("#cupom-info")).toContainText("20% OFF");

    // Finaliza pedido
    await page.locator("#btn-finalizar-pedido").click();
    await expect(page.locator("#pedidos-lista .order-card")).toHaveCount(1);
    await expect(page.locator("#pedidos-lista")).toContainText("TECH20");
  });

  test("Admin faz login JWT, visualiza metricas e cria cupom com limite de usos", async ({ page }) => {
    await page.goto("/admin.html");

    await expect(page.locator("#admin-login-section")).toBeVisible();
    await page.locator("#login-usuario").fill("admin");
    await page.locator("#login-senha").fill("nexus2026");
    await page.getByRole("button", { name: /Entrar no Painel/i }).click();

    await expect(page.locator("#admin-conteudo")).toBeVisible();
    await expect(page.locator("#stat-produtos")).toHaveText("8");

    // Cadastra cupom para os primeiros 1000 usuarios
    await page.locator("#btn-novo-cupom").click();
    await page.locator("#cupom-codigo").fill("E2E1000");
    await page.locator("#cupom-desconto").fill("15");
    await page.locator("#cupom-limite").fill("1000");
    await page.getByRole("button", { name: /Criar Cupom/i }).click();

    await expect(page.locator("#cupons-lista")).toContainText("E2E1000");
    await expect(page.locator("#cupons-lista")).toContainText("0/1000 usos");
  });
});
