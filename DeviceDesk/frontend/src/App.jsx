import './App.css'
import { useState } from 'react'
import { Link, Route, Routes } from 'react-router-dom'
import HomePage from './pages/HomePage'
import CurrentUserPage from './pages/CurrentUserPage'
import DbMigrationsPage from './pages/DbMigrationsPage'

function App() {
  const [menuOpen, setMenuOpen] = useState(false)

  const closeMenu = () => setMenuOpen(false)

  return (
    <main className="app-shell">
      <header className="topbar">
        <Link className="brand" to="/" onClick={closeMenu}>
          DeviceDesk
        </Link>

        <button
          className="menu-toggle"
          type="button"
          onClick={() => setMenuOpen((v) => !v)}
          aria-label="Toggle menu"
          aria-expanded={menuOpen}
        >
          <span />
          <span />
          <span />
        </button>

        <nav className={`topnav ${menuOpen ? 'open' : ''}`}>
          <Link to="/" onClick={closeMenu}>
            Home
          </Link>
          <Link to="/current-user" onClick={closeMenu}>
            Current User
          </Link>
          <Link to="/db-migrations" onClick={closeMenu}>
            DB Migrations
          </Link>
          <a href="/dev/swagger" onClick={closeMenu}>
            Swagger
          </a>
        </nav>
      </header>

      <div className="app-content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/current-user" element={<CurrentUserPage />} />
          <Route path="/db-migrations" element={<DbMigrationsPage />} />
        </Routes>
      </div>
    </main>
  )
}

export default App
