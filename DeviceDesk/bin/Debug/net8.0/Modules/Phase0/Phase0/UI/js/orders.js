(function () {
  const { API_BASE, fetchJson, byId } = PHASE0;
  const schoolsContainer = byId("schoolsContainer");
  const ordersTableBody = byId("ordersTableBody");
  const ordersEmptyState = byId("ordersEmptyState");
  const schoolsTotalEl = byId("schoolsTotal");
  const balanceStatusEl = byId("balanceStatus");
  const orderAlert = byId("orderAlert");
  const varianceDisplay = byId("varianceDisplay");
  const reconBalanceBadge = byId("reconBalanceBadge");
  const ordersSearch = byId("ordersSearch");

  let schoolCounter = 0;
  /** @type {any[]} */
  let ordersListCache = [];

  function fmtCurrency(value) {
    const n = Number(value || 0);
    return (
      "R " +
      n.toLocaleString("en-ZA", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      })
    );
  }

  function toNumber(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  function esc(s) {
    const d = document.createElement("div");
    d.textContent = s == null ? "" : String(s);
    return d.innerHTML;
  }

  function setAlert(type, message) {
    if (!orderAlert) return;
    if (!message) {
      orderAlert.innerHTML = "";
      return;
    }
    orderAlert.innerHTML = `<div class="alert alert-${type}">${esc(message)}</div>`;
  }

  function showToast(message) {
    const host = byId("ordersToastHost");
    if (!host) return;
    const el = document.createElement("div");
    el.className = "orders-toast";
    el.innerHTML = `<i class="bi bi-check-lg"></i><span>${esc(message)}</span>`;
    host.appendChild(el);
    setTimeout(() => {
      el.remove();
    }, 3000);
  }

  /** Only these classes are toggled on the delivery select; marker classes stay on the element. */
  const deliveryStatusVisualClasses = [
    "status-pending",
    "status-intransit",
    "status-partial",
    "status-delivered",
    "status-cancelled"
  ];

  function deliveryStatusClass(value) {
    switch (value) {
      case "InTransit":
        return "status-intransit";
      case "Partial":
        return "status-partial";
      case "Delivered":
        return "status-delivered";
      case "Cancelled":
        return "status-cancelled";
      default:
        return "status-pending";
    }
  }

  function syncDeliverySelectStyle(sel) {
    deliveryStatusVisualClasses.forEach((c) => sel.classList.remove(c));
    sel.classList.add(deliveryStatusClass(sel.value));
  }

  /** Item rows live only under `.school-items` (avoids stray `.order-item-row` matches). */
  const itemRowsSelector = ".school-items .order-item-row";

  function getDeliverySelect(row) {
    return (
      row.querySelector("select.item-delivery-status") ||
      row.querySelector("select.select-delivery") ||
      row.querySelector("select")
    );
  }

  function itemRowTemplate(schoolIdx, itemIdx) {
    return `
      <div class="order-item-row item-grid-row" data-item-row="${itemIdx}">
        <div>
          <label class="orders-label d-lg-none small">Description</label>
          <input class="orders-input item-description" type="text" placeholder="Item description" />
          <div class="d-flex flex-wrap gap-1 mt-1">
            <input class="orders-input item-brand flex-grow-1" type="text" placeholder="Brand" style="font-size:0.75rem;height:1.75rem;min-width:6rem;" />
            <input class="orders-input item-model flex-grow-1" type="text" placeholder="Model" style="font-size:0.75rem;height:1.75rem;min-width:6rem;" />
            <input class="orders-input item-device-type flex-grow-1" type="text" placeholder="Type (e.g. Laptop)" style="font-size:0.75rem;height:1.75rem;min-width:6rem;" />
          </div>
          <div class="invalid-feedback-inline d-none item-desc-err"></div>
        </div>
        <div>
          <label class="orders-label d-lg-none small">Unit Price</label>
          <div class="input-r-prefix">
            <span class="input-r-prefix__sym">R</span>
            <input class="orders-input item-unit-price" type="number" min="0" step="0.01" placeholder="0.00" />
          </div>
          <div class="invalid-feedback-inline d-none item-price-err"></div>
        </div>
        <div>
          <label class="orders-label d-lg-none small">Qty</label>
          <div class="qty-stepper">
            <button type="button" class="qty-minus" aria-label="Decrease quantity">−</button>
            <input class="orders-input item-qty" type="number" min="0" step="1" value="0" />
            <button type="button" class="qty-plus" aria-label="Increase quantity">+</button>
          </div>
        </div>
        <div>
          <label class="orders-label d-lg-none small">Total</label>
          <div class="item-total-readonly item-total-display">R 0,00</div>
          <input type="hidden" class="item-total-price" value="0" />
        </div>
        <div>
          <label class="orders-label d-lg-none small">Delivery</label>
          <select class="select-delivery status-pending item-delivery-status">
            <option value="Pending">Pending</option>
            <option value="InTransit">In Transit</option>
            <option value="Partial">Partial</option>
            <option value="Delivered">Delivered</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
        <div class="d-flex align-items-end">
          <button type="button" class="btn btn-sm btn-outline-danger remove-item-btn w-100" style="height:2.25rem;font-size:0.75rem">Remove</button>
        </div>
      </div>
    `;
  }

  function schoolTemplate(schoolIdx) {
    return `
      <div class="school-shell" data-school-idx="${schoolIdx}">
        <div class="school-shell-header">
          <span class="school-shell-title school-number-label">School #${schoolIdx + 1}</span>
          <button type="button" class="btn-remove-school remove-school-btn">Remove School</button>
        </div>
        <div class="row g-3 mb-3">
          <div class="col-md-8">
            <label class="orders-label d-none d-md-block">School Name</label>
            <input type="text" class="orders-input school-name" placeholder="School Name" />
            <div class="invalid-feedback-inline d-none school-name-err"></div>
          </div>
          <div class="col-md-4">
            <label class="orders-label d-none d-md-block">School Subtotal</label>
            <div class="input-r-prefix">
              <span class="input-r-prefix__sym">R</span>
              <input type="text" class="orders-input school-subtotal" readonly value="0.00" />
            </div>
          </div>
        </div>
        <div class="school-items-scroll">
          <div class="item-grid-head" aria-hidden="true">
            <span>Description</span>
            <span>Unit Price</span>
            <span>Qty</span>
            <span>Total</span>
            <span>Delivery Status</span>
            <span></span>
          </div>
          <div class="school-items"></div>
        </div>
        <button type="button" class="btn-add-item add-item-btn">
          <i class="bi bi-plus-lg"></i> Add Item
        </button>
      </div>
    `;
  }

  function renumberSchools() {
    const cards = schoolsContainer.querySelectorAll(".school-shell");
    cards.forEach((card, i) => {
      const label = card.querySelector(".school-number-label");
      if (label) label.textContent = "School #" + (i + 1);
    });
  }

  function addItemRow(schoolCard) {
    const tbody = schoolCard.querySelector(".school-items");
    const itemIdx = tbody.querySelectorAll(itemRowsSelector).length;
    tbody.insertAdjacentHTML("beforeend", itemRowTemplate(Number(schoolCard.dataset.schoolIdx), itemIdx));
    bindItemHandlers(schoolCard);
  }

  function recalcSchoolSubtotal(schoolCard) {
    const rows = Array.from(schoolCard.querySelectorAll(itemRowsSelector));
    let subtotal = 0;
    rows.forEach((row) => {
      subtotal += toNumber(row.querySelector(".item-total-price").value);
    });
    const inp = schoolCard.querySelector(".school-subtotal");
    inp.value = subtotal.toFixed(2);
    inp.classList.toggle("is-positive", subtotal > 0);
    recalcOrderBalance();
    recalcReconciliation();
  }

  function bindItemHandlers(schoolCard) {
    const rows = Array.from(schoolCard.querySelectorAll(itemRowsSelector));
    rows.forEach((row) => {
      const unit = row.querySelector(".item-unit-price");
      const qty = row.querySelector(".item-qty");
      const totalHidden = row.querySelector(".item-total-price");
      const totalDisplay = row.querySelector(".item-total-display");
      const removeBtn = row.querySelector(".remove-item-btn");
      const delivery = getDeliverySelect(row);
      const minus = row.querySelector(".qty-minus");
      const plus = row.querySelector(".qty-plus");

      if (!unit || !qty || !totalHidden || !totalDisplay || !removeBtn || !delivery || !minus || !plus) {
        return;
      }

      function autoTotal() {
        const v = toNumber(unit.value) * toNumber(qty.value);
        totalHidden.value = v.toFixed(2);
        totalDisplay.textContent = fmtCurrency(v);
        recalcSchoolSubtotal(schoolCard);
      }

      unit.oninput = autoTotal;
      qty.oninput = autoTotal;
      totalHidden.onchange = () => recalcSchoolSubtotal(schoolCard);
      minus.onclick = () => {
        qty.value = String(Math.max(0, toNumber(qty.value) - 1));
        autoTotal();
      };
      plus.onclick = () => {
        qty.value = String(toNumber(qty.value) + 1);
        autoTotal();
      };
      removeBtn.onclick = () => {
        row.remove();
        recalcSchoolSubtotal(schoolCard);
      };
      delivery.onchange = () => syncDeliverySelectStyle(delivery);
      syncDeliverySelectStyle(delivery);
    });
  }

  function addSchool() {
    const idx = schoolCounter++;
    schoolsContainer.insertAdjacentHTML("beforeend", schoolTemplate(idx));
    const card = schoolsContainer.querySelector(`[data-school-idx="${idx}"]`);
    card.querySelector(".add-item-btn").onclick = () => addItemRow(card);
    card.querySelector(".remove-school-btn").onclick = () => {
      card.remove();
      renumberSchools();
      recalcOrderBalance();
      recalcReconciliation();
    };
    card.querySelector(".school-name").oninput = () => {
      const err = card.querySelector(".school-name-err");
      err.classList.add("d-none");
      card.querySelector(".school-name").classList.remove("is-invalid");
    };
    addItemRow(card);
    renumberSchools();
  }

  function recalcReconciliation() {
    const invoiced = toNumber(byId("totalInvoicedToDepartment").value);
    const paidDept = toNumber(byId("totalPaidByDepartment").value);
    const variance = invoiced - paidDept;
    varianceDisplay.textContent = fmtCurrency(variance);
    varianceDisplay.className =
      variance === 0 ? "variance-zero" : variance > 0 ? "variance-positive" : "variance-negative";

    if (variance === 0) {
      reconBalanceBadge.className = "pill-balanced";
      reconBalanceBadge.textContent = "✓ Balanced";
    } else if (variance < 0) {
      reconBalanceBadge.className = "pill-overpaid";
      reconBalanceBadge.textContent = "✗ Overpaid";
    } else {
      reconBalanceBadge.className = "pill-unbalanced";
      reconBalanceBadge.textContent = "⚠ Unbalanced";
    }
  }

  function recalcOrderBalance() {
    const parentTotal = toNumber(byId("totalOrderValue").value);
    const schoolTotals = Array.from(document.querySelectorAll(".school-subtotal")).reduce(
      (acc, input) => acc + toNumber(input.value),
      0
    );
    const balanced = Number(parentTotal.toFixed(2)) === Number(schoolTotals.toFixed(2));
    schoolsTotalEl.textContent = fmtCurrency(schoolTotals);
    if (balanced) {
      balanceStatusEl.className = "pill-balanced";
      balanceStatusEl.textContent = "✓ Balanced";
    } else {
      balanceStatusEl.className = "pill-unbalanced";
      balanceStatusEl.textContent = "⚠ Unbalanced";
    }
  }

  function clearValidation() {
    document.querySelectorAll(".is-invalid").forEach((el) => el.classList.remove("is-invalid"));
    document.querySelectorAll(".invalid-feedback-inline").forEach((el) => {
      el.classList.add("d-none");
      el.textContent = "";
    });
    byId("err-schools").classList.add("d-none");
    byId("err-schools").textContent = "";
  }

  function validateForm() {
    clearValidation();
    let ok = true;
    const po = byId("poNumber");
    if (!po.value.trim()) {
      ok = false;
      po.classList.add("is-invalid");
      const e = byId("err-poNumber");
      e.textContent = "PO Number is required";
      e.classList.remove("d-none");
    }
    const proj = byId("projectName");
    if (!proj.value.trim()) {
      ok = false;
      proj.classList.add("is-invalid");
      const e = byId("err-projectName");
      e.textContent = "Project Name is required";
      e.classList.remove("d-none");
    }

    const cards = schoolsContainer
      ? Array.from(schoolsContainer.querySelectorAll(":scope > .school-shell"))
      : [];
    if (cards.length === 0) {
      ok = false;
      const e = byId("err-schools");
      e.textContent = "At least one school is required.";
      e.classList.remove("d-none");
    }

    cards.forEach((card) => {
      const nameInp = card.querySelector(".school-name");
      if (!nameInp.value.trim()) {
        ok = false;
        nameInp.classList.add("is-invalid");
        const err = card.querySelector(".school-name-err");
        err.textContent = "School name is required";
        err.classList.remove("d-none");
      }

      card.querySelectorAll(itemRowsSelector).forEach((row) => {
        const qty = toNumber(row.querySelector(".item-qty").value);
        const desc = row.querySelector(".item-description").value.trim();
        const price = toNumber(row.querySelector(".item-unit-price").value);
        const dErr = row.querySelector(".item-desc-err");
        const pErr = row.querySelector(".item-price-err");
        dErr.classList.add("d-none");
        pErr.classList.add("d-none");
        if (qty > 0) {
          if (!desc) {
            ok = false;
            row.querySelector(".item-description").classList.add("is-invalid");
            dErr.textContent = "Description required when qty > 0";
            dErr.classList.remove("d-none");
          }
          if (price <= 0) {
            ok = false;
            row.querySelector(".item-unit-price").classList.add("is-invalid");
            pErr.textContent = "Unit price required when qty > 0";
            pErr.classList.remove("d-none");
          }
        }
      });
    });

    return ok;
  }

  /** API JSON uses numeric enum values for delivery status. */
  const deliveryStatusNum = {
    Pending: 0,
    InTransit: 3,
    Partial: 4,
    Delivered: 2,
    Cancelled: 5,
    InProgress: 1
  };

  function collectPayload() {
    const shells = schoolsContainer
      ? Array.from(schoolsContainer.querySelectorAll(":scope > .school-shell"))
      : [];
    const schools = shells.map((card) => {
      const items = Array.from(card.querySelectorAll(itemRowsSelector)).map((row) => {
        const deliverySel = getDeliverySelect(row);
        const key = deliverySel ? deliverySel.value : "Pending";
        const brand = (row.querySelector(".item-brand") || {}).value || "";
        const model = (row.querySelector(".item-model") || {}).value || "";
        const deviceType = (row.querySelector(".item-device-type") || {}).value || "";
        return {
          description: row.querySelector(".item-description").value.trim(),
          brand: brand.trim() || null,
          model: model.trim() || null,
          deviceType: deviceType.trim() || null,
          unitPrice: toNumber(row.querySelector(".item-unit-price").value),
          qtyOrdered: toNumber(row.querySelector(".item-qty").value),
          totalPrice: toNumber(row.querySelector(".item-total-price").value),
          deliveryStatus: deliveryStatusNum[key] ?? 0
        };
      });

      const schoolSubTotal = items.reduce((sum, it) => sum + it.totalPrice, 0);
      return {
        schoolName: card.querySelector(".school-name").value.trim(),
        schoolSubTotal: Number(schoolSubTotal.toFixed(2)),
        items
      };
    });

    const supplierEl = byId("supplierName");
    const expectedEl = byId("expectedDeliveryDate");
    const supplierName = supplierEl ? supplierEl.value.trim() || null : null;
    const expectedDeliveryDate = expectedEl && expectedEl.value
      ? new Date(expectedEl.value + "T00:00:00Z").toISOString()
      : null;

    return {
      poNumber: byId("poNumber").value.trim(),
      projectName: byId("projectName").value.trim(),
      financialYear: byId("financialYear").value.trim(),
      totalOrderValue: toNumber(byId("totalOrderValue").value),
      totalInvoicedToDepartment: toNumber(byId("totalInvoicedToDepartment").value),
      totalPaidByDepartment: toNumber(byId("totalPaidByDepartment").value),
      totalPaidToSuppliers: toNumber(byId("totalPaidToSuppliers").value),
      supplierName,
      expectedDeliveryDate,
      schools
    };
  }

  async function saveOrder() {
    setAlert(null, "");
    if (!validateForm()) return;

    const payload = collectPayload();
    const schoolsTotal = payload.schools.reduce((sum, s) => sum + s.schoolSubTotal, 0);
    if (Number(payload.totalOrderValue.toFixed(2)) !== Number(schoolsTotal.toFixed(2))) {
      setAlert(
        "warning",
        "Order is not balanced. Total Order Value must equal the sum of school subtotals."
      );
      return;
    }

    try {
      const result = await fetchJson(`${API_BASE}/orders`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      const batchHint = result && result.newStockBatchId
        ? " — Phase 1 receiving batch created (PendingScan)"
        : "";
      showToast("Order saved successfully" + batchHint);
      if (result && result.newStockBatchId) {
        setAlert(
          "info",
          `Order saved. A NewStockBatch was created and is ready for Phase 1 receiving. ` +
          `Open New Stock Receiving to scan devices into this batch.`
        );
      }
      await refreshOrders();
    } catch (error) {
      setAlert("danger", `Failed to save order: ${error.message || "Unknown error"}`);
    }
  }

  async function downloadExport(kind) {
    const path = kind === "excel" ? "export/excel" : "export/pdf";
    try {
      const res = await fetch(`${API_BASE}/orders/${path}`, { credentials: "include" });
      if (!res.ok) {
        const text = await res.text().catch(() => "");
        throw new Error(text || res.statusText);
      }
      const blob = await res.blob();
      const disp = res.headers.get("Content-Disposition");
      let filename = kind === "excel" ? "procurement-orders.xlsx" : "procurement-orders.pdf";
      const m = /filename\*=UTF-8''([^;]+)|filename="([^"]+)"/i.exec(disp || "");
      if (m) {
        filename = decodeURIComponent((m[1] || m[2]).trim());
      }
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = filename;
      a.rel = "noopener";
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      setAlert("danger", `Export failed: ${error.message || "Unknown error"}`);
    }
  }

  function workflowBadge(row) {
    const fin = String(row.financialBalanceStatus || "").toLowerCase() === "balanced";
    const ob = toNumber(row.outstandingBalance);
    if (fin && ob === 0) {
      return `<span class="badge-approved">✓ Balanced</span>`;
    }
    if (ob < 0) {
      return `<span class="badge-rejected">✗ Overpaid</span>`;
    }
    if (!row.isBalanced) {
      return `<span class="badge-submitted">⚠ School totals</span>`;
    }
    return `<span class="badge-submitted">⚠ Unbalanced</span>`;
  }

  function renderOrdersTable(rows) {
    const q = (ordersSearch.value || "").trim().toLowerCase();
    const filtered = !q
      ? rows
      : rows.filter(
          (r) =>
            (r.poNumber || "").toLowerCase().includes(q) ||
            (r.projectName || "").toLowerCase().includes(q) ||
            (r.financialYear || "").toLowerCase().includes(q)
        );

    ordersTableBody.innerHTML = "";
    if (filtered.length === 0) {
      ordersEmptyState.classList.remove("d-none");
      return;
    }
    ordersEmptyState.classList.add("d-none");

    filtered.forEach((row) => {
      const id = row.id || row.procurementOrderId;
      const tr = document.createElement("tr");
      tr.dataset.orderId = id;
      tr.innerHTML = `
        <td class="font-monospace small text-secondary fw-medium">${esc(row.poNumber)}</td>
        <td class="small">${esc(row.projectName)}</td>
        <td class="small text-muted">${esc(row.financialYear)}</td>
        <td class="small text-end fw-semibold">${esc(fmtCurrency(row.totalOrderValue))}</td>
        <td class="small text-end text-secondary">${esc(fmtCurrency(row.schoolTotals))}</td>
        <td>${workflowBadge(row)}</td>
      `;
      tr.addEventListener("click", () => {
        window.location.href = `/phase0/order-detail.html?id=${encodeURIComponent(id)}`;
      });
      ordersTableBody.appendChild(tr);
    });
  }

  async function refreshOrders() {
    const icon = byId("refreshOrdersIcon");
    if (icon) {
      icon.classList.add("bi-spin");
      setTimeout(() => icon.classList.remove("bi-spin"), 600);
    }
    try {
      ordersListCache = await fetchJson(`${API_BASE}/orders`);
      renderOrdersTable(ordersListCache);
    } catch (error) {
      setAlert("danger", `Failed to load orders: ${error.message || "Unknown error"}`);
    }
  }

  // If the page was opened with ?po=PO-…, pre-populate the search box so the
  // user immediately sees the order they came from.
  try {
    const params = new URLSearchParams(location.search);
    const po = (params.get("po") || "").trim();
    if (po && ordersSearch) {
      ordersSearch.value = po;
    }
  } catch (_) {
    // ignore
  }

  byId("addSchoolBtn").onclick = addSchool;
  byId("saveOrderBtn").onclick = saveOrder;
  byId("refreshOrdersBtn").onclick = refreshOrders;
  byId("exportExcelBtn").onclick = () => downloadExport("excel");
  byId("exportPdfBtn").onclick = () => downloadExport("pdf");
  byId("totalOrderValue").oninput = () => {
    recalcOrderBalance();
    recalcReconciliation();
  };

  ["totalInvoicedToDepartment", "totalPaidByDepartment", "totalPaidToSuppliers"].forEach((id) => {
    byId(id).oninput = () => {
      setAlert(null, "");
      recalcReconciliation();
    };
  });

  ordersSearch.oninput = () => renderOrdersTable(ordersListCache);

  addSchool();
  recalcOrderBalance();
  recalcReconciliation();
  refreshOrders();
})();
