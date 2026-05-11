import { useEffect, useState } from 'react'
import { apiGet } from '../api/client'

export default function DbMigrationsPage() {
  const [state, setState] = useState({
    loading: true,
    error: '',
    data: null,
  })

  useEffect(() => {
    let mounted = true

    async function load() {
      try {
        const data = await apiGet('/api/db/migrations')
        if (mounted) setState({ loading: false, error: '', data })
      } catch (error) {
        if (mounted) {
          setState({
            loading: false,
            error: error.message || 'Failed to load migrations.',
            data: null,
          })
        }
      }
    }

    load()
    return () => {
      mounted = false
    }
  }, [])

  if (state.loading) return <p>Loading database migrations...</p>
  if (state.error) return <p className="error">Error: {state.error}</p>

  return (
    <section>
      <h1>Database Migrations</h1>
      <p>Response from <code>/api/db/migrations</code>:</p>
      <pre>{JSON.stringify(state.data, null, 2)}</pre>
    </section>
  )
}
