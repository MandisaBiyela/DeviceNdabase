// Served under /app from the same ASP.NET host as /api — use relative URLs so cookies work.
const apiBase = ''

export async function apiGet(path) {
  const response = await fetch(`${apiBase}${path}`, {
    method: 'GET',
    credentials: 'include',
    headers: {
      Accept: 'application/json',
    },
  })

  const contentType = response.headers.get('content-type') || ''
  const payload = contentType.includes('application/json')
    ? await response.json()
    : await response.text()

  if (!response.ok) {
    const message =
      typeof payload === 'object' && payload?.error
        ? payload.error
        : `Request failed with ${response.status}`
    throw new Error(message)
  }

  return payload
}
