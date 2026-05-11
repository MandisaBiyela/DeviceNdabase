import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiGet } from '../api/client'

const moduleLinks = [
  {
    label: 'Phase 0',
    href: '/phase0/new.html',
    tooltip: 'School Readiness & Intake',
  },
  { label: 'Phase 1', href: '/phase1/dashboard.html', tooltip: 'Main Receiving' },
  { label: 'Phase 2', href: '/phase2/index.html', tooltip: 'ICT Center' },
  {
    label: 'Dispatch',
    href: '/dispatch/index.html',
    tooltip: 'Dispatch Management',
  },
  { label: 'Admin', href: '/admin/index.html', tooltip: 'Admin Panel' },
  {
    label: 'SuperAdmin',
    href: '/superadmin/dashboard.html',
    tooltip: 'Super Admin Panel',
  },
]

export default function HomePage() {
  const [statusLoading, setStatusLoading] = useState(true)
  const [statusMap, setStatusMap] = useState({})

  useEffect(() => {
    let mounted = true

    const checks = [
      { key: 'phase0', label: 'Phase 0 API', path: '/api/phase0/readiness' },
      { key: 'phase1', label: 'Phase 1 API', path: '/api/phase1/receiving' },
      { key: 'phase2', label: 'Phase 2 API', path: '/api/phase2/receipting' },
      { key: 'database', label: 'Database', path: '/api/db/migrations' },
      { key: 'auth', label: 'Auth Service', path: '/api/auth/current-user' },
    ]

    async function loadStatus() {
      const next = {}
      await Promise.all(
        checks.map(async (check) => {
          try {
            await apiGet(check.path)
            next[check.key] = { ok: true, label: check.label }
          } catch {
            next[check.key] = { ok: false, label: check.label }
          }
        }),
      )

      if (mounted) {
        setStatusMap(next)
        setStatusLoading(false)
      }
    }

    loadStatus()
    return () => {
      mounted = false
    }
  }, [])

  const statusItems = useMemo(
    () => [
      { key: 'phase0', fallback: 'Operational' },
      { key: 'phase1', fallback: 'Operational' },
      { key: 'phase2', fallback: 'Operational' },
      { key: 'database', fallback: 'Connected' },
      { key: 'auth', fallback: 'Online' },
    ],
    [],
  )

  return (
    <section className="dashboard">
      <div className="hero">
        <p className="eyebrow">OPERATIONS DASHBOARD</p>
        <h1>DeviceDesk Frontend</h1>
        <p className="subtitle">
          Central access to backend modules and API diagnostics.
        </p>
      </div>

      <div className="card-grid">
        <article className="card">
          <h2>Backend Modules</h2>
          <p>Open current production module pages.</p>
          <div className="actions">
            {moduleLinks.map((item) => (
              <span
                className={`button-wrap ${item.label === 'SuperAdmin' ? 'full-row' : ''}`}
                key={item.href}
              >
                <a href={item.href}>{item.label}</a>
                <span className="tooltip">{item.tooltip}</span>
              </span>
            ))}
          </div>
        </article>

        <article className="card">
          <h2>API Tools</h2>
          <p>Use connected React pages for backend checks.</p>
          <div className="actions">
            <Link to="/current-user">Current User API</Link>
            <Link to="/db-migrations">DB Migrations API</Link>
          </div>
        </article>
      </div>

      <section className="status-section">
        <p className="status-title">System Status</p>
        <div className="status-row">
          {statusLoading
            ? statusItems.map((item) => (
                <span className="status-pill skeleton" key={item.key}>
                  <span className="dot loading" />
                  <span className="status-label">Loading...</span>
                </span>
              ))
            : statusItems.map((item) => {
                const entry = statusMap[item.key]
                const ok = Boolean(entry?.ok)
                return (
                  <span className="status-pill" key={item.key}>
                    <span className={`dot ${ok ? 'ok' : 'bad'}`} />
                    <span className="status-label">{entry?.label || item.key}</span>
                    <span className={`status-value ${ok ? 'ok' : 'bad'}`}>
                      {ok ? item.fallback : 'Degraded'}
                    </span>
                  </span>
                )
              })}
        </div>
      </section>

      <footer className="quick-links">
        <a href="/dev/swagger">Swagger Docs</a>
        <Link to="/db-migrations">DB Migrations</Link>
        <Link to="/current-user">Current User</Link>
        <a href="https://github.com" target="_blank" rel="noreferrer">
          GitHub
        </a>
      </footer>
    </section>
  )
}
