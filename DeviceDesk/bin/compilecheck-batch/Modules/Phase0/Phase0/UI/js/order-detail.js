(function () {
  const { API_BASE, fetchJson, fetchBlob, byId } = PHASE0;

  function safeFilePart(s) {
    return String(s || "")
      .replace(/[\\/:*?"<>|]/g, "_")
      .trim() || "order";
  }

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

  function fmtDate(iso) {
    if (!iso) return "—";
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return "—";
    try {
      return d.toLocaleString("en-GB", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "numeric",
        minute: "2-digit",
        hour12: true
      });
    } catch {
      return d.toLocaleString();
    }
  }

  function esc(s) {
    const d = document.createElement("div");
    d.textContent = s == null ? "" : String(s);
    return d.innerHTML;
  }

  async function loadLinkedBatches(poNumber) {
    const wrap = byId("dBatchesWrap");
    const rows = byId("dBatchesRows");
    const empty = byId("dBatchesEmpty");
    const err = byId("dBatchesError");
    const link = byId("dOpenBatches");
    const poEl = byId("dLinkPo");

    if (poEl) poEl.textContent = poNumber || "—";
    if (link) link.href = `/phase0/new-batches.html?orderNumber=${encodeURIComponent(poNumber)}`;

    if (!poNumber) {
      empty?.classList.remove("d-none");
      return;
    }

    try {
      const data = await fetchJson(
        `${API_BASE}/new/batches?page=1&pageSize=50&orderNumber=${encodeURIComponent(poNumber)}`
      );
      const list = (data && data.rows) || [];
      if (list.length === 0) {
        empty?.classList.remove("d-none");
        wrap?.classList.add("d-none");
        return;
      }

      empty?.classList.add("d-none");
      wrap?.classList.remove("d-none");
      rows.innerHTML = list
        .map(
          (r) => `
          <tr>
            <td class="small text-muted">${esc(fmtDate(r.createdAt))}</td>
            <td class="small">${esc(r.sourceFileName || "Manual Entry")}</td>
            <td class="small text-end">${Number(r.items || 0).toLocaleString("en-US")}</td>
            <td class="text-end">
              <a class="btn btn-link btn-sm p-0" href="/phase0/new-batch.html?batchId=${encodeURIComponent(
                r.id
              )}">View →</a>
            </td>
          </tr>`
        )
        .join("");
    } catch (e) {
      if (err) {
        err.textContent = `Failed to load linked batches: ${e.message || "Unknown error"}`;
        err.classList.remove("d-none");
      }
    }
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

      const btnReport = byId("btnCloseOutReport");
      if (btnReport) {
        btnReport.onclick = async () => {
          const errEl = byId("detailError");
          errEl.classList.add("d-none");
          btnReport.disabled = true;
          try {
            const blob = await fetchBlob(
              `${API_BASE}/orders/${encodeURIComponent(id)}/close-out-report`
            );
            const name = `CloseOut_${safeFilePart(row.poNumber)}_${safeFilePart(row.financialYear)}.docx`;
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            a.download = name;
            a.rel = "noopener";
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(url);
          } catch (e) {
            errEl.textContent = e.message || "Report generation failed.";
            errEl.classList.remove("d-none");
          } finally {
            btnReport.disabled = false;
          }
        };
      }

      // Now also pull any stock batches that reference this order's PO number.
      await loadLinkedBatches(row.poNumber);
    } catch (e) {
      byId("detailError").textContent = e.message || "Failed to load order.";
      byId("detailError").classList.remove("d-none");
    }
  }

  init();
})();
