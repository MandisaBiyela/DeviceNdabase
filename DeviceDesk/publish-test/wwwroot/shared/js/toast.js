// Simple global toast utility without external dependencies.
// Usage: toast('Message', 'success' | 'danger' | 'warning' | 'info')
(function () {
  function ensureContainer() {
    let c = document.getElementById('dd-toast-container');
    if (!c) {
      c = document.createElement('div');
      c.id = 'dd-toast-container';
      c.style.position = 'fixed';
      c.style.top = '16px';
      c.style.right = '16px';
      c.style.zIndex = '1060'; // above modals
      c.style.display = 'flex';
      c.style.flexDirection = 'column';
      c.style.gap = '8px';
      document.body.appendChild(c);
    }
    return c;
  }

  function getClasses(type) {
    const base = 'alert alert-dismissible fade show';
    switch ((type || 'info').toLowerCase()) {
      case 'success': return base + ' alert-success';
      case 'danger':
      case 'error': return base + ' alert-danger';
      case 'warning': return base + ' alert-warning';
      default: return base + ' alert-info';
    }
  }

  function toast(message, type, options) {
    try {
      const container = ensureContainer();
      const div = document.createElement('div');
      div.className = getClasses(type);
      div.style.minWidth = '280px';
      div.style.boxShadow = '0 2px 16px rgba(0,0,0,0.12)';
      div.innerHTML = `
        <div>${message || ''}</div>
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
      `;
      container.appendChild(div);
      const ttl = (options && options.ttl) || 3500;
      setTimeout(() => {
        try { div.remove(); } catch {}
      }, ttl);
      return div;
    } catch (e) {
      console.warn('toast fallback', e);
      alert(message);
    }
  }

  window.toast = toast;
})();