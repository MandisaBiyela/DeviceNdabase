(() => {
  const rowsEl = document.getElementById('insp_rows');
  const statusEl = document.getElementById('insp_status');
  const daysEl = document.getElementById('insp_days');
  const pagerInfoEl = document.getElementById('insp_pagerInfo');

  // Ensure we are on the correct view
  if (!rowsEl || !daysEl) return;

  let skip = 0;
  const take = 50;

  function niceCase(s) { return s && s.replace(/([a-z])([A-Z])/g, '$1 $2'); }

  function actionText(row) {
    const raw = row.action || row.Action;
    const qaPassed = row.qaPassed ?? row.QaPassed;
    const prePassed = row.preAssessmentPassed ?? row.PreAssessmentPassed;
    if (raw === 'PreAssessment') {
      if (prePassed === true) return 'Pre-assessment (passed)';
      if (prePassed === false) return 'Pre-assessment (failed)';
      return 'Pre-assessment';
    }
    if (raw === 'QualityAssessment') {
      if (qaPassed === true) return 'QA Passed';
      if (qaPassed === false) return 'QA Failed';
      return 'Quality assessment';
    }
    if (raw) return niceCase(raw);
    if (qaPassed === true) return 'QA Passed';
    if (qaPassed === false) return 'QA Failed';
    return 'QA Reviewed';
  }

  function stageText(row) {
    return row.stageName || row.StageName || row.stage || row.Stage || '';
  }

  function timestampOf(row) {
    return row.timestamp || row.Timestamp || row.updatedAt || row.UpdatedAt;
  }

  function notesOf(row) {
    return row.notes || row.Notes || '';
  }

  async function load(delta = 0) {
    skip = Math.max(0, skip + delta * take);
    const days = parseInt(daysEl.value || '30', 10);
    const url = `/api/phase2/quality/my/history?days=${days}&take=${take}&skip=${skip}`;
    if (statusEl) statusEl.textContent = 'Loading...';
    rowsEl.innerHTML = '';
    if (pagerInfoEl) pagerInfoEl.textContent = '';
    try {
      const res = await fetch(url, { credentials: 'include', headers: { 'Accept': 'application/json' } });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      const list = Array.isArray(data?.items) ? data.items : (Array.isArray(data) ? data : []);
      for (const row of list) {
        const tr = document.createElement('tr');
        const ts = timestampOf(row);
        const action = actionText(row);
        const stage = stageText(row);
        const notes = notesOf(row);
        const deviceId = row.deviceId ?? row.DeviceId;
        const serial = row.serial ?? row.Serial ?? '';
        tr.innerHTML = `
          <td>${ts ? formatLocalTime(ts) : ''}</td>
          <td>${action}</td>
          <td>${deviceId ? `<a href=\"#\" onclick=\"viewInspectorHistory(${deviceId}); return false;\">${serial}</a>` : serial}</td>
          <td>${stage}</td>
          <td>${notes}</td>
          <td>${deviceId ? `<button class=\"btn btn-link btn-sm p-0\" onclick=\"viewInspectorHistory(${deviceId})\">View</button>` : ''}</td>
        `;
        rowsEl.appendChild(tr);
      }
      const total = data.total ?? list.length;
      if (pagerInfoEl) pagerInfoEl.textContent = `Showing ${list.length} · skip ${skip}`;
      if (statusEl) statusEl.textContent = `Loaded ${list.length} rows`;
    } catch (err) {
      if (statusEl) statusEl.textContent = `Error: ${err.message}`;
    }
  }

  document.getElementById('insp_refreshBtn')?.addEventListener('click', () => load(0));
  document.getElementById('insp_prevBtn')?.addEventListener('click', () => load(-1));
  document.getElementById('insp_nextBtn')?.addEventListener('click', () => load(1));

  // Expose a global helper for opening read-only inspector detail from history
  window.viewInspectorHistory = function(deviceId) {
    try {
      window.currentDetailedDeviceId = deviceId;
      window.currentDetailedReadOnly = true;
      if (typeof window.loadTechDetailFromHistory === 'function') {
        window.loadTechDetailFromHistory(deviceId, true);
      } else {
        // Fallback: navigate directly
        location.href = `/Modules/Phase2/UI/technician-detail.html?deviceId=${encodeURIComponent(deviceId)}&readOnly=1`;
      }
    } catch (e) {
      console.error('Failed to open inspector detail from history:', e);
    }
  };

  load(0);
})();