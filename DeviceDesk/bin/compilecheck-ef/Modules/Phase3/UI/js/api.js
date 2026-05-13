// Lightweight Phase 3 API helper (mirrors Phase 2)
// Ensures authenticated requests (cookies) and consistent JSON handling

window.api = {
  async get(url) {
    const res = await fetch(url, {
      method: 'GET',
      credentials: 'include',
      headers: { 'Accept': 'application/json' }
    });
    return this._handle(res);
  },

  async upload(url, formData) {
    const res = await fetch(url, {
      method: 'POST',
      credentials: 'include',
      body: formData
    });
    return this._handle(res);
  },

  async post(url, data) {
    const res = await fetch(url, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
      body: JSON.stringify(data)
    });
    return this._handle(res);
  },

  async _handle(res) {
    let body;
    const ct = res.headers.get('content-type') || '';
    if (ct.includes('application/json')) {
      body = await res.json();
    } else {
      body = await res.text();
    }
    if (!res.ok) {
      const msg = (body && body.message) ? body.message : `HTTP ${res.status}`;
      throw new Error(msg);
    }
    return body;
  }
};