(function () {
  const { API_BASE, fetchJson, byId } = PHASE0;

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

  function esc(s) {
    const d = document.createElement("div");
    d.textContent = s == null ? "" : String(s);
    return d.innerHTML;
  }

  function statusPill(row) {
    const fin = String(row.financialBalanceStatus || "").toLowerCase() === "balanced";
    const ob = Number(row.outstandingBalance || 0);
    if (fin && ob === 0) return `<span class="pill-balanced">✓ Balanced</span>`;
    if (ob < 0) return `<span class="pill-overpaid">✗ Overpaid</span>`;
    if (!row.isBalanced) return `<span class="pill-unbalanced">⚠ School totals</span>`;
    return `<span class="pill-unbalanced">⚠ Unbalanced</span>`;
  }

  async function init() {
    const params = new URLSearchParams(window.location.search);
    const id = params.get("id");
    if (!id) {
      byId("detailError").textContent = "Missing order id.";
      byId("detailError").classList.remove("d-none");
      return;
    }

    try {
      const row = await fetchJson(`${API_BASE}/orders/${encodeURIComponent(id)}`);
      byId("dPo").textContent = row.poNumber || "—";
      byId("dProject").textContent = row.projectName || "—";
      byId("dStatus").outerHTML = statusPill(row);
      byId("dFy").textContent = row.financialYear || "—";
      byId("dTotal").textContent = fmtCurrency(row.totalOrderValue);
      byId("dSchoolTotals").textContent = fmtCurrency(row.schoolTotals);
      byId("dInv").textContent = fmtCurrency(row.totalInvoicedToDepartment);
      byId("dPaidDept").textContent = fmtCurrency(row.totalPaidByDepartment);
      byId("dPaidSup").textContent = fmtCurrency(row.totalPaidToSuppliers);
      byId("dOut").textContent = fmtCurrency(row.outstandingBalance);
      const c = row.createdAt ? new Date(row.createdAt) : null;
      byId("dCreated").textContent = c && !Number.isNaN(c.getTime()) ? c.toISOString().replace("T", " ").substring(0, 19) + " UTC" : "—";

      const host = byId("dSchools");
      host.innerHTML = "";
      (row.schools || []).forEach((school) => {
        const card = document.createElement("section");
        card.className = "orders-card-xl";
        const items = (school.items || [])
          .map(
            (it) => `
          <tr>
            <td>${esc(it.description)}</td>
            <td class="text-end">${esc(fmtCurrency(it.unitPrice))}</td>
            <td class="text-end">${esc(String(it.qtyOrdered))}</td>
            <td class="text-end">${esc(fmtCurrency(it.totalPrice))}</td>
            <td>${esc(it.deliveryStatus)}</td>
          </tr>`
          )
          .join("");
        card.innerHTML = `
          <h2 class="h6 fw-semibold mb-3">${esc(school.schoolName)} <span class="text-muted small">(${esc(
          fmtCurrency(school.schoolSubTotal)
        )})</span></h2>
          <div class="table-responsive">
            <table class="table table-sm">
              <thead><tr><th>Description</th><th class="text-end">Unit</th><th class="text-end">Qty</th><th class="text-end">Total</th><th>Delivery</th></tr></thead>
              <tbody>${items}</tbody>
            </table>
          </div>`;
        host.appendChild(card);
      });

      byId("detailRoot").classList.remove("d-none");
    } catch (e) {
      byId("detailError").textContent = e.message || "Failed to load order.";
      byId("detailError").classList.remove("d-none");
    }
  }

  init();
})();
