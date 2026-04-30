(() => {
  const { API_BASE, fetchJson, byId } = PHASE0;

  // ---- CSV upload (Card 1) ----
  const csvInput = byId('rnrCsvInput');
  const dropZone = byId('rnrDropZone');
  const chooseCsvLink = byId('rnrChooseCsvLink');
  const filePillWrap = byId('rnrFilePillWrap');
  const filePillText = byId('rnrFilePillText');
  const clearFileBtn = byId('rnrClearFileBtn');
  const importBtn = byId('rnrImportBtn');
  const alertEl = byId('rnrAlert');
  const templateLink = byId('rnrDownloadTemplateLink');

  let selectedFile = null;

  function setAlert(type, html) {
    alertEl.style.display = html ? 'block' : 'none';
    alertEl.classList.remove('success', 'error', 'info');
    if (type) alertEl.classList.add(type);
    alertEl.innerHTML = html || '';
  }

  function setFile(file) {
    selectedFile = file || null;
    if (!selectedFile) {
      filePillWrap.classList.add('d-none');
      importBtn.disabled = true;
      setAlert(null, '');
      if (csvInput) csvInput.value = '';
      return;
    }

    filePillWrap.classList.remove('d-none');
    filePillText.textContent = selectedFile.name;
    importBtn.disabled = false;
    setAlert(null, '');
  }

  function downloadTemplateCsv() {
    const headers = ['Serial', 'IMEI', 'Brand', 'Model', 'Qty', 'EMIS'];
    const csv = headers.join(',') + '\n';
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'rnr_handover_template.csv';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  templateLink?.addEventListener('click', (e) => {
    e.preventDefault();
    downloadTemplateCsv();
  });

  // Choose file (link + dropzone click)
  dropZone?.addEventListener('click', () => csvInput?.click());
  chooseCsvLink?.addEventListener('click', (e) => {
    e.preventDefault();
    csvInput?.click();
  });

  csvInput?.addEventListener('change', () => {
    const f = csvInput?.files?.[0] || null;
    setFile(f);
  });

  clearFileBtn?.addEventListener('click', (e) => {
    e.preventDefault();
    setFile(null);
  });

  // Drag and drop
  function onDragOver(e) {
    e.preventDefault();
    e.stopPropagation();
    dropZone.classList.add('sr-dropzone-active');
  }
  function onDragLeave(e) {
    e.preventDefault();
    e.stopPropagation();
    dropZone.classList.remove('sr-dropzone-active');
  }
  function onDrop(e) {
    e.preventDefault();
    e.stopPropagation();
    dropZone.classList.remove('sr-dropzone-active');

    const files = e.dataTransfer?.files;
    if (!files || files.length === 0) return;

    // Keep first matching file
    const f = files[0];
    if (!f) return;
    setFile(f);
  }

  dropZone?.addEventListener('dragover', onDragOver);
  dropZone?.addEventListener('dragenter', onDragOver);
  dropZone?.addEventListener('dragleave', onDragLeave);
  dropZone?.addEventListener('drop', onDrop);

  importBtn?.addEventListener('click', async () => {
    if (!selectedFile) {
      setAlert('error', `Please select a file first.`);
      return;
    }

    setAlert(null, '');
    importBtn.disabled = true;
    try {
      const fd = new FormData();
      fd.append('file', selectedFile);
      fd.append('attach', 'true');

      const res = await fetchJson(`${API_BASE}/rnr/import`, { method: 'POST', body: fd });

      const added = res?.added ?? 0;
      const duplicates = res?.duplicates ?? 0;
      const invalid = res?.invalid ?? 0;
      const batchId = res?.batchId;
      const up = res?.packUploaded ? ' (and file attached to batch)' : '';

      const link1 = batchId ? `<a href="/phase0/batch-items.html?id=${batchId}">Open</a>` : '';
      const link2 = `<a href="/phase0/rnr-batches.html">All batches</a>`;

      setAlert('success',
        `✓ ${added} devices imported, ${duplicates} duplicates skipped${invalid ? `, ${invalid} invalid skipped` : ''}. ` +
        (batchId ? `Batch: ${batchId}. ${link1} · ${link2}.` : `${link2}.`) + up
      );

      setFile(null);
    } catch (e) {
      setAlert('error', `Import failed: ${e?.message || 'Unknown error'}`);
    } finally {
      importBtn.disabled = !selectedFile;
    }
  });

  // ---- Manual entry (Card 2) ----
  const manualToggleBtn = byId('rnrManualToggleBtn');
  const manualFormWrap = byId('manualFormWrap');
  const manualSerial = byId('manualSerial');
  const manualImei = byId('manualImei');
  const manualBrand = byId('manualBrand');
  const manualModel = byId('manualModel');
  const manualQty = byId('manualQty');
  const manualEmis = byId('manualEmis');
  const manualAddDeviceBtn = byId('manualAddDeviceBtn');
  const manualCancelBtn = byId('manualCancelBtn');
  const manualDevicesBody = byId('manualDevicesBody');
  const manualDevicesEmpty = byId('manualDevicesEmpty');
  const manualAlert = byId('manualAlert');

  const manualState = {
    devices: [],
  };

  function setManualAlert(type, html) {
    if (!manualAlert) return;
    manualAlert.style.display = html ? 'block' : 'none';
    manualAlert.classList.remove('success', 'error', 'info');
    if (type) manualAlert.classList.add(type);
    manualAlert.innerHTML = html || '';
  }

  function clearManualInputs() {
    manualSerial.value = '';
    manualImei.value = '';
    manualBrand.value = '';
    manualModel.value = '';
    manualQty.value = '1';
    manualEmis.value = '';
    [manualSerial, manualImei].forEach((el) => el?.classList.remove('sr-validation-invalid'));
  }

  function setManualInvalid(invalidSerial, invalidImei) {
    [manualSerial, manualImei].forEach((el) => {
      if (!el) return;
      el.classList.remove('sr-validation-invalid');
      el.removeAttribute('title');
    });

    if (invalidSerial && manualSerial) {
      manualSerial.classList.add('sr-validation-invalid');
      manualSerial.setAttribute('title', 'Serial or IMEI is required.');
    }
    if (invalidImei && manualImei) {
      manualImei.classList.add('sr-validation-invalid');
      manualImei.setAttribute('title', 'Serial or IMEI is required.');
    }
  }

  function openManualForm() {
    manualFormWrap?.classList.add('open');
    manualSerial?.focus?.();
  }
  function closeManualForm() {
    manualFormWrap?.classList.remove('open');
    clearManualInputs();
    setManualAlert(null, '');
  }

  manualToggleBtn?.addEventListener('click', () => {
    if (manualFormWrap?.classList.contains('open')) closeManualForm();
    else openManualForm();
  });

  manualCancelBtn?.addEventListener('click', () => {
    closeManualForm();
  });

  function renderManualTable() {
    const devices = manualState.devices;
    manualDevicesBody.innerHTML = '';

    if (!devices.length) {
      manualDevicesEmpty.style.display = 'block';
      return;
    }

    manualDevicesEmpty.style.display = 'none';

    devices.forEach((d, idx) => {
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td>${d.serial ? escapeHtml(d.serial) : ''}</td>
        <td>${d.imei ? escapeHtml(d.imei) : ''}</td>
        <td>${d.brand ? escapeHtml(d.brand) : ''}</td>
        <td>${d.model ? escapeHtml(d.model) : ''}</td>
        <td class="text-end">${d.qty}</td>
        <td>${d.emis ? escapeHtml(d.emis) : ''}</td>
        <td class="text-center">
          <button type="button" class="btn btn-sm btn-light sr-mini-remove" aria-label="Remove item">✕</button>
        </td>
      `;

      tr.querySelector('.sr-mini-remove')?.addEventListener('click', () => {
        manualState.devices.splice(idx, 1);
        renderManualTable();
      });
      manualDevicesBody.appendChild(tr);
    });
  }

  // Minimal HTML escaping for user-entered values
  function escapeHtml(str) {
    return String(str ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  function normalizeText(v) {
    return (v ?? '').toString().trim();
  }

  manualAddDeviceBtn?.addEventListener('click', () => {
    const serial = normalizeText(manualSerial.value);
    const imei = normalizeText(manualImei.value);
    const brand = normalizeText(manualBrand.value);
    const model = normalizeText(manualModel.value);
    const emis = normalizeText(manualEmis.value);
    const qty = Math.max(1, Number.parseInt(normalizeText(manualQty.value) || '1', 10) || 1);

    const hasKey = !!(serial || imei);
    if (!hasKey) {
      setManualInvalid(true, true);
      manualSerial.focus();
      return;
    }

    setManualInvalid(false, false);

    // Immediately persist to backend
    const btn = manualAddDeviceBtn;
    if (btn) btn.disabled = true;
    setManualAlert(null, '');

    const items = [
      {
        serial: serial || '',
        imei: imei || '',
        brand: brand || '',
        model: model || '',
        qty: qty,
      },
    ];

    const fd = new FormData();
    fd.append('itemsJson', JSON.stringify({ items }));

    (async () => {
      try {
        const res = await fetchJson(`${API_BASE}/rnr/import-manual`, { method: 'POST', body: fd });

        const added = res?.added ?? 0;
        const duplicates = res?.duplicates ?? 0;
        const invalid = res?.invalid ?? 0;
        const batchId = res?.batchId;

        if (added > 0) {
          manualState.devices.push({
            serial: serial || '',
            imei: imei || '',
            brand: brand || '',
            model: model || '',
            qty,
            // UI-only: current backend schema doesn't persist EMIS for RNR devices yet.
            emis: emis || '',
          });
          renderManualTable();
          clearManualInputs();
          setManualAlert(
            'success',
            `✓ Device added${batchId ? ` to batch ${batchId}` : ''}.`
          );
        } else {
          // Keep values so the user can adjust (e.g., fix invalid key or re-check duplicates).
          const msg = duplicates
            ? `Duplicate skipped (${duplicates} duplicates).`
            : invalid
              ? `Invalid item skipped (${invalid} invalid).`
              : `No device added.`;
          setManualAlert('info', msg);
        }
      } catch (e) {
        setManualAlert('error', `Save failed: ${e?.message || 'Unknown error'}`);
      } finally {
        if (btn) btn.disabled = false;
      }
    })();
  });

  // ---- Init ----
  window.addEventListener('DOMContentLoaded', () => {
    try {
      if (window.lucide && typeof window.lucide.createIcons === 'function') {
        window.lucide.createIcons();
      }
    } catch {}
  });

  // Ensure initial state
  if (manualDevicesEmpty) manualDevicesEmpty.style.display = 'block';
})();