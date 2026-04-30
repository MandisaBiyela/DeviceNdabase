(() => {
  const { API_BASE, fetchJson, byId } = PHASE0;

  const pageCfg = {
    phase: (document.body?.dataset?.phase || 'NEW').trim().toUpperCase(),
    apiPath: (document.body?.dataset?.apiPath || 'new/batches').trim(),
    viewPath: (document.body?.dataset?.viewPath || '/phase0/new-batch.html').trim(),
    backRoute: (document.body?.dataset?.backRoute || '/phase0/new.html').trim(),
    title: document.body?.dataset?.title || 'New Stock Intake Batches',
    backLabel: document.body?.dataset?.backLabel || 'Back to New Intake',
  };

  const rowsEl = byId('rows');
  const refreshBtn = byId('refreshBtn');
  const refreshIcon = byId('refreshIcon');
  const backLink = byId('backLink');
  const pageTitle = byId('pageTitle');
  const lastUpdatedEl = byId('lastUpdated');

  const profileInitials = byId('profileInitials');
  const profileName = byId('profileName');
  const profileRole = byId('profileRole');
  const profileNameSkeleton = byId('profileNameSkeleton');
  const sidebarProfile = byId('sidebarProfile');

  let confirmDeleteId = null;
  let lastUpdatedAt = null;
  let lastRows = [];
  let autoRefreshTimer = null;
  let secondsTicker = null;

  function escapeHtml(str) {
    return String(str ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
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

  function setLastUpdatedNow() {
    lastUpdatedAt = Date.now();
    updateLastUpdatedText();
  }

  function updateLastUpdatedText() {
    if (!lastUpdatedEl) return;
    if (!lastUpdatedAt) {
      lastUpdatedEl.textContent = 'Last updated —';
      return;
    }
    const sec = Math.max(0, Math.floor((Date.now() - lastUpdatedAt) / 1000));
    lastUpdatedEl.textContent = `Last updated ${sec}s ago`;
  }

  function statusBadge(status) {
    if (!status) return '';
    const key = String(status).toLowerCase();
    const cls =
      key === 'processing'
        ? 'sr-status-processing'
        : key === 'complete'
          ? 'sr-status-complete'
          : key === 'failed'
            ? 'sr-status-failed'
            : 'sr-status-pending';
    return `<span class="sr-status-badge ${cls}">${escapeHtml(String(status))}</span>`;
  }

  function fileCellIcon(fileName) {
    const isManual = !fileName || String(fileName).toLowerCase().includes('manual');
    return isManual
      ? `<i data-lucide="pen-line" class="sr-file-icon"></i>`
      : `<i data-lucide="file-text" class="sr-file-icon"></i>`;
  }

  function renderEmptyState() {
    rowsEl.innerHTML = `
      <tr>
        <td colspan="4" class="sr-empty-cell">
          <div class="sr-empty-state">
            <i data-lucide="inbox" class="sr-empty-icon"></i>
            <div class="sr-empty-title">No batches yet</div>
            <div class="sr-empty-subtitle">Upload a CSV or add items manually to create your first batch</div>
            <a id="emptyCtaBtn" class="sr-btn sr-btn-outline-blue mt-2" href="${escapeHtml(pageCfg.backRoute)}">Go to Upload →</a>
          </div>
        </td>
      </tr>
    `;
    try {
      if (window.lucide && typeof window.lucide.createIcons === 'function') window.lucide.createIcons();
    } catch {}
  }

  function renderRows(rows) {
    if (!rows || rows.length === 0) {
      renderEmptyState();
      return;
    }

    rowsEl.innerHTML = rows
      .map((r) => {
        const id = r.id || r.batchId || '';
        const created = fmtDate(r.createdAt);
        const sourceFile = r.sourceFileName || r.sourceFile || 'Manual Entry';
        const items = Number(r.items || 0);
        const status = r.status || null;
        const isConfirm = confirmDeleteId && confirmDeleteId === id;

        const actions = isConfirm
          ? `<span class="sr-confirm-text">Are you sure?</span> <button type="button" class="sr-action-link sr-yes" data-act="confirm-delete" data-id="${escapeHtml(
              id
            )}">Yes</button> <button type="button" class="sr-action-link" data-act="cancel-delete">No</button>`
          : `<a class="sr-action-link" href="${escapeHtml(pageCfg.viewPath)}?batchId=${encodeURIComponent(
              id
            )}">View</a><button type="button" class="sr-action-link sr-delete ms-3" data-act="ask-delete" data-id="${escapeHtml(
              id
            )}">Delete</button>`;

        return `
          <tr class="sr-batch-row">
            <td class="sr-created-cell">${escapeHtml(created)}</td>
            <td>
              <div class="sr-file-wrap">
                ${fileCellIcon(sourceFile)}
                <span class="sr-file-name" title="${escapeHtml(sourceFile)}">${escapeHtml(sourceFile)}</span>
                ${status ? statusBadge(status) : ''}
              </div>
            </td>
            <td class="text-center"><span class="sr-items-pill">${items.toLocaleString('en-US')}</span></td>
            <td class="sr-actions-cell">${actions}</td>
          </tr>
        `;
      })
      .join('');

    rowsEl.querySelectorAll('[data-act="ask-delete"]').forEach((btn) => {
      btn.addEventListener('click', () => {
        confirmDeleteId = btn.getAttribute('data-id');
        renderRows(lastRows);
      });
    });
    rowsEl.querySelectorAll('[data-act="cancel-delete"]').forEach((btn) => {
      btn.addEventListener('click', () => {
        confirmDeleteId = null;
        renderRows(lastRows);
      });
    });
    rowsEl.querySelectorAll('[data-act="confirm-delete"]').forEach((btn) => {
      btn.addEventListener('click', async () => {
        const id = btn.getAttribute('data-id');
        confirmDeleteId = null;
        if (!id) return;
        try {
          await fetchJson(`${API_BASE}/${pageCfg.apiPath}/${encodeURIComponent(id)}`, { method: 'DELETE' });
          await loadBatches(false);
        } catch {
          // Endpoint may not exist yet; keep UX non-blocking and refresh list.
          await loadBatches(false);
        }
      });
    });

    try {
      if (window.lucide && typeof window.lucide.createIcons === 'function') window.lucide.createIcons();
    } catch {}
  }

  async function loadBatches(showSpin = false) {
    if (showSpin) {
      refreshIcon?.classList.add('spin-once');
      setTimeout(() => refreshIcon?.classList.remove('spin-once'), 600);
    }
    try {
      const data = await fetchJson(`${API_BASE}/${pageCfg.apiPath}?page=1&pageSize=100`);
      lastRows = (data && data.rows) || [];
      renderRows(lastRows);
      setLastUpdatedNow();
    } catch {
      renderRows([]);
      setLastUpdatedNow();
    }
  }

  function applyStaticPageText() {
    if (pageTitle) pageTitle.textContent = pageCfg.title;
    if (backLink) {
      backLink.innerHTML = `<i data-lucide="chevron-left" style="width:14px;height:14px;"></i>${escapeHtml(
        pageCfg.backLabel
      )}`;
      backLink.setAttribute('href', pageCfg.backRoute);
    }
  }

  async function loadProfile() {
    try {
      // Keep layout stable while loading
      profileNameSkeleton?.classList.remove('d-none');
      if (profileName) profileName.style.display = 'none';

      const response = await fetch('/api/auth/current-user');
      if (!response.ok) return;
      const user = await response.json();
      const initials = user.fullName
        ? user.fullName
            .split(' ')
            .map((n) => n[0])
            .join('')
            .substring(0, 2)
            .toUpperCase()
        : 'U';

      if (profileInitials) profileInitials.textContent = initials;
      if (profileName) {
        profileName.textContent = user.fullName || 'User';
        profileName.style.display = '';
      }
      if (profileRole) profileRole.textContent = user.role || 'User';
    } catch {
      // Keep fallback state
    } finally {
      profileNameSkeleton?.classList.add('d-none');
      sidebarProfile?.classList.remove('sr-profile-loading');
    }
  }

  function startTimers() {
    autoRefreshTimer = window.setInterval(() => loadBatches(false), 30000);
    secondsTicker = window.setInterval(updateLastUpdatedText, 1000);
  }

  function init() {
    applyStaticPageText();
    try {
      if (window.lucide && typeof window.lucide.createIcons === 'function') window.lucide.createIcons();
    } catch {}
    refreshBtn?.addEventListener('click', () => loadBatches(true));
    loadProfile();
    loadBatches(false);
    startTimers();
  }

  init();
})();

