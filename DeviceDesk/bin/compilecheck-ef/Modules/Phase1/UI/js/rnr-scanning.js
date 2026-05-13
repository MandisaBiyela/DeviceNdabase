(() => {
  const qs = new URLSearchParams(location.search);
  const batchId = qs.get('batchId');

  const input = document.getElementById('deviceScanInput');
  const scanBtn = document.getElementById('scanBtn');
  const statusEl = document.getElementById('scanStatus');

  const tbody = document.getElementById('deviceList');            // matches your HTML
  const scannedEl = document.getElementById('scannedCount');
  const totalEl = document.querySelector('[data-total]');
  const subEl = document.querySelector('[data-sub]');

  // Header card elements
  const slipBox = document.querySelector('[data-scan-header]');
  const slipLoader = slipBox?.querySelector('[data-loading]');
  const slipSchool = slipBox?.querySelector('[data-school]');
  const slipEmis = slipBox?.querySelector('[data-emis]');
  const slipSlip = slipBox?.querySelector('[data-slip]');
  const slipDate = slipBox?.querySelector('[data-date]');
  const slipBy = slipBox?.querySelector('[data-by]');

  const completeBtn = document.getElementById('completeScanBtn');

  // Shortage modal bits
  const reconModalEl = document.getElementById('reconModal');
  const reconModal = new bootstrap.Modal(reconModalEl);
  const reconMissingCountEl = document.getElementById('reconMissingCount');
  const reconMissingTBody = document.querySelector('#reconMissingTable tbody');
  const ackShortage = document.getElementById('ackShortage');
  const pinRow = document.getElementById('pinRow');
  const btnReconProceed = document.getElementById('btnReconProceed');

  if (!batchId) {
    window.toast && toast('Missing batchId.', 'danger');
    return;
  }

  // helpers
  async function jget(url) {
    const r = await fetch(url);
    if (!r.ok) throw new Error(await r.text());
    return r.json();
  }
  async function jpost(url, body) {
    const r = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body || {})
    });
    return { ok: r.ok, status: r.status, data: await (async () => { try { return await r.json(); } catch { return {}; } })() };
  }
  function cls(status) {
    const s = String(status || '').toLowerCase();
    if (s === 'matched') return 'status-received';
    if (s === 'mismatch') return 'status-mismatch';
    return 'status-not-found';
  }
  function fmt(dt) {
    const d = new Date(dt);
    return isNaN(d) ? '-' : d.toLocaleString();
  }

  // 1) table
  async function fetchTable() {
    const rows = await jget(`/api/phase1/rnr/batches/${batchId}/scans`);
    if (!rows || !rows.length) {
      tbody.innerHTML = `
        <tr><td colspan="7" class="text-center text-muted py-4">
          No devices scanned yet. Start scanning devices for R&R.
        </td></tr>`;
      return;
    }
    tbody.innerHTML = rows.map((r, i) => `
      <tr>
        <td>${i + 1}</td>
        <td>${r.serial ?? r.serialNumber ?? '-'}</td>
        <td>${r.deviceInfo ?? r.model ?? '-'}</td>
        <td>${r.schoolMatch ?? '-'}</td>
        <td><span class="device-status ${cls(r.status)}">${r.status ?? '-'}</span></td>
        <td>${fmt(r.scannedAt ?? r.createdAt)}</td>
        <td>
          ${r.itemId || r.receivingBatchItemId ? `
            <button class="btn btn-sm btn-outline-danger"
              data-id="${r.itemId ?? r.receivingBatchItemId}"
              onclick="window.__removeScan && window.__removeScan('${r.itemId ?? r.receivingBatchItemId}')">
              Remove
            </button>` : ''}
        </td>
      </tr>
    `).join('');
  }

  // 2) header counts
  async function fetchSummary() {
    const s = await jget(`/api/phase1/rnr/batches/${batchId}/summary`);
    const expected = s.expectedCount ?? 0;
    const scanned = s.onSlipScanned ?? s.scannedCount ?? 0;
    const missing = s.missing ?? s.missingCount ?? 0;

    scannedEl.textContent = scanned;
    if (totalEl) totalEl.textContent = expected;
    if (subEl) subEl.innerHTML = `of <span data-total>${expected}</span> scanned • Missing: ${missing}`;

    return { expected, scanned, missing, missingList: s.missingList || [] };
  }

  // Header details loader (Collection Slip Information)
  async function loadHeader() {
    try {
      const h = await jget(`/api/phase1/rnr/batches/${batchId}/header`);
      if (slipSchool) slipSchool.textContent = h.schoolName || '-';
      if (slipEmis)   slipEmis.textContent   = h.emisCode || '-';
      if (slipSlip)   slipSlip.textContent   = h.slipNumber || '-';
      if (slipDate)   slipDate.textContent   = h.collectionDate ? new Date(h.collectionDate).toLocaleString() : '-';
      if (slipBy)     slipBy.textContent     = h.collectedBy || '-';

      // Optionally sync counts
      if (totalEl)   totalEl.textContent   = h.expectedCount ?? 0;
      if (scannedEl) scannedEl.textContent = h.scannedCount ?? 0;
      if (subEl)     subEl.innerHTML       = `of <span data-total>${h.expectedCount ?? 0}</span> scanned • Missing: ${h.missingCount ?? 0}`;
    } catch (e) {
      console.error('Header load failed', e);
      window.toast && toast('Failed to load collection slip details.', 'danger');
    } finally {
      // Hide loader regardless so it never hangs
      if (slipLoader) slipLoader.style.display = 'none';
    }
  }

  async function fullRefresh() {
    await Promise.all([loadHeader(), fetchTable(), fetchSummary()]);
  }

  // 3) submit scan -> RnrReceivingController
  async function doScan() {
    const serial = (input.value || '').trim();
    if (!serial) return;
    scanBtn.disabled = true;
    statusEl.textContent = 'Submitting scan...';

    try {
      const { ok, status, data } = await jpost(`/api/phase1/rnr/batches/${batchId}/scan`, { serial });
      if (!ok) {
        const msg =
          data?.message ||
          data?.error ||
          (status === 409 ? 'Duplicate in this batch' : 'Scan failed');
        statusEl.textContent = msg;
        window.toast && toast(msg, 'danger');
      } else {
        statusEl.textContent = 'Scan saved.';
        window.toast && toast('Scan saved.', 'success');
        input.value = '';
        await fullRefresh();
      }
    } catch (e) {
      console.error(e);
      statusEl.textContent = 'Scan failed';
      window.toast && toast('Scan failed.', 'danger');
    } finally {
      scanBtn.disabled = false;
      input.focus();
    }
  }

  // 4) complete scanning -> shows modal if missing; otherwise redirect
  async function complete() {
    const { ok, status, data } = await jpost(`/api/phase1/rnr/batches/${batchId}/complete-scanning`);
    if (!ok && status >= 400) {
      window.toast && toast(data?.error || 'Failed to complete scanning.', 'danger');
      return;
    }

    if (data?.ok) {
      // success path: redirect to whatever server says (GRV/verification)
      location.href = data.nextUrl || `/phase1/rnr-verification.html?batchId=${batchId}`;
      return;
    }

    // missing / variance path: open modal and list missing
    const missing = data?.missing || [];
    reconMissingCountEl.textContent = missing.length;
    reconMissingTBody.innerHTML = missing.map((m, i) =>
      `<tr><td>${i + 1}</td><td>${m}</td><td>Missing</td></tr>` 
    ).join('');
    ackShortage.checked = false;
    pinRow.style.display = 'none';
    btnReconProceed.disabled = true;
    window.toast && toast(`${missing.length} device(s) missing from slip.`, 'warning');
    reconModal.show();

    // when they proceed, still go to nextUrl (usually verification page)
    btnReconProceed.onclick = () => {
      location.href = data?.nextUrl || `/phase1/rnr-verification.html?batchId=${batchId}`;
    };
  }

  // wire up UI
  scanBtn.addEventListener('click', doScan);
  input.addEventListener('keydown', e => { if (e.key === 'Enter') doScan(); });
  completeBtn.addEventListener('click', complete);

  // modal interactions
  ackShortage.addEventListener('change', () => {
    const on = ackShortage.checked;
    pinRow.style.display = on ? '' : 'none';
    // (optional) require PIN before enabling:
    btnReconProceed.disabled = !on;
  });

  // optional: remove scan hook (if your DeleteScannedDevice is used)
  window.__removeScan = async function (id) {
    if (!confirm('Remove this scan?')) return;
    const r = await fetch(`/api/phase1/scanning/devices/${id}`, { method: 'DELETE' });
    if (!r.ok) {
      window.toast && toast('Failed to remove scan', 'danger');
      return;
    }
    await fullRefresh();
  };

  // initial load
  fullRefresh().catch(console.error);
  input.focus();
})();
