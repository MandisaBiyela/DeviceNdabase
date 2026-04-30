(() => {
  const rowsEl = document.getElementById('rows');
  const daysEl = document.getElementById('days');

  // Ensure we are on the correct view
  if (!rowsEl || !daysEl) return;

  let skip = 0;
  const take = 50;

  function niceCase(s) { return s && s.replace(/([a-z])([A-Z])/g, '$1 $2'); }

  function actionText(row) {
    const raw = row.action || row.Action;
    const disposal = row.disposalRequested ?? row.DisposalRequested;
    const repairable = (row.repairable ?? row.Repairable);
    if (raw === 'DetailedInspection') return 'Detailed inspection';
    if (raw === 'DisposalRequest') return 'Disposal requested';
    if (raw) return niceCase(raw);
    if (disposal) return 'Disposal requested';
    if (repairable === true) return 'Repaired / OK';
    if (repairable === false) return 'Not repairable';
    return 'Inspection done';
  }

  function stageText(row) {
    return row.stageName || row.StageName || row.stage || row.Stage || '';
  }

  function timestampOf(row) {
    return row.timestamp || row.Timestamp || row.inspectionDate || row.InspectionDate || row.updatedAt || row.UpdatedAt;
  }

  function notesOf(row) {
    return row.notes || row.Notes || row.repairCategory || row.RepairCategory || '';
  }

  async function load(delta = 0) {
    skip = Math.max(0, skip + delta * take);
    const days = parseInt(daysEl.value || '30', 10);
    const url = `/api/phase2/technician/my/history?days=${days}&take=${take}&skip=${skip}`;
    rowsEl.innerHTML = '';
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
          <td>${deviceId ? `<a href="#" onclick="loadTechDetailFromHistory(${deviceId}, true); return false;">${serial}</a>` : serial}</td>
          <td>${stage}</td>
          <td>${notes}</td>
          <td>${deviceId ? `<button class="btn btn-link btn-sm p-0" onclick="loadTechDetailFromHistory(${deviceId}, true)">View</button>` : ''}</td>
        `;
        rowsEl.appendChild(tr);
      }
    } catch (err) {
      console.error('Error loading technician history:', err);
    }
  }

  document.getElementById('refreshBtn')?.addEventListener('click', () => load(0));
  document.getElementById('prevBtn')?.addEventListener('click', () => load(-1));
  document.getElementById('nextBtn')?.addEventListener('click', () => load(1));

  load(0);
})();