(() => {
  const API_BASE = `${location.origin}/api/phase1/receiving`;
  let allBatches = [];
  let filteredBatches = [];
  let currentPage = 1;
  const pageSize = 25;

  const els = {
    table: document.getElementById('batchesTable'),
    sourceFilter: document.getElementById('sourceFilter'),
    statusFilter: document.getElementById('statusFilter'),
    dateFrom: document.getElementById('dateFrom'),
    dateTo: document.getElementById('dateTo'),
    filterBtn: document.getElementById('filterBtn'),
    exportBtn: document.getElementById('exportBtn'),
    pagination: document.getElementById('pagination'),
    pagerSummary: document.getElementById('pagerSummary'),
    totalBatches: document.getElementById('totalBatches'),
    totalInvoices: document.getElementById('totalInvoices'),
    totalSlips: document.getElementById('totalSlips'),
    totalDevices: document.getElementById('totalDevices')
  };

  init();

  async function init() {
    showSkeletonRows();
    setupEvents();
    await loadProfile();
    await loadAllData();
    applyFilters();
    if (window.lucide && typeof window.lucide.createIcons === 'function') window.lucide.createIcons();
  }

  function setupEvents() {
    els.filterBtn?.addEventListener('click', applyFilters);
    els.sourceFilter?.addEventListener('change', applyFilters);
    els.statusFilter?.addEventListener('change', applyFilters);
    els.exportBtn?.addEventListener('click', exportFilteredCsv);
  }

  async function loadProfile() {
    try {
      const response = await fetch('/api/auth/current-user');
      if (!response.ok) return;
      const user = await response.json();
      const initials = user.fullName ? user.fullName.split(' ').map((n) => n[0]).join('').substring(0, 2).toUpperCase() : 'U';
      const i = document.getElementById('profileInitials');
      const n = document.getElementById('profileName');
      const r = document.getElementById('profileRole');
      if (i) i.textContent = initials;
      if (n) n.textContent = user.fullName || 'User';
      if (r) r.textContent = user.role || 'User';
    } catch (error) {
      console.error('Error loading profile:', error);
    }
  }

  async function loadAllData() {
    try {
      const response = await fetch(`${API_BASE}/list`);
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      allBatches = await response.json() || [];
      updateStatsBanner();
    } catch (error) {
      console.error('Error loading data:', error);
      allBatches = [];
      updateStatsBanner();
      showErrorRow('Failed to load batch data.');
    }
  }

  function updateStatsBanner() {
    const invoices = allBatches.filter((b) => {
      const t = String(b?.documentInfo?.type || '').toLowerCase();
      return t.includes('invoice') || t.includes('new stock batch');
    }).length;
    const slips = allBatches.filter((b) => {
      const t = String(b?.documentInfo?.type || '').toLowerCase();
      return t.includes('slip');
    }).length;
    const devices = allBatches.reduce((sum, b) => sum + Number(b?.actualCount || 0), 0);
    els.totalBatches.textContent = num(allBatches.length);
    els.totalInvoices.textContent = num(invoices);
    els.totalSlips.textContent = num(slips);
    els.totalDevices.textContent = num(devices);
  }

  function mapUiStatus(rawStatus) {
    const s = String(rawStatus || '').toLowerCase();
    if (s.includes('cancel')) return 'Cancelled';
    if (s.includes('fail')) return 'Failed';
    if (s.includes('grvissued') || s.includes('completed')) return 'GRVIssued';
    if (s.includes('draft')) return 'Pending';
    return 'InProgress';
  }

  function applyFilters() {
    filteredBatches = allBatches.filter((batch) => {
      if (els.sourceFilter.value && String(batch.sourceType) !== String(els.sourceFilter.value)) return false;
      const uiStatus = mapUiStatus(batch.status);
      if (els.statusFilter.value && uiStatus !== els.statusFilter.value) return false;
      const created = new Date(batch.createdAt);
      if (els.dateFrom.value) {
        const from = new Date(els.dateFrom.value);
        if (created < from) return false;
      }
      if (els.dateTo.value) {
        const to = new Date(els.dateTo.value);
        to.setHours(23, 59, 59, 999);
        if (created > to) return false;
      }
      return true;
    });
    currentPage = 1;
    renderTable();
    renderPagination();
    renderPagerSummary();
  }

  function renderTable() {
    const start = (currentPage - 1) * pageSize;
    const rows = filteredBatches.slice(start, start + pageSize);
    if (rows.length === 0) {
      els.table.innerHTML = `<tr><td colspan="9" style="padding:28px 8px;text-align:center;color:#6b7280">No batches found matching your criteria.</td></tr>`;
      return;
    }
    els.table.innerHTML = rows.map((b) => rowTemplate(b)).join('');
  }

  function rowTemplate(batch) {
    const id = String(batch.batchId || '');
    const shortId = `${id.slice(0, 8)}...${id.slice(-6)}`;
    const sourceClass = Number(batch.sourceType) === 1 ? 'src-new' : (Number(batch.sourceType) === 2 ? 'src-rnr' : 'src-emergency');
    const sourceLabel = Number(batch.sourceType) === 1 ? 'NewStock' : (Number(batch.sourceType) === 2 ? 'RnRNormal' : 'RnREmergency');
    const uiStatus = mapUiStatus(batch.status);
    const statusClass = uiStatus === 'GRVIssued' ? 'st-grv' : (uiStatus === 'InProgress' ? 'st-progress' : (uiStatus === 'Failed' ? 'st-failed' : 'st-pending'));
    const actual = Number(batch.actualCount || 0);
    const expected = Number(batch.deviceCount || 0);
    const ratioClass = actual === 0 ? 'd-none' : (actual >= expected && expected > 0 ? 'd-complete' : 'd-partial');
    const actionButtons = buildActions(batch, uiStatus);
    const docType = batch.documentInfo?.type || 'Document';
    const supplierSchool = batch.documentInfo?.supplier || batch.documentInfo?.school || batch.schoolSupplier || 'N/A';
    return `
      <tr>
        <td>
          <a href="/phase1/reconciliation.html?batchId=${encodeURIComponent(id)}" class="mono-id" title="${escapeHtml(id)}">${escapeHtml(shortId)}</a>
          <div class="id-sub">by ${escapeHtml(batch.createdBy || 'unknown')}</div>
        </td>
        <td class="c-center"><span class="badge-source ${sourceClass}">${sourceLabel}</span></td>
        <td>
          <div class="doc-line-1">${escapeHtml(docType)}:</div>
          <div class="doc-line-2">Supplier: ${escapeHtml(supplierSchool)}</div>
          <div class="doc-line-3">Uploaded: ${fmt(batch.documentInfo?.uploadedAt || batch.createdAt)}</div>
        </td>
        <td><div style="font-size:14px;font-weight:600;color:#1f2937">${escapeHtml(batch.schoolSupplier || supplierSchool)}</div></td>
        <td class="c-center"><span class="status-pill ${statusClass}">${uiStatus === 'InProgress' ? 'InProgress' : uiStatus}</span></td>
        <td class="c-center">
          <div class="devices-main ${ratioClass}">${num(actual)}/${num(expected)}</div>
          <span class="devices-sub">scanned/expected</span>
        </td>
        <td><div class="dt-main">${fmt(batch.createdAt)}</div><div class="dt-sub">${ago(batch.createdAt)}</div></td>
        <td><div class="dt-main">${fmt(batch.lastUpdated)}</div><div class="dt-sub">${ago(batch.lastUpdated)}</div></td>
        <td class="c-right">${actionButtons}</td>
      </tr>
    `;
  }

  function buildActions(batch, uiStatus) {
    const id = encodeURIComponent(batch.batchId || '');
    const view = `<button class="act-btn act-view" onclick="window.location.href='/phase1/reconciliation.html?batchId=${id}'"><i data-lucide="eye" style="width:12px;height:12px"></i>View</button>`;
    if (uiStatus === 'GRVIssued') return view;
    if (uiStatus === 'Failed') {
      return `${view}<button class="act-btn act-retry" onclick="window.location.href='/phase1/receiving-create.html?retryBatchId=${id}'"><i data-lucide="rotate-ccw" style="width:12px;height:12px"></i>Retry</button>`;
    }
    return `${view}<button class="act-btn act-continue" onclick="window.continueBatch('${id}')"><i data-lucide="play" style="width:12px;height:12px"></i>Continue</button>`;
  }

  function renderPagination() {
    const totalPages = Math.max(1, Math.ceil(filteredBatches.length / pageSize));
    const cur = currentPage;
    const btn = (label, page, active = false, disabled = false) =>
      `<button class="pager-pill ${active ? 'active' : ''}" ${disabled ? 'disabled' : ''} data-page="${page}">${label}</button>`;
    let html = btn('‹', cur - 1, false, cur <= 1);
    const start = Math.max(1, cur - 2);
    const end = Math.min(totalPages, cur + 2);
    for (let i = start; i <= end; i++) html += btn(String(i), i, i === cur, false);
    html += btn('›', cur + 1, false, cur >= totalPages);
    els.pagination.innerHTML = html;
    els.pagination.querySelectorAll('[data-page]').forEach((b) => {
      b.addEventListener('click', () => {
        const p = Number(b.getAttribute('data-page'));
        if (!p || p < 1 || p > totalPages || p === currentPage) return;
        currentPage = p;
        renderTable();
        renderPagination();
        renderPagerSummary();
        if (window.lucide && typeof window.lucide.createIcons === 'function') window.lucide.createIcons();
      });
    });
  }

  function renderPagerSummary() {
    const total = filteredBatches.length;
    const from = total ? ((currentPage - 1) * pageSize + 1) : 0;
    const to = total ? Math.min(currentPage * pageSize, total) : 0;
    els.pagerSummary.textContent = `Showing ${from}-${to} of ${total} batches`;
  }

  function exportFilteredCsv() {
    const lines = [['Batch ID', 'Source Type', 'Status', 'Document Type', 'Supplier/School', 'Devices', 'Created', 'Updated']];
    filteredBatches.forEach((b) => {
      lines.push([
        b.batchId || '',
        b.sourceTypeName || '',
        mapUiStatus(b.status),
        b.documentInfo?.type || '',
        b.schoolSupplier || '',
        `${b.actualCount || 0}/${b.deviceCount || 0}`,
        fmt(b.createdAt),
        fmt(b.lastUpdated)
      ]);
    });
    const csv = lines.map((row) => row.map(csvEsc).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const a = document.createElement('a');
    const url = URL.createObjectURL(blob);
    a.href = url;
    a.download = `phase1_all_batches_${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  function showSkeletonRows() {
    const col = 9;
    const row = () => `
      <tr>${Array.from({ length: col }).map((_, i) => `<td><div class="skel" style="width:${[70,50,78,65,58,55,72,72,65][i]}%"></div></td>`).join('')}</tr>`;
    els.table.innerHTML = Array.from({ length: 5 }).map(row).join('');
  }

  function showErrorRow(msg) {
    els.table.innerHTML = `<tr><td colspan="9" style="padding:24px 8px;text-align:center;color:#b91c1c">${escapeHtml(msg)}</td></tr>`;
  }

  window.continueBatch = function continueBatch(id) {
    const batch = allBatches.find((b) => String(b.batchId) === String(id));
    if (!batch) return;
    let url = '/phase1/receiving-create.html';
    if (String(batch.status).toLowerCase().includes('scanning')) {
      url = Number(batch.sourceType) === 3 ? `/phase1/emergency-scanning.html?batchId=${id}` : `/phase1/rnr-scanning.html?batchId=${id}`;
    } else if (String(batch.status).toLowerCase().includes('verification') || String(batch.status).toLowerCase().includes('verified') || String(batch.status).toLowerCase().includes('variance')) {
      url = Number(batch.sourceType) === 1 ? `/phase1/reconciliation.html?batchId=${id}` : `/phase1/rnr-verification.html?batchId=${id}`;
    }
    window.location.href = url;
  };

  function num(v) { return Number(v || 0).toLocaleString(); }
  function fmt(d) { const x = new Date(d); return Number.isNaN(x.getTime()) ? '-' : x.toLocaleString(); }
  function ago(d) { const x = new Date(d); const now = new Date(); const ms = now - x; const hrs = Math.floor(ms / 3600000); const days = Math.floor(hrs / 24); if (days >= 1) return days === 1 ? 'Yesterday' : `${days} days ago`; if (hrs >= 1) return `${hrs} hours ago`; return 'Just now'; }
  function csvEsc(v) { const s = String(v ?? ''); return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s; }
  function escapeHtml(v) { return String(v ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;'); }
})();
