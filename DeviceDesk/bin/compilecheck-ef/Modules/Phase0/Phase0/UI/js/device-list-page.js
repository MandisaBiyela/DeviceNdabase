(() => {
  const type = (document.body?.dataset?.deviceType || 'rnr').trim().toLowerCase();
  const apiPath = (document.body?.dataset?.apiPath || `devices/${type}`).trim();

  const rowsEl = document.getElementById('rows');
  const pagerEl = document.getElementById('pager');
  const pagerText = document.getElementById('pagerText');
  const statsEl = document.getElementById('stats');

  const qEl = document.getElementById('q');
  const fromEl = document.getElementById('from');
  const toEl = document.getElementById('to');
  const pageSizeEl = document.getElementById('pageSize');
  const searchBtn = document.getElementById('searchBtn');

  let page = 1;
  function qs(obj) {
    return Object.entries(obj)
      .filter(([_, v]) => v !== undefined && v !== null && v !== '')
      .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
      .join('&');
  }

  function fmtInt(n) {
    if (n === undefined || n === null) return '0';
    const num = Number(n);
    if (Number.isNaN(num)) return '0';
    return num.toLocaleString('en-US');
  }

  function fmtDate(iso) {
    if (!iso) return '—';
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '—';
    try {
      const s = d.toLocaleString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
        hour12: true,
      });
      return s.replace(/\b(am|pm)\b/i, (m) => m.toUpperCase());
    } catch {
      return d.toLocaleString();
    }
  }

  function escapeHtml(str) {
    return String(str ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  function mkPill(n, label, { current = false, disabled = false } = {}) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.textContent = label;
    btn.className = 'sr-page-pill';
    if (current) btn.classList.add('sr-page-pill-current');
    else btn.classList.add('sr-page-pill-other');
    if (disabled) {
      btn.disabled = true;
      btn.classList.add('sr-page-pill-disabled');
      return btn;
    }
    btn.onclick = () => {
      page = n;
      load();
    };
    return btn;
  }

  function renderEmpty() {
    const html = `
      <tr>
        <td colspan="8" class="sr-empty-cell">
          <div class="sr-empty-state">
            <i data-lucide="server" class="sr-empty-icon"></i>
            <div class="sr-empty-title">No devices found</div>
            <div class="sr-empty-subtitle">Try adjusting your search filters</div>
          </div>
        </td>
      </tr>
    `;
    rowsEl.innerHTML = html;
    try {
      if (window.lucide && typeof window.lucide.createIcons === 'function') window.lucide.createIcons();
    } catch {}
  }

  function renderSkeleton() {
    rowsEl.innerHTML = `
      <tr class="sr-skeleton-row">
        <td><div class="sr-skeleton s-w-8"></div></td>
        <td><div class="sr-skeleton s-w-28"></div></td>
        <td><div class="sr-skeleton s-w-20"></div></td>
        <td><div class="sr-skeleton s-w-16"></div></td>
        <td><div class="sr-skeleton s-w-24"></div></td>
        <td><div class="sr-skeleton s-w-10"></div></td>
        <td><div class="sr-skeleton s-w-24"></div></td>
        <td><div class="sr-skeleton s-w-24"></div></td>
      </tr>
      <tr class="sr-skeleton-row">
        <td><div class="sr-skeleton s-w-8"></div></td>
        <td><div class="sr-skeleton s-w-24"></div></td>
        <td><div class="sr-skeleton s-w-16"></div></td>
        <td><div class="sr-skeleton s-w-20"></div></td>
        <td><div class="sr-skeleton s-w-20"></div></td>
        <td><div class="sr-skeleton s-w-10"></div></td>
        <td><div class="sr-skeleton s-w-28"></div></td>
        <td><div class="sr-skeleton s-w-20"></div></td>
      </tr>
      <tr class="sr-skeleton-row">
        <td><div class="sr-skeleton s-w-8"></div></td>
        <td><div class="sr-skeleton s-w-30"></div></td>
        <td><div class="sr-skeleton s-w-18"></div></td>
        <td><div class="sr-skeleton s-w-14"></div></td>
        <td><div class="sr-skeleton s-w-22"></div></td>
        <td><div class="sr-skeleton s-w-10"></div></td>
        <td><div class="sr-skeleton s-w-24"></div></td>
        <td><div class="sr-skeleton s-w-18"></div></td>
      </tr>
    `;
  }

  function renderSummary(data) {
    const total = Number(data?.total ?? 0);
    const stats = data?.stats || {};
    // The API currently does not return qtySum; treat qty sum as total (each row is qty=1).
    const qtySum = stats?.qtySum ?? total;
    const brands = stats?.brands ?? 0;
    const models = stats?.models ?? 0;

    statsEl.textContent = `Total: ${fmtInt(total)} · Qty Sum: ${fmtInt(qtySum)} · Brands: ${fmtInt(
      brands
    )} · Models: ${fmtInt(models)}`;
  }

  async function load() {
    if (!rowsEl || !pagerEl || !statsEl) return;

    const pageSize = parseInt(pageSizeEl?.value || '25', 10);
    const q = qEl?.value ? qEl.value.trim() : '';
    const from = fromEl?.value || '';
    const to = toEl?.value || '';

    try {
      renderSkeleton();
      statsEl.textContent = 'Total: 0 · Qty Sum: 0 · Brands: 0 · Models: 0';

      const data = await PHASE0.fetchJson(
        `${PHASE0.API_BASE}/${apiPath}?${qs({ page, pageSize, q, from, to })}`
      );

      renderSummary(data);

      const rows = PHASE0.rowsOrEmpty(data);
      const pages = Math.max(1, Math.ceil((data?.total ?? 0) / pageSize));

      pagerEl.innerHTML = '';
      if (!pagerText) return;
      pagerText.textContent = `Page ${data.page || page} of ${pages}`;

      const prev = page - 1;
      const next = page + 1;
      const canPrev = prev >= 1;
      const canNext = next <= pages;

      pagerEl.appendChild(mkPill(canPrev ? prev : 1, '«', { disabled: !canPrev }));

      const start = Math.max(1, page - 2);
      const end = Math.min(pages, page + 2);
      for (let i = start; i <= end; i++) pagerEl.appendChild(mkPill(i, String(i), { current: i === page }));

      pagerEl.appendChild(mkPill(canNext ? next : pages, '»', { disabled: !canNext }));

      if (!rows.length) {
        renderEmpty();
        return;
      }

      const startIndex = ((data.page || page) - 1) * pageSize;
      rowsEl.innerHTML = rows
        .map((r, idx) => {
          const rowNum = startIndex + idx + 1;
          const serial = r.serial ?? '';
          const imei = r.imei ?? '';
          const brand = r.brand ?? '';
          const model = r.model ?? '';
          const qty = r.qty ?? 1;
          const qtyNum = Number(qty) || 1;
          const importedAt = fmtDate(r.importedAt);

          const batchId = r.batchId || '';
          const batchFile = r.batchFile ?? null;
          const batchLink = batchId && batchFile ? `/phase0/batch-items.html?id=${encodeURIComponent(batchId)}` : '';

          return `
            <tr class="sr-device-row" data-batch-id="${escapeHtml(batchId)}">
              <td class="sr-col-num">${rowNum}</td>
              <td class="sr-col-mono">${serial ? escapeHtml(serial) : `<span class="sr-muted">—</span>`}</td>
              <td class="sr-col-mono">${imei ? escapeHtml(imei) : `<span class="sr-muted">—</span>`}</td>
              <td>${brand ? escapeHtml(brand) : `<span class="sr-muted">—</span>`}</td>
              <td>${model ? escapeHtml(model) : `<span class="sr-muted">—</span>`}</td>
              <td class="sr-col-qty"><span class="sr-qty-pill ${qtyNum > 1 ? 'sr-qty-pill-bulk' : 'sr-qty-pill-single'}">${escapeHtml(String(qtyNum))}</span></td>
              <td class="sr-col-date">${escapeHtml(importedAt)}</td>
              <td class="sr-batch-cell">
                ${
                  batchLink
                    ? `<a class="sr-batch-link sr-truncate" href="${escapeHtml(batchLink)}">${escapeHtml(
                        batchFile
                      )}</a>`
                    : `<span class="sr-muted sr-batch-empty">—</span>`
                }
              </td>
            </tr>
          `;
        })
        .join('');

      // Row click navigation
      rowsEl.querySelectorAll('tr.sr-device-row').forEach((tr) => {
        tr.onclick = (e) => {
          // Don't navigate when clicking an anchor (batch file link).
          if (e?.target?.closest?.('a')) return;
          const bId = tr.dataset.batchId;
          if (!bId) return;
          window.location.href = `/phase0/batch-items.html?id=${encodeURIComponent(bId)}`;
        };
      });
    } catch (e) {
      renderEmpty();
      statsEl.textContent = 'Total: 0 · Qty Sum: 0 · Brands: 0 · Models: 0';
    }
  }

  function doSearch() {
    page = 1;
    load();
  }

  // Search triggers (Enter anywhere + Search button)
  [qEl, fromEl, toEl, pageSizeEl].forEach((el) => {
    if (!el) return;
    el.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        doSearch();
      }
    });
  });

  searchBtn?.addEventListener('click', (e) => {
    e.preventDefault();
    doSearch();
  });

  // Initial load
  load();
})();

