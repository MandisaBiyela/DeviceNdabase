(() => {
  const { API_BASE, fetchJson, byId, show } = PHASE0;

  const fileInput = byId('newOneFile');
  const importBtn = byId('newImportBtn');
  const msg = byId('newMsg');

  // Manual modal elements
  const modal = byId('manualModal');
  const manualClose = byId('manualClose');
  const manualBtn = byId('newManualBtn');
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

    // Upload file to backend CsvImportService
  importBtn.addEventListener('click', async () => {
    setMsg(msg, '');
    const f = fileInput.files[0];
    if (!f) { setMsg(msg, 'Please select a spreadsheet first.', true); return; }
    
    // Upload file to backend for processing
    try {
      const formData = new FormData();
      formData.append('file', f);
      
      const res = await fetch(`${location.origin}/api/phase0/new/import`, {
        method: 'POST',
        body: formData
      });
      
      if (!res.ok) {
        const error = await res.json();
        throw new Error(error.error || 'Upload failed');
      }
      
      const result = await res.json();
      setMsgHtml(msg, `✅ Import successful! Added: ${result.added}, Duplicates: ${result.duplicates}, Invalid: ${result.invalid}, Total: ${result.total}. ` +
        `<a href="/phase0/new-stock-batch.html">View Batches</a>`);
      fileInput.value = '';
    } catch (e) {
      setMsg(msg, `Error: ${e.message}`, true);
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
      <td><input class="form-control" value="${row.brand || ''}" placeholder="HP" /></td>
      <td><input class="form-control" value="${row.model || ''}" placeholder="EliteBook 840" /></td>
      <td><select class="form-select">
        <option value="Laptop" ${row.deviceType === 'Laptop' ? 'selected' : ''}>Laptop</option>
        <option value="Desktop" ${row.deviceType === 'Desktop' ? 'selected' : ''}>Desktop</option>
        <option value="Tablet" ${row.deviceType === 'Tablet' ? 'selected' : ''}>Tablet</option>
        <option value="Chromebook" ${row.deviceType === 'Chromebook' ? 'selected' : ''}>Chromebook</option>
        <option value="Other" ${row.deviceType === 'Other' ? 'selected' : ''}>Other</option>
      </select></td>
      <td><input class="form-control" value="${row.description || ''}" placeholder="14-inch laptop" /></td>
      <td><input class="form-control" type="number" min="1" value="${row.quantity || 1}" /></td>
      <td><button class="btn btn-light del">×</button></td>
    `;
    tr.querySelector('.del').addEventListener('click', () => tr.remove());
    manualTable.appendChild(tr);
  }
  manualAddRow.addEventListener('click', () => addRow());

  manualSubmit.addEventListener('click', async () => {
    setMsg(manualMsg,'');
    const items = [...manualTable.querySelectorAll('tr')].map(tr => {
      const inputs = tr.querySelectorAll('input');
      const select = tr.querySelector('select');
      return {
        brand: inputs[0].value.trim() || null,
        model: inputs[1].value.trim() || null,
        deviceType: select.value,
        description: inputs[2].value.trim() || null,
        quantityExpected: Number(inputs[3].value || 1)
      };
    }).filter(r => r.quantityExpected > 0);

    if (!items.length) {
      setMsg(manualMsg, 'Please add at least one row with quantity > 0.', true);
      return;
    }

    // Create batch via new API
    const payload = {
      supplierName: null,
      invoiceNumber: null,
      expectedDeliveryDate: null,
      items: items,
      createdBy: 'clerk@example.com' // TODO: Get from session
    };

    try {
      const res = await fetch(`${location.origin}/api/phase0/newstock/batches`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      
      if (!res.ok) throw new Error('Failed to create batch');
      
      const result = await res.json();
      setMsgHtml(manualMsg, `✅ Batch ${result.batchNumber} created with ${result.totalQuantityExpected} items. ` +
        `<a href="/phase0/new-stock-batch.html">View Batches</a>`);
      closeModal();
    } catch (e) {
      setMsg(manualMsg, `Error: ${e.message}`, true);
    }
  });
})();