(() => {
  const FALLBACK_IMAGE = "/images/teclado.jpg";

  const state = {
    produtos: [],
    categorias: [],
    pedidos: [],
    carrinho: new Map(),
    categoriaAtiva: "Todas",
    busca: "",
    ordenar: "",
    cupomAtivo: null
  };

  const els = {
    categoriasBar: document.getElementById("categorias-bar"),
    produtosGrid: document.getElementById("produtos-grid"),
    carrinhoLista: document.getElementById("carrinho-lista"),
    carrinhoTotal: document.getElementById("carrinho-total"),
    carrinhoDesconto: document.getElementById("carrinho-desconto"),
    descontoRow: document.getElementById("desconto-row"),
    cupomInput: document.getElementById("cupom-input"),
    btnAplicarCupom: document.getElementById("btn-aplicar-cupom"),
    cupomInfo: document.getElementById("cupom-info"),
    pedidosLista: document.getElementById("pedidos-lista"),
    pedidosCount: document.getElementById("pedidos-count"),
    buscaProduto: document.getElementById("busca-produto"),
    ordenarProduto: document.getElementById("ordenar-produto"),
    btnLimparCarrinho: document.getElementById("btn-limpar-carrinho"),
    btnFinalizarPedido: document.getElementById("btn-finalizar-pedido"),
    toast: document.getElementById("toast")
  };

  const formatBRL = (valor) =>
    new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(valor);

  let toastTimer = null;
  function showToast(msg, isError = false) {
    els.toast.textContent = msg;
    els.toast.classList.toggle("error", isError);
    els.toast.classList.remove("hidden");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => els.toast.classList.add("hidden"), 3200);
  }

  function safeImageSrc(url) {
    if (typeof url !== "string") return FALLBACK_IMAGE;
    const trimmed = url.trim();
    if (trimmed.startsWith("/images/") || trimmed.startsWith("https://")) {
      return trimmed;
    }
    return FALLBACK_IMAGE;
  }

  async function apiFetch(url, options = {}) {
    const res = await fetch(url, {
      headers: { "Content-Type": "application/json" },
      ...options
    });
    if (!res.ok) {
      let detail = `Erro ${res.status}`;
      try {
        const body = await res.json();
        if (body?.erro) detail = body.erro;
      } catch {
        // Ignora corpo vazio/não-JSON
      }
      throw new Error(detail);
    }
    if (res.status === 204) return null;
    return res.json();
  }

  function createEmptyState(texto) {
    const div = document.createElement("div");
    div.className = "empty-state";
    div.textContent = texto;
    return div;
  }

  function renderCategorias() {
    els.categoriasBar.replaceChildren();
    const totalProdutos = state.categorias.reduce((acc, c) => acc + c.quantidadeProdutos, 0);
    const todas = [{ nome: "Todas", quantidadeProdutos: totalProdutos }, ...state.categorias];

    for (const cat of todas) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = `pill-btn${state.categoriaAtiva === cat.nome ? " active" : ""}`;
      btn.textContent = `${cat.nome} (${cat.quantidadeProdutos})`;
      btn.addEventListener("click", () => {
        state.categoriaAtiva = cat.nome;
        carregarProdutos();
      });
      els.categoriasBar.appendChild(btn);
    }
  }

  function renderProdutos() {
    els.produtosGrid.replaceChildren();
    if (state.produtos.length === 0) {
      els.produtosGrid.appendChild(createEmptyState("Nenhum produto encontrado."));
      return;
    }

    for (const produto of state.produtos) {
      const card = document.createElement("article");
      card.className = "product-card";

      const thumb = document.createElement("div");
      thumb.className = "product-thumb";

      const img = document.createElement("img");
      img.src = safeImageSrc(produto.imagemUrl);
      img.alt = produto.nome;
      img.loading = "lazy";
      img.addEventListener("error", () => {
        img.src = FALLBACK_IMAGE;
      });

      const catTag = document.createElement("span");
      catTag.className = "category-tag";
      catTag.textContent = produto.categoria || "Geral";

      const pill = document.createElement("span");
      const stockClass = produto.estoque === 0 ? " empty" : produto.estoque <= 5 ? " low" : "";
      pill.className = `stock-pill${stockClass}`;
      pill.textContent = produto.estoque > 0 ? `${produto.estoque} un.` : "Esgotado";

      thumb.append(img, catTag, pill);

      const body = document.createElement("div");
      body.className = "product-body";

      const title = document.createElement("h3");
      title.className = "product-title";
      title.textContent = produto.nome;

      const price = document.createElement("div");
      price.className = "product-price";
      price.textContent = formatBRL(produto.preco);

      const actions = document.createElement("div");
      actions.className = "product-actions";

      const btnAdd = document.createElement("button");
      btnAdd.type = "button";
      btnAdd.className = "btn btn-primary btn-full";
      btnAdd.textContent = produto.estoque > 0 ? "+ Adicionar ao Carrinho" : "Indisponível";
      btnAdd.disabled = produto.estoque === 0;
      btnAdd.addEventListener("click", () => adicionarAoCarrinho(produto));

      actions.appendChild(btnAdd);
      body.append(title, price, actions);
      card.append(thumb, body);
      els.produtosGrid.appendChild(card);
    }
  }

  function renderCarrinho() {
    els.carrinhoLista.replaceChildren();
    let subtotal = 0;

    if (state.carrinho.size === 0) {
      els.carrinhoLista.appendChild(createEmptyState("Seu carrinho está vazio."));
      els.descontoRow.classList.add("hidden");
      els.carrinhoTotal.textContent = formatBRL(0);
      els.btnFinalizarPedido.disabled = true;
      return;
    }

    for (const [produtoId, item] of state.carrinho.entries()) {
      subtotal += item.preco * item.quantidade;

      const row = document.createElement("div");
      row.className = "cart-item";

      const info = document.createElement("div");
      info.className = "cart-item-info";

      const nome = document.createElement("span");
      nome.className = "cart-item-name";
      nome.textContent = item.nome;

      const sub = document.createElement("span");
      sub.className = "cart-item-sub";
      sub.textContent = `${formatBRL(item.preco)} × ${item.quantidade}`;

      info.append(nome, sub);

      const controls = document.createElement("div");
      controls.className = "qty-controls";

      const btnMinus = document.createElement("button");
      btnMinus.type = "button";
      btnMinus.className = "qty-btn";
      btnMinus.textContent = "-";
      btnMinus.addEventListener("click", () => alterarQuantidade(produtoId, -1));

      const qtySpan = document.createElement("span");
      qtySpan.textContent = String(item.quantidade);

      const btnPlus = document.createElement("button");
      btnPlus.type = "button";
      btnPlus.className = "qty-btn";
      btnPlus.textContent = "+";
      btnPlus.addEventListener("click", () => alterarQuantidade(produtoId, 1));

      controls.append(btnMinus, qtySpan, btnPlus);
      row.append(info, controls);
      els.carrinhoLista.appendChild(row);
    }

    let desconto = 0;
    if (state.cupomAtivo) {
      desconto = subtotal * (state.cupomAtivo.percentualDesconto / 100);
      els.descontoRow.classList.remove("hidden");
      els.carrinhoDesconto.textContent = `- ${formatBRL(desconto)}`;
    } else {
      els.descontoRow.classList.add("hidden");
    }

    els.carrinhoTotal.textContent = formatBRL(subtotal - desconto);
    els.btnFinalizarPedido.disabled = state.carrinho.size === 0;
  }

  function renderPedidos() {
    els.pedidosLista.replaceChildren();
    els.pedidosCount.textContent = String(state.pedidos.length);

    if (state.pedidos.length === 0) {
      els.pedidosLista.appendChild(createEmptyState("Você ainda não fez pedidos."));
      return;
    }

    const recentes = [...state.pedidos].reverse().slice(0, 6);
    for (const pedido of recentes) {
      const card = document.createElement("div");
      card.className = "order-card";

      const top = document.createElement("div");
      top.className = "order-top";

      const idSpan = document.createElement("span");
      idSpan.textContent = `Pedido #${pedido.id}`;

      const badge = document.createElement("span");
      badge.className = `status-badge status-${pedido.status.toLowerCase()}`;
      badge.textContent = pedido.status;

      const totalSpan = document.createElement("span");
      totalSpan.textContent = formatBRL(pedido.total);

      top.append(idSpan, badge, totalSpan);

      const items = document.createElement("div");
      items.className = "order-items";
      const resumoItens = pedido.itens.map((i) => `${i.quantidade}x ${i.nomeProduto}`).join(", ");
      items.textContent = pedido.cupomAplicado
        ? `${resumoItens} (Cupom ${pedido.cupomAplicado})`
        : resumoItens;

      card.append(top, items);
      els.pedidosLista.appendChild(card);
    }
  }

  function adicionarAoCarrinho(produto) {
    const existente = state.carrinho.get(produto.id);
    const novaQtd = (existente?.quantidade || 0) + 1;
    if (novaQtd > produto.estoque) {
      showToast("Limite de estoque atingido.", true);
      return;
    }
    state.carrinho.set(produto.id, {
      nome: produto.nome,
      preco: produto.preco,
      estoque: produto.estoque,
      quantidade: novaQtd
    });
    renderCarrinho();
  }

  function alterarQuantidade(produtoId, delta) {
    const item = state.carrinho.get(produtoId);
    if (!item) return;
    const novaQtd = item.quantidade + delta;
    if (novaQtd <= 0) {
      state.carrinho.delete(produtoId);
    } else if (novaQtd > item.estoque) {
      showToast("Estoque insuficiente.", true);
      return;
    } else {
      state.carrinho.set(produtoId, { ...item, quantidade: novaQtd });
    }
    renderCarrinho();
  }

  async function carregarProdutos() {
    const params = new URLSearchParams();
    if (state.categoriaAtiva && state.categoriaAtiva !== "Todas") {
      params.set("categoria", state.categoriaAtiva);
    }
    if (state.busca.trim()) params.set("busca", state.busca.trim());
    if (state.ordenar) params.set("ordenar", state.ordenar);

    const qs = params.toString();
    state.produtos = await apiFetch(qs ? `/produtos?${qs}` : "/produtos");
    renderCategorias();
    renderProdutos();
  }

  async function carregarDados() {
    try {
      const [categorias, pedidos] = await Promise.all([
        apiFetch("/categorias"),
        apiFetch("/pedidos")
      ]);
      state.categorias = categorias;
      state.pedidos = pedidos;
      await carregarProdutos();
      renderCarrinho();
      renderPedidos();
    } catch (err) {
      showToast(`Falha ao carregar loja: ${err.message}`, true);
    }
  }

  async function aplicarCupom() {
    const codigo = els.cupomInput.value.trim();
    if (!codigo) {
      state.cupomAtivo = null;
      els.cupomInfo.classList.add("hidden");
      renderCarrinho();
      return;
    }

    try {
      const cupom = await apiFetch("/cupons/validar", {
        method: "POST",
        body: JSON.stringify({ codigo })
      });
      state.cupomAtivo = cupom;
      els.cupomInfo.textContent = `Cupom ${cupom.codigo} aplicado (${cupom.percentualDesconto}% OFF).`;
      els.cupomInfo.classList.remove("hidden");
      renderCarrinho();
    } catch (err) {
      state.cupomAtivo = null;
      els.cupomInfo.classList.add("hidden");
      renderCarrinho();
      showToast(err.message, true);
    }
  }

  async function finalizarPedido() {
    const itens = Array.from(state.carrinho.entries()).map(([produtoId, item]) => ({
      produtoId,
      quantidade: item.quantidade
    }));
    if (itens.length === 0) return;

    const query = state.cupomAtivo
      ? `?cupom=${encodeURIComponent(state.cupomAtivo.codigo)}`
      : "";

    try {
      const pedido = await apiFetch(`/pedidos${query}`, {
        method: "POST",
        body: JSON.stringify(itens)
      });
      state.carrinho.clear();
      state.cupomAtivo = null;
      els.cupomInput.value = "";
      els.cupomInfo.classList.add("hidden");
      showToast(`Pedido #${pedido.id} realizado (${formatBRL(pedido.total)})!`);
      await carregarDados();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  els.buscaProduto.addEventListener("input", (e) => {
    state.busca = e.target.value;
    carregarProdutos();
  });

  els.ordenarProduto.addEventListener("change", (e) => {
    state.ordenar = e.target.value;
    carregarProdutos();
  });

  els.btnAplicarCupom.addEventListener("click", aplicarCupom);
  els.btnLimparCarrinho.addEventListener("click", () => {
    state.carrinho.clear();
    renderCarrinho();
  });
  els.btnFinalizarPedido.addEventListener("click", finalizarPedido);

  carregarDados();
})();
