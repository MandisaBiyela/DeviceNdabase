(function(){
  const sameOrigin = `${location.origin}/api/phase0`;
  const fallback   = `http://localhost:5170/api/phase0`;
  // Use same-origin for any hosted app port (e.g., 5170, 5171). When serving static HTML
  // previews (e.g., 8213/8211/5501/8000), fall back to the API on 5170.
  const staticPreviewPorts = new Set(['8213','8211','5501','8000']);
  const apiBase    = staticPreviewPorts.has(location.port) ? fallback : sameOrigin;

  window.PHASE0 = {
    API_BASE: apiBase,
    async fetchBlob(url, opts = {}) {
      const res = await fetch(url, { credentials: "include", ...opts });
      const ct = res.headers.get("content-type") || "";
      if (!res.ok) {
        let msg = res.statusText;
        if (ct.includes("application/json")) {
          try {
            const data = await res.json();
            if (data.errors && Array.isArray(data.errors))
              msg = data.errors.map((e) => e.message || e.field || "").filter(Boolean).join("; ") || msg;
            else msg = data.details || data.message || data.error || msg;
          } catch {
            try {
              msg = await res.text();
            } catch {}
          }
        }
        throw new Error(msg || res.statusText);
      }
      return res.blob();
    },
    rowsOrEmpty(data){ return (data && Array.isArray(data.rows)) ? data.rows : []; },
    toast(msg){ const el = document.getElementById('alert'); if(!el) return; el.innerHTML = `<div class="alert alert-info">${msg}</div>`; },
    q(sel){ return document.querySelector(sel); },
    v(sel){ const el = document.querySelector(sel); return el ? el.value.trim() : ''; },
    byId(id){ return document.getElementById(id); },
    show(elOrId, visible = true){ const el = typeof elOrId === 'string' ? document.getElementById(elOrId) : elOrId; if(!el) return; el.style.display = visible ? 'block' : 'none'; },
    localDateTime(iso){ if(!iso) return ''; const d = new Date(iso); if(Number.isNaN(d.getTime())) return ''; const pad = n => `${n}`.padStart(2,'0'); return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`; }
  };
})();