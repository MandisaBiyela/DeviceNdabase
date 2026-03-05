(() => {
  const { API_BASE, fetchJson, byId, show } = PHASE0;

  const fileInput = byId('rnrOneFile');
  const importBtn = byId('rnrImportBtn');
  const msg = byId('rnrMsg');

  // Manual modal elements
  const modal = byId('manualModal');
  const manualClose = byId('manualClose');
  const manualBtn = byId('rnrManualBtn');
  const manualTable = byId('manualTable').querySelector('tbody');
  const manualAddRow = byId('manualAddRow');
  const manualPack = byId('manualPack');
  const manualSubmit = byId('manualSubmit');
  const manualMsg = byId('manualMsg');

  function setMsg(el, text, isErr=false){
    el.textContent = text;
    el.className = `mt-2 ${isErr ? 'text-danger' : 'text-muted'}`;
  }
  function setMsgHtml(el, html, isErr=false){
    el.innerHTML = html;
    el.className = `mt-2 ${isErr ? 'text-danger' : 'text-muted'}`;
  }

  // Single spreadsheet; attach same file to batch
  importBtn.addEventListener('click', async () => {
    setMsg(msg, '');
    const f = fileInput.files[0];
    if (!f) { setMsg(msg, 'Please select a spreadsheet first.', true); return; }
    const fd = new FormData();
    fd.append('file', f);
    fd.append('attach', 'true');
    try {
      const res = await fetchJson(`${API_BASE}/rnr/import`, { method:'POST', body: fd });
      const up = res?.packUploaded ? ' (+ file attached to batch)' : '';
      const batchId = res.batchId;
      setMsgHtml(msg, `Imported ${res.added}/${res.total} (duplicates ${res.duplicates}, invalid ${res.invalid}) — batch ${batchId}${up}. ` +
        `<a href="/phase0/batch-items.html?id=${batchId}">Open</a> · <a href="/phase0/rnr-batches.html">All batches</a>`);
      fileInput.value = '';
    } catch (e) {
      setMsg(msg, `Server unreachable. Run the ASP.NET app. ${e.message}`, true);
    }
  });

  // 2) Manual entry modal
  function openModal(){ show(modal, true); }
  function closeModal(){ show(modal, false); manualTable.innerHTML=''; manualPack.value=''; setMsg(manualMsg,''); }

  manualBtn.addEventListener('click', () => { openModal(); addRow(); });
  manualClose.addEventListener('click', closeModal);

  function addRow(row = {}) {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td><input class="form-control" value="${row.serial || ''}" /></td>
      <td><input class="form-control" value="${row.imei || ''}" /></td>
      <td><input class="form-control" value="${row.brand || ''}" /></td>
      <td><input class="form-control" value="${row.model || ''}" /></td>
      <td><input class="form-control" type="number" min="1" value="${row.qty || 1}" /></td>
      <td><button class="btn btn-light del">×</button></td>
    `;
    tr.querySelector('.del').addEventListener('click', () => tr.remove());
    manualTable.appendChild(tr);
  }
  manualAddRow.addEventListener('click', () => addRow());

  manualSubmit.addEventListener('click', async () => {
    setMsg(manualMsg,'');
    const items = [...manualTable.querySelectorAll('tr')].map(tr => {
      const [serial, imei, brand, model, qty] = [...tr.querySelectorAll('input')].map(i => i.value.trim());
      return { serial, imei, brand, model, qty: Number(qty || 1) };
    }).filter(r => r.serial || r.imei);

    if (!items.length) {
      setMsg(manualMsg, 'Please add at least one row with Serial or IMEI.', true);
      return;
    }

    // Send as multipart: JSON payload + optional pack
    const fd = new FormData();
    fd.append('itemsJson', JSON.stringify({ items }));
    if (manualPack.files[0]) fd.append('pack', manualPack.files[0]);

    try {
      const res = await fetchJson(`${API_BASE}/rnr/import-manual`, { method:'POST', body: fd });
      const up = res?.packUploaded ? ' (+ pack uploaded)' : '';
      const batchId = res.batchId;
      setMsgHtml(manualMsg, `Saved ${res.added} manual items — batch ${batchId}${up}. ` +
        `<a href="/phase0/batch-items.html?id=${batchId}">Open</a> · <a href="/phase0/rnr-batches.html">All batches</a>`);
    } catch (e) {
      setMsg(manualMsg, `Server unreachable. Run the ASP.NET app. ${e.message}`, true);
    }
  });
})();