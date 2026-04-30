(() => {
  const API_BASE = `${location.origin}/api/phase1`;
  const DATE_LABEL = { today: 'Today', week: 'This Week', month: 'This Month', all: 'All Time' };
  let selectedRange = 'today';
  let allActivity = [];
  let visibleActivityCount = 10;

  document.addEventListener('DOMContentLoaded', async () => {
    wireDateFilter();
    wireRoutes();
    wireExport();
    await loadProfile();
    await Promise.all([loadDashboardStats(), loadRecentActivity()]);
    renderActivityList();
    if (window.lucide && typeof window.lucide.createIcons === 'function') window.lucide.createIcons();
  });

  function wireRoutes() {
    const goto = (primary, fallback) => {
      fetch(primary, { method: 'HEAD' }).then((res) => {
        location.href = res.ok ? primary : fallback;
      }).catch(() => { location.href = fallback; });
    };
    document.getElementById('btnCreateBatch')?.addEventListener('click', () => goto('/phase1/new-batch', '/phase1/receiving-create.html'));
    document.getElementById('btnViewAllBatches')?.addEventListener('click', () => goto('/phase1/batches', '/phase1/receiving-list.html'));
    document.getElementById('navNewBatch')?.addEventListener('click', (e) => { e.preventDefault(); goto('/phase1/new-batch', '/phase1/receiving-create.html'); });
    document.getElementById('navAllBatches')?.addEventListener('click', (e) => { e.preventDefault(); goto('/phase1/batches', '/phase1/receiving-list.html'); });
  }

  function wireDateFilter() {
    const btn = document.getElementById('dateFilterBtn');
    const menu = document.getElementById('dateFilterMenu');
    btn?.addEventListener('click', (e) => {
      e.preventDefault();
      menu?.classList.toggle('open');
    });
    menu?.querySelectorAll('[data-range]').forEach((opt) => {
      opt.addEventListener('click', async () => {
        selectedRange = opt.getAttribute('data-range') || 'today';
        document.getElementById('phase1-date-range').textContent = DATE_LABEL[selectedRange] || 'Today';
        menu.querySelectorAll('[data-range]').forEach((x) => x.classList.remove('active'));
        opt.classList.add('active');
        menu.classList.remove('open');
        await Promise.all([loadDashboardStats(), loadRecentActivity()]);
        visibleActivityCount = 10;
        renderActivityList();
      });
    });
    document.addEventListener('click', (e) => {
      if (!e.target.closest('.date-wrap')) menu?.classList.remove('open');
    });
  }

  function wireExport() {
    document.getElementById('btnExportPhase1')?.addEventListener('click', async () => {
      const btn = document.getElementById('btnExportPhase1');
      const txt = document.getElementById('exportBtnText');
      if (!btn || !txt) return;
      btn.disabled = true;
      txt.textContent = 'Exporting...';
      try {
        const url = `${API_BASE}/receiving/dashboard/export?range=${encodeURIComponent(selectedRange)}`;
        const res = await fetch(url);
        if (!res.ok) throw new Error(`Export failed (${res.status})`);
        const blob = await res.blob();
        const fileName = getFileName(res.headers.get('content-disposition')) || `phase1_dashboard_${selectedRange}.csv`;
        const objectUrl = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = objectUrl;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(objectUrl), 1500);
      } catch (err) {
        console.error(err);
      } finally {
        txt.textContent = 'Export';
        btn.disabled = false;
      }
    });
  }

  async function loadDashboardStats() {
    try {
      const response = await fetch(`${API_BASE}/receiving/dashboard/stats`);
      const stats = response.ok ? await response.json() : {};
      updateStats({
        totalBatches: stats.totalBatches || 0,
        completedBatches: stats.completedBatches || 0,
        inProgressBatches: stats.inProgressBatches || 0,
        totalDevices: stats.totalDevices || 0,
        newStockCount: stats.newStockCount || 0,
        rnrNormalCount: stats.rnrNormalCount || 0,
        rnrEmergencyCount: stats.rnrEmergencyCount || 0
      });
    } catch (err) {
      console.error('Error loading dashboard stats:', err);
      updateStats({ totalBatches: 0, completedBatches: 0, inProgressBatches: 0, totalDevices: 0, newStockCount: 0, rnrNormalCount: 0, rnrEmergencyCount: 0 });
    }
  }

  function updateStats(stats) {
    document.getElementById('totalBatches').textContent = numberFmt(stats.totalBatches);
    document.getElementById('completedBatches').textContent = numberFmt(stats.completedBatches);
    document.getElementById('inProgressBatches').textContent = numberFmt(stats.inProgressBatches);
    document.getElementById('totalDevices').textContent = numberFmt(stats.totalDevices);
    document.getElementById('completedBatchesSub').textContent = numberFmt(stats.completedBatches);
    document.getElementById('inProgressSub').textContent = `${numberFmt(stats.inProgressBatches)} active`;
    document.getElementById('newStockCount').textContent = numberFmt(stats.newStockCount);
    document.getElementById('rnrNormalCount').textContent = numberFmt(stats.rnrNormalCount);
    document.getElementById('rnrEmergencyCount').textContent = numberFmt(stats.rnrEmergencyCount);

    const total = Math.max(1, stats.totalBatches || (stats.newStockCount + stats.rnrNormalCount + stats.rnrEmergencyCount));
    document.getElementById('newStockBar').style.width = `${(stats.newStockCount / total) * 100}%`;
    document.getElementById('rnrNormalBar').style.width = `${(stats.rnrNormalCount / total) * 100}%`;
    document.getElementById('rnrEmergencyBar').style.width = `${(stats.rnrEmergencyCount / total) * 100}%`;
  }

  async function loadRecentActivity() {
    try {
      const response = await fetch(`${API_BASE}/receiving/dashboard/recent`);
      const rows = response.ok ? await response.json() : [];
      allActivity = rows.filter((row) => inSelectedRange(row.createdAt));
    } catch (err) {
      console.error('Error loading recent activity:', err);
      allActivity = [];
    }
  }

  function renderActivityList() {
    const wrap = document.getElementById('recentActivity');
    const loadMore = document.getElementById('loadMoreActivity');
    if (!wrap || !loadMore) return;

    const visible = allActivity.slice(0, visibleActivityCount);
    if (visible.length === 0) {
      wrap.innerHTML = `<div class="text-center py-5 text-muted"><i data-lucide="inbox" style="width:40px;height:40px"></i><div class="mt-2">No recent activity yet</div></div>`;
      loadMore.style.display = 'none';
      if (window.lucide && typeof window.lucide.createIcons === 'function') window.lucide.createIcons();
      return;
    }

    wrap.innerHTML = visible.map((b) => {
      const sourceName = b.sourceTypeName || b.sourceType || '';
      const sourceTagClass = sourceName === 'New Stock' ? 'tag-new' : (sourceName === 'RnR Normal' ? 'tag-rnr' : 'tag-emergency');
      const statusClass = mapStatusClass(b.statusName || b.status);
      return `
        <div class="activity-item">
          <span class="activity-tag ${sourceTagClass}">${sourceName} ${b.documentNumber ? `- ${b.documentNumber}` : ''}</span>
          <div class="activity-meta"><strong>School/Supplier:</strong> ${escapeHtml(b.schoolName || 'N/A')}</div>
          <div class="activity-meta"><strong>Devices:</strong> ${numberFmt(b.deviceCount || 0)}</div>
          <div class="d-flex justify-content-between align-items-center mt-2">
            <span class="activity-time">${new Date(b.createdAt).toLocaleString()}</span>
            <span class="status-pill ${statusClass}">${escapeHtml(b.statusName || b.status || 'Pending')}</span>
          </div>
        </div>
      `;
    }).join('');

    loadMore.style.display = visibleActivityCount < allActivity.length ? 'inline-block' : 'none';
    loadMore.onclick = () => {
      visibleActivityCount += 10;
      renderActivityList();
    };
  }

  function mapStatusClass(statusRaw) {
    const s = String(statusRaw || '').toLowerCase();
    if (s.includes('grvissued')) return 'st-grv';
    if (s.includes('progress') || s.includes('scanning') || s.includes('verified') || s.includes('variance')) return 'st-progress';
    if (s.includes('failed') || s.includes('cancel')) return 'st-failed';
    return 'st-pending';
  }

  function inSelectedRange(iso) {
    if (selectedRange === 'all') return true;
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return false;
    const now = new Date();
    if (selectedRange === 'today') return d.toDateString() === now.toDateString();
    if (selectedRange === 'week') {
      const start = new Date(now);
      const day = (start.getDay() + 6) % 7;
      start.setDate(start.getDate() - day);
      start.setHours(0, 0, 0, 0);
      return d >= start && d <= now;
    }
    if (selectedRange === 'month') return d.getMonth() === now.getMonth() && d.getFullYear() === now.getFullYear();
    return true;
  }

  async function loadProfile() {
    try {
      const response = await fetch('/api/auth/current-user');
      if (!response.ok) return;
      const user = await response.json();
      document.getElementById('profileRoleSub').textContent = user.role || 'ReceivingClerk';
    } catch (error) {
      console.error('Error loading profile:', error);
    }
  }

  function getFileName(disposition) {
    if (!disposition) return null;
    const match = /filename="?([^"]+)"?/.exec(disposition);
    return match ? match[1] : null;
  }
  function numberFmt(v) { return Number(v || 0).toLocaleString(); }
  function escapeHtml(v) { return String(v ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;'); }
})();
