// Lightweight JSON fetch helper that avoids HTML redirects and surfaces errors clearly
// Usage: getJson(url, { method, headers, credentials, body })
// - Same origin: credentials defaults to 'same-origin'
// - Cross-origin: pass credentials: 'include' and ensure CORS allows it

async function getJson(url, opts = {}) {
  const target = new URL(url, location.origin);
  const sameOrigin = target.origin === location.origin;
  const res = await fetch(url, {
    headers: { Accept: 'application/json', ...(opts.headers || {}) },
    credentials: opts.credentials ?? (sameOrigin ? 'same-origin' : 'include'),
    ...opts,
  });

  const ct = res.headers.get('content-type') || '';
  if (!ct.includes('application/json')) {
    const text = await res.text();
    throw new Error(`Expected JSON but got ${res.status} at ${res.url}\n${text.slice(0, 300)}`);
  }

  if (!res.ok) {
    let msg = `HTTP ${res.status}`;
    try {
      const data = await res.json();
      msg += data?.error ? `: ${data.error}` : '';
    } catch (_) {}
    throw new Error(msg);
  }

  return res.json();
}

window.getJson = getJson;