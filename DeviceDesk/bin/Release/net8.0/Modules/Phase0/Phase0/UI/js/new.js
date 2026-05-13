(() => {
  const { API_BASE, fetchJson, byId } = PHASE0;

  // Card 1: CSV import
  const csvInput = byId('newCsvInput');
  const dropZone = byId('newDropZone');
  const chooseCsvLink = byId('newChooseCsvLink');
  const filePillWrap = byId('newFilePillWrap');
  const filePillText = byId('newFilePillText');
  const clearFileBtn = byId('newClearFileBtn');
  const importBtn = byId('newImportBtn');
  const alertEl = byId('newAlert');
  const templateLink = byId('newDownloadTemplateLink');

  // Card 2: manual batch
  const manualToggleBtn = byId('newManualToggleBtn');
  const manualFormWrap = byId('newManualFormWrap');
  const manualAlert = byId('newManualAlert');
  const manualDeviceType = byId('newManualDeviceType');
  const manualQty = byId('newManualQty');
  const manualBrand = byId('newManualBrand');
  const manualModel = byId('newManualModel');
  const manualDescription = byId('newManualDescription');
  const manualOrderNumber = byId('newManualOrderNumber');
  const addToBatchBtn = byId('newManualAddBtn');
  const cancelManualBtn = byId('newManualCancelBtn');
  const manualRowsBody = byId('newManualRows');
  const manualTotalLabel = byId('newManualTotal');
  const submitBatchBtn = byId('newSubmitBatchBtn');
  const emptyRow = byId('newManualEmptyRow');

  // Header date filter
  const dateFilterBtn = byId('newDateFilterBtn');
  const dateFilterLabel = byId('newDateFilterLabel');
  const dateMenu = byId('newDateFilterMenu');
  const dateOptionBtns = Array.from(document.querySelectorAll('[data-date-option]'));
  const customFrom = byId('newDateFrom');
  const customTo = byId('newDateTo');
  const customApplyBtn = byId('newDateApplyBtn');
  const customRangeWrap = byId('newCustomDateRangeWrap');

  const state = {
    selectedCsv: null,
    manualItems: [],
    dateFilter: { mode: 'today', from: '', to: '' },
  };

  function setAlert(el, type, html) {
    if (!el) return;
    el.style.display = html ? 'flex' : 'none';
    el.classList.remove('success', 'error', 'info');
    if (type) el.classList.add(type);
    el.innerHTML = html || '';
  }

  function escapeHtml(str) {
    return String(str ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  function setCsvFile(file) {
    state.selectedCsv = file || null;
    if (!state.selectedCsv) {
      filePillWrap?.classList.add('d-none');
      if (csvInput) csvInput.value = '';
      if (importBtn) importBtn.disabled = true;
      return;
    }
    filePillWrap?.classList.remove('d-none');
    if (filePillText) filePillText.textContent = state.selectedCsv.name;
    if (importBtn) importBtn.disabled = false;
  }

  function downloadTemplateCsv() {
    const headers = ['DeviceType', 'Quantity', 'Brand', 'Model', 'Description', 'OrderNumber'];
    const content = `${headers.join(',')}\n`;
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'new_stock_batch_template.csv';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  function renderManualTable() {
    if (!manualRowsBody) return;
    manualRowsBody.innerHTML = '';
    if (!state.manualItems.length) {
      if (emptyRow) {
        const tr = document.createElement('tr');
        tr.id = 'newManualEmptyRow';
        tr.innerHTML = `<td colspan="7" class="sr-empty-inline">No items in this session batch yet.</td>`;
        manualRowsBody.appendChild(tr);
      }
      if (submitBatchBtn) submitBatchBtn.disabled = true;
      if (manualTotalLabel) manualTotalLabel.textContent = 'Total items: 0';
      return;
    }

    state.manualItems.forEach((it, idx) => {
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td>${escapeHtml(it.deviceType)}</td>
        <td><span class="sr-qty-pill">${escapeHtml(String(it.qty))}</span></td>
        <td>${escapeHtml(it.brand || '')}</td>
        <td>${escapeHtml(it.model || '')}</td>
        <td>${escapeHtml(it.description || '')}</td>
        <td>${escapeHtml(it.orderNumber || '')}</td>
        <td class="text-center">
          <button type="button" class="btn btn-sm btn-light" data-remove-idx="${idx}" aria-label="Remove row">✕</button>
        </td>
      `;
      manualRowsBody.appendChild(tr);
    });

    manualRowsBody.querySelectorAll('[data-remove-idx]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const idx = Number(btn.getAttribute('data-remove-idx'));
        if (Number.isNaN(idx)) return;
        state.manualItems.splice(idx, 1);
        renderManualTable();
      });
    });

    if (submitBatchBtn) submitBatchBtn.disabled = false;
    if (manualTotalLabel) manualTotalLabel.textContent = `Total items: ${state.manualItems.length}`;
  }

  function clearManualForm() {
    if (manualDeviceType) manualDeviceType.value = '';
    if (manualQty) manualQty.value = '1';
    if (manualBrand) manualBrand.value = '';
    if (manualModel) manualModel.value = '';
    if (manualDescription) manualDescription.value = '';
    if (manualOrderNumber) manualOrderNumber.value = '';
    [manualDeviceType, manualQty].forEach((el) => {
      if (!el) return;
      el.classList.remove('sr-validation-invalid');
      el.removeAttribute('title');
    });
  }

  function validateManualInputs() {
    const deviceType = (manualDeviceType?.value || '').trim();
    const qty = Math.max(1, Number.parseInt((manualQty?.value || '1').trim(), 10) || 0);
    let ok = true;

    [manualDeviceType, manualQty].forEach((el) => {
      if (!el) return;
      el.classList.remove('sr-validation-invalid');
      el.removeAttribute('title');
    });

    if (!deviceType) {
      ok = false;
      if (manualDeviceType) {
        manualDeviceType.classList.add('sr-validation-invalid');
        manualDeviceType.setAttribute('title', 'DeviceType is required.');
      }
    }
    if (qty < 1) {
      ok = false;
      if (manualQty) {
        manualQty.classList.add('sr-validation-invalid');
        manualQty.setAttribute('title', 'Quantity must be at least 1.');
      }
    }
    return { ok, deviceType, qty };
  }

  async function submitManualBatch() {
    if (!state.manualItems.length) return;
    setAlert(manualAlert, null, '');
    if (submitBatchBtn) submitBatchBtn.disabled = true;

    const items = state.manualItems.map((it) => ({
      deviceType: it.deviceType,
      qty: it.qty,
      brand: it.brand || null,
      model: it.model || null,
      description: it.description || null,
      orderNumber: it.orderNumber || null,
      serial: null,
      imei: null,
    }));

    const fd = new FormData();
    fd.append('itemsJson', JSON.stringify({ items }));

    try {
      const res = await fetchJson(`${API_BASE}/new/import-manual`, { method: 'POST', body: fd });
      setAlert(
        manualAlert,
        'success',
        `✓ Batch submitted: ${res?.added ?? 0} devices created${res?.batchId ? ` (Batch ${res.batchId})` : ''}. <a href="/phase0/new-batches.html">View batches</a>`
      );
      state.manualItems = [];
      renderManualTable();
      clearManualForm();
      manualFormWrap?.classList.remove('open');
    } catch (e) {
      setAlert(manualAlert, 'error', `Submit failed: ${e?.message || 'Unknown error'}`);
    } finally {
      if (submitBatchBtn) submitBatchBtn.disabled = !state.manualItems.length;
    }
  }

  function setDateMode(mode) {
    state.dateFilter.mode = mode;
    const labels = {
      today: 'Today',
      week: 'This Week',
      month: 'This Month',
      custom: 'Custom Range',
    };
    if (dateFilterLabel) dateFilterLabel.textContent = labels[mode] || 'Today';
    dateOptionBtns.forEach((btn) => {
      const selected = btn.getAttribute('data-date-option') === mode;
      btn.classList.toggle('selected', selected);
    });
    if (customRangeWrap) customRangeWrap.style.display = mode === 'custom' ? 'block' : 'none';
  }

  function closeDateMenu() {
    dateMenu?.classList.remove('open');
    dateFilterBtn?.setAttribute('aria-expanded', 'false');
  }
  function openDateMenu() {
    dateMenu?.classList.add('open');
    dateFilterBtn?.setAttribute('aria-expanded', 'true');
  }

  // CSV controls
  templateLink?.addEventListener('click', (e) => { e.preventDefault(); downloadTemplateCsv(); });
  chooseCsvLink?.addEventListener('click', (e) => { e.preventDefault(); csvInput?.click(); });
  dropZone?.addEventListener('click', () => csvInput?.click());
  csvInput?.addEventListener('change', () => setCsvFile(csvInput.files?.[0] || null));
  clearFileBtn?.addEventListener('click', (e) => { e.preventDefault(); setCsvFile(null); });

  ['dragenter', 'dragover'].forEach((evt) => {
    dropZone?.addEventListener(evt, (e) => {
      e.preventDefault();
      dropZone.classList.add('sr-dropzone-active');
    });
  });
  ['dragleave', 'drop'].forEach((evt) => {
    dropZone?.addEventListener(evt, (e) => {
      e.preventDefault();
      dropZone.classList.remove('sr-dropzone-active');
    });
  });
  dropZone?.addEventListener('drop', (e) => {
    const file = e.dataTransfer?.files?.[0];
    if (file) setCsvFile(file);
  });

  importBtn?.addEventListener('click', async () => {
    if (!state.selectedCsv) {
      setAlert(alertEl, 'error', 'Please select a CSV file first.');
      return;
    }
    setAlert(alertEl, null, '');
    importBtn.disabled = true;
    try {
      const fd = new FormData();
      fd.append('file', state.selectedCsv);
      const res = await fetchJson(`${API_BASE}/new/import`, { method: 'POST', body: fd });
      setAlert(
        alertEl,
        'success',
        `✓ ${res?.added ?? 0} items imported, ${res?.duplicates ?? 0} duplicates skipped${res?.invalid ? `, ${res.invalid} invalid` : ''}. <a href="/phase0/new-batches.html">View batches</a>`
      );
      setCsvFile(null);
    } catch (e) {
      setAlert(alertEl, 'error', `Import failed: ${e?.message || 'Unknown error'}`);
    } finally {
      importBtn.disabled = !state.selectedCsv;
    }
  });

  // Manual form controls
  manualToggleBtn?.addEventListener('click', () => {
    const isOpen = manualFormWrap?.classList.contains('open');
    if (isOpen) {
      manualFormWrap?.classList.remove('open');
      clearManualForm();
      setAlert(manualAlert, null, '');
    } else {
      manualFormWrap?.classList.add('open');
      manualDeviceType?.focus();
    }
  });

  cancelManualBtn?.addEventListener('click', () => {
    manualFormWrap?.classList.remove('open');
    clearManualForm();
    setAlert(manualAlert, null, '');
  });

  addToBatchBtn?.addEventListener('click', () => {
    const { ok, deviceType, qty } = validateManualInputs();
    if (!ok) return;

    state.manualItems.push({
      deviceType,
      qty,
      brand: (manualBrand?.value || '').trim(),
      model: (manualModel?.value || '').trim(),
      description: (manualDescription?.value || '').trim(),
      orderNumber: (manualOrderNumber?.value || '').trim(),
    });
    renderManualTable();
    clearManualForm();
    manualDeviceType?.focus();
  });

  submitBatchBtn?.addEventListener('click', submitManualBatch);

  // Header date filter controls
  dateFilterBtn?.addEventListener('click', (e) => {
    e.preventDefault();
    if (dateMenu?.classList.contains('open')) closeDateMenu();
    else openDateMenu();
  });

  dateOptionBtns.forEach((btn) => {
    btn.addEventListener('click', () => {
      const mode = btn.getAttribute('data-date-option');
      if (!mode) return;
      setDateMode(mode);
      if (mode !== 'custom') closeDateMenu();
    });
  });

  customApplyBtn?.addEventListener('click', () => {
    state.dateFilter.from = customFrom?.value || '';
    state.dateFilter.to = customTo?.value || '';
    setDateMode('custom');
    closeDateMenu();
  });

  document.addEventListener('click', (e) => {
    const inside = e.target?.closest?.('#newDateFilterWrap');
    if (!inside) closeDateMenu();
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') closeDateMenu();
  });

  // Init
  setCsvFile(null);
  renderManualTable();
  setDateMode('today');
  setAlert(alertEl, null, '');
  setAlert(manualAlert, null, '');

  try {
    if (window.lucide && typeof window.lucide.createIcons === 'function') window.lucide.createIcons();
  } catch {}
})();