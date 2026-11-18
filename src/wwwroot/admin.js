(() => {
  const FALLBACK_IMAGE = "/images/ps5.jpg";
  const PRESET_IMAGES = new Set([
    "/images/ps5.jpg",
    "/images/gta6.jpg",
    "/images/switch.jpg",
    "/images/dualsense.jpg",
    "/images/iphone.jpg",
    "/images/macbook.jpg",
    "/images/teclado.jpg",
    "/images/mouse.jpg",
    "/images/headset.jpg",
    "/images/monitor.jpg"
  ]);

  const NEXT_STATUS = {
    Pendente: "Pago",
    Pago: "Enviado",
    Enviado: "Entregue"
  };

  const state = {
    produtos: [],
    pedidos: [],
    cupons: [],
    resumo: null,
    busca: "",
    filtroStatus: ""
  };

  const els = {
    loginSection: document.getElementById("admin-login-section"),
    adminConteudo: document.getElementById("admin-conteudo"),
    formLogin: document.getElementById("form-login-admin"),
    loginUsuario: document.getElementById("login-usuario"),
    loginSenha: document.getElementById("login-senha"),
    btnLogout: document.getElementById("btn-logout-admin"),
    statFaturamento: document.getElementById("stat-faturamento"),
    statProdutos: document.getElementById("stat-produtos"),
    statEstoque: document.getElementById("stat-estoque"),
    statBaixo: document.getElementById("stat-baixo"),
    buscaAdmin: document.getElementById("busca-admin"),
    produtosLista: document.getElementById("admin-produtos-lista"),
    filtroStatus: document.getElementById("filtro-status"),
    pedidosLista: document.getElementById("admin-pedidos-lista"),
    cuponsLista: document.getElementById("cupons-lista"),
    apiHealth: document.getElementById("api-health"),
    btnNovoProduto: document.getElementById("btn-novo-produto"),
    btnNovoCupom: document.getElementById("btn-novo-cupom"),
    modalCupom: document.getElementById("modal-cupom"),
    btnFecharCupom: document.getElementById("btn-fechar-cupom"),
    btnCancelarCupom: document.getElementById("btn-cancelar-cupom"),
    formCupom: document.getElementById("form-cupom"),
    cupomCodigo: document.getElementById("cupom-codigo"),
    cupomDesconto: document.getElementById("cupom-desconto"),
    cupomLimite: document.getElementById("cupom-limite"),
    cupomExpira: document.getElementById("cupom-expira"),
    modal: document.getElementById("modal-produto"),
    modalTitulo: document.getElementById("modal-titulo"),
    btnFecharModal: document.getElementById("btn-fechar-modal"),
    btnCancelarModal: document.getElementById("btn-cancelar-modal"),
    formProduto: document.getElementById("form-produto"),
    produtoId: document.getElementById("produto-id"),
    produtoNome: document.getElementById("produto-nome"),
    produtoCategoria: document.getElementById("produto-categoria"),
    produtoPreco: document.getElementById("produto-preco"),
    produtoEstoque: document.getElementById("produto-estoque"),
    produtoImagemPreset: document.getElementById("produto-imagem-preset"),
    campoImagemCustom: document.getElementById("campo-imagem-custom"),
    produtoImagemUrl: document.getElementById("produto-imagem-url"),
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

  const TOKEN_KEY = "nexus_admin_jwt";

  async function apiFetch(url, options = {}) {
    const token = sessionStorage.getItem(TOKEN_KEY);
    const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    const res = await fetch(url, {
      ...options,
      headers
    });
    if (res.status === 401) {
      sessionStorage.removeItem(TOKEN_KEY);
      atualizarVisaoAuth();
      throw new Error("Sessão expirada ou não autorizada. Faça login.");
    }
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

  function atualizarVisaoAuth() {
    const autenticado = Boolean(sessionStorage.getItem(TOKEN_KEY));
    els.loginSection.classList.toggle("hidden", autenticado);
    els.adminConteudo.classList.toggle("hidden", !autenticado);
    els.btnLogout.classList.toggle("hidden", !autenticado);
    return autenticado;
  }

  function createEmptyState(texto) {
    const div = document.createElement("div");
    div.className = "empty-state";
    div.textContent = texto;
    return div;
  }

  function renderStats() {
    if (!state.resumo) return;
    els.statFaturamento.textContent = formatBRL(state.resumo.faturamentoTotal);
    els.statProdutos.textContent = String(state.resumo.totalProdutos);
    els.statEstoque.textContent = String(state.resumo.totalUnidadesEstoque);
    els.statBaixo.textContent = String(state.resumo.produtosEstoqueBaixo);
  }

  function renderProdutos() {
    els.produtosLista.replaceChildren();
    if (state.produtos.length === 0) {
      els.produtosLista.appendChild(createEmptyState("Nenhum produto cadastrado."));
      return;
    }

    for (const p of state.produtos) {
      const row = document.createElement("div");
      row.className = "admin-product-row";

      const info = document.createElement("div");
      info.className = "admin-product-info";

      const img = document.createElement("img");
      img.className = "admin-product-thumb";
      img.src = safeImageSrc(p.imagemUrl);
      img.alt = p.nome;
      img.addEventListener("error", () => {
        img.src = FALLBACK_IMAGE;
      });

      const meta = document.createElement("div");
      meta.className = "admin-product-meta";

      const title = document.createElement("strong");
      title.textContent = p.nome;

      const sub = document.createElement("span");
      sub.textContent = `${p.categoria || "Geral"} · ${formatBRL(p.preco)} · Estoque: ${p.estoque} un.`;

      meta.append(title, sub);
      info.append(img, meta);

      const actions = document.createElement("div");
      actions.className = "admin-product-actions";

      const btnMinus = document.createElement("button");
      btnMinus.type = "button";
      btnMinus.className = "btn btn-ghost btn-sm";
      btnMinus.textContent = "-1";
      btnMinus.disabled = p.estoque === 0;
      btnMinus.addEventListener("click", () => ajustarEstoque(p.id, -1));

      const btnPlus = document.createElement("button");
      btnPlus.type = "button";
      btnPlus.className = "btn btn-ghost btn-sm";
      btnPlus.textContent = "+5";
      btnPlus.addEventListener("click", () => ajustarEstoque(p.id, 5));

      const btnEdit = document.createElement("button");
      btnEdit.type = "button";
      btnEdit.className = "btn btn-ghost btn-sm";
      btnEdit.textContent = "Editar";
      btnEdit.addEventListener("click", () => abrirModalProduto(p));

      const btnDel = document.createElement("button");
      btnDel.type = "button";
      btnDel.className = "btn btn-danger-soft btn-sm";
      btnDel.textContent = "Excluir";
      btnDel.addEventListener("click", () => excluirProduto(p));

      actions.append(btnMinus, btnPlus, btnEdit, btnDel);
      row.append(info, actions);
      els.produtosLista.appendChild(row);
    }
  }

  function renderPedidos() {
    els.pedidosLista.replaceChildren();
    if (state.pedidos.length === 0) {
      els.pedidosLista.appendChild(createEmptyState("Nenhum pedido encontrado."));
      return;
    }

    for (const pedido of [...state.pedidos].reverse()) {
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

      if (pedido.status !== "Cancelado" && pedido.status !== "Entregue") {
        const actions = document.createElement("div");
        actions.className = "order-actions";

        const proximo = NEXT_STATUS[pedido.status];
        if (proximo) {
          const btnNext = document.createElement("button");
          btnNext.type = "button";
          btnNext.className = "btn btn-ghost btn-sm";
          btnNext.textContent = `Marcar ${proximo}`;
          btnNext.addEventListener("click", () => atualizarStatusPedido(pedido.id, proximo));
          actions.appendChild(btnNext);
        }

        const btnCancel = document.createElement("button");
        btnCancel.type = "button";
        btnCancel.className = "btn btn-danger-soft btn-sm";
        btnCancel.textContent = "Cancelar";
        btnCancel.addEventListener("click", () => cancelarPedido(pedido.id));
        actions.appendChild(btnCancel);

        card.appendChild(actions);
      }

      els.pedidosLista.appendChild(card);
    }
  }

  function renderCupons() {
    els.cuponsLista.replaceChildren();
    if (state.cupons.length === 0) {
      els.cuponsLista.appendChild(createEmptyState("Nenhum cupom cadastrado."));
      return;
    }

    const agora = Date.now();
    for (const c of state.cupons) {
      const card = document.createElement("div");
      card.className = "coupon-card";

      const info = document.createElement("div");
      info.className = "admin-product-meta";

      const code = document.createElement("strong");
      code.textContent = `${c.codigo} · ${c.percentualDesconto}% OFF`;

      const usoTexto = c.limiteUsos ? `${c.usosAtuais}/${c.limiteUsos} usos` : `${c.usosAtuais} usos (ilimitado)`;
      const expiraTexto = c.expiraEm
        ? ` · Até ${new Date(c.expiraEm).toLocaleString("pt-BR")}`
        : " · Sem expiração";

      const sub = document.createElement("span");
      sub.textContent = `${usoTexto}${expiraTexto}`;

      info.append(code, sub);

      const right = document.createElement("div");
      right.className = "admin-product-actions";

      const expirado = c.expiraEm && new Date(c.expiraEm).getTime() <= agora;
      const esgotado = c.limiteUsos && c.usosAtuais >= c.limiteUsos;
      const badge = document.createElement("span");
      badge.className = `status-badge ${expirado || esgotado ? "status-cancelado" : "status-entregue"}`;
      badge.textContent = expirado ? "Expirado" : esgotado ? "Esgotado" : "Ativo";

      const btnDel = document.createElement("button");
      btnDel.type = "button";
      btnDel.className = "btn btn-danger-soft btn-sm";
      btnDel.textContent = "Excluir";
      btnDel.addEventListener("click", () => excluirCupom(c.codigo));

      right.append(badge, btnDel);
      card.append(info, right);
      els.cuponsLista.appendChild(card);
    }
  }

  function abrirModalProduto(produto = null) {
    els.formProduto.reset();
    if (produto) {
      els.modalTitulo.textContent = "Editar Produto";
      els.produtoId.value = String(produto.id);
      els.produtoNome.value = produto.nome;
      els.produtoCategoria.value = produto.categoria || "Geral";
      els.produtoPreco.value = String(produto.preco);
      els.produtoEstoque.value = String(produto.estoque);

      if (produto.imagemUrl && PRESET_IMAGES.has(produto.imagemUrl)) {
        els.produtoImagemPreset.value = produto.imagemUrl;
        els.campoImagemCustom.classList.add("hidden");
      } else if (produto.imagemUrl) {
        els.produtoImagemPreset.value = "custom";
        els.produtoImagemUrl.value = produto.imagemUrl;
        els.campoImagemCustom.classList.remove("hidden");
      } else {
        els.produtoImagemPreset.value = "/images/teclado.jpg";
        els.campoImagemCustom.classList.add("hidden");
      }
    } else {
      els.modalTitulo.textContent = "Cadastrar Produto";
      els.produtoId.value = "";
      els.produtoCategoria.value = "Periféricos";
      els.produtoImagemPreset.value = "/images/teclado.jpg";
      els.campoImagemCustom.classList.add("hidden");
    }
    els.modal.classList.remove("hidden");
    els.produtoNome.focus();
  }

  function fecharModal() {
    els.modal.classList.add("hidden");
  }

  async function carregarTudo() {
    try {
      const prodQs = state.busca.trim() ? `?busca=${encodeURIComponent(state.busca.trim())}` : "";
      const pedQs = state.filtroStatus ? `?status=${encodeURIComponent(state.filtroStatus)}` : "";

      const [resumo, produtos, pedidos, cupons, health] = await Promise.all([
        apiFetch("/resumo"),
        apiFetch(`/produtos${prodQs}`),
        apiFetch(`/pedidos${pedQs}`),
        apiFetch("/cupons"),
        apiFetch("/health")
      ]);

      state.resumo = resumo;
      state.produtos = produtos;
      state.pedidos = pedidos;
      state.cupons = cupons;

      els.apiHealth.textContent = `Online · v${health.versao}`;
      renderStats();
      renderProdutos();
      renderPedidos();
      renderCupons();
    } catch (err) {
      els.apiHealth.textContent = "Offline";
      els.apiHealth.className = "status-badge status-cancelado";
      showToast(`Erro ao carregar painel: ${err.message}`, true);
    }
  }

  async function ajustarEstoque(id, quantidade) {
    try {
      await apiFetch(`/produtos/${id}/estoque`, {
        method: "PATCH",
        body: JSON.stringify({ quantidade })
      });
      showToast("Estoque atualizado.");
      await carregarTudo();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  async function salvarProduto(e) {
    e.preventDefault();
    const id = els.produtoId.value;
    const preset = els.produtoImagemPreset.value;
    const imagemUrl = preset === "custom" ? els.produtoImagemUrl.value.trim() : preset;

    const payload = {
      nome: els.produtoNome.value.trim(),
      categoria: els.produtoCategoria.value.trim() || "Geral",
      preco: Number(els.produtoPreco.value),
      estoque: Number(els.produtoEstoque.value),
      imagemUrl: imagemUrl || null
    };

    try {
      if (id) {
        await apiFetch(`/produtos/${id}`, {
          method: "PUT",
          body: JSON.stringify(payload)
        });
        showToast("Produto atualizado.");
      } else {
        await apiFetch("/produtos", {
          method: "POST",
          body: JSON.stringify(payload)
        });
        showToast("Produto cadastrado.");
      }
      fecharModal();
      await carregarTudo();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  async function excluirProduto(produto) {
    try {
      await apiFetch(`/produtos/${produto.id}`, { method: "DELETE" });
      showToast(`Produto "${produto.nome}" removido.`);
      await carregarTudo();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  async function atualizarStatusPedido(id, status) {
    try {
      await apiFetch(`/pedidos/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status })
      });
      showToast(`Pedido #${id} atualizado para ${status}.`);
      await carregarTudo();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  async function cancelarPedido(id) {
    try {
      await apiFetch(`/pedidos/${id}/cancelar`, { method: "POST" });
      showToast(`Pedido #${id} cancelado e estoque estornado.`);
      await carregarTudo();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  async function salvarCupom(e) {
    e.preventDefault();
    const limiteVal = els.cupomLimite.value ? Number(els.cupomLimite.value) : null;
    const expiraVal = els.cupomExpira.value ? new Date(els.cupomExpira.value).toISOString() : null;

    const payload = {
      codigo: els.cupomCodigo.value.trim(),
      percentualDesconto: Number(els.cupomDesconto.value),
      limiteUsos: limiteVal,
      expiraEm: expiraVal
    };

    try {
      await apiFetch("/cupons", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      els.modalCupom.classList.add("hidden");
      els.formCupom.reset();
      showToast("Cupom criado com sucesso.");
      await carregarTudo();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  async function excluirCupom(codigo) {
    try {
      await apiFetch(`/cupons/${encodeURIComponent(codigo)}`, { method: "DELETE" });
      showToast(`Cupom ${codigo} removido.`);
      await carregarTudo();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  els.btnNovoCupom.addEventListener("click", () => {
    els.formCupom.reset();
    els.modalCupom.classList.remove("hidden");
    els.cupomCodigo.focus();
  });
  els.btnFecharCupom.addEventListener("click", () => els.modalCupom.classList.add("hidden"));
  els.btnCancelarCupom.addEventListener("click", () => els.modalCupom.classList.add("hidden"));
  els.modalCupom.addEventListener("click", (e) => {
    if (e.target === els.modalCupom) els.modalCupom.classList.add("hidden");
  });
  els.formCupom.addEventListener("submit", salvarCupom);

  els.buscaAdmin.addEventListener("input", (e) => {
    state.busca = e.target.value;
    carregarTudo();
  });

  els.filtroStatus.addEventListener("change", (e) => {
    state.filtroStatus = e.target.value;
    carregarTudo();
  });

  els.btnNovoProduto.addEventListener("click", () => abrirModalProduto());
  els.btnFecharModal.addEventListener("click", fecharModal);
  els.btnCancelarModal.addEventListener("click", fecharModal);
  els.modal.addEventListener("click", (e) => {
    if (e.target === els.modal) fecharModal();
  });

  els.produtoImagemPreset.addEventListener("change", (e) => {
    els.campoImagemCustom.classList.toggle("hidden", e.target.value !== "custom");
  });

  els.formProduto.addEventListener("submit", salvarProduto);

  els.formLogin.addEventListener("submit", async (e) => {
    e.preventDefault();
    try {
      const res = await apiFetch("/auth/login", {
        method: "POST",
        body: JSON.stringify({
          usuario: els.loginUsuario.value.trim(),
          senha: els.loginSenha.value
        })
      });
      sessionStorage.setItem(TOKEN_KEY, res.token);
      els.formLogin.reset();
      atualizarVisaoAuth();
      showToast(`Bem-vindo, ${res.usuario}!`);
      await carregarTudo();
    } catch (err) {
      showToast("Usuário ou senha inválidos.", true);
    }
  });

  els.btnLogout.addEventListener("click", () => {
    sessionStorage.removeItem(TOKEN_KEY);
    atualizarVisaoAuth();
    showToast("Você saiu do painel.");
  });

  if (atualizarVisaoAuth()) {
    carregarTudo();
  }
})();
