
import { useEffect, useState } from 'react'
import './App.css'

interface SystemStatus {
  name: string
  version: string
  status: string
  description: string
}

function App() {
  const [system, setSystem] = useState<SystemStatus | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function loadSystemStatus() {
      try {
        const response = await fetch('/api/system/status', {
          signal: controller.signal,
        })

        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`)
        }

        const data: SystemStatus = await response.json()
        setSystem(data)
      } catch (err) {
        if (controller.signal.aborted) return
        setError('Backend konnte nicht erreicht werden.')
        console.error(err)
      }
    }

    loadSystemStatus()

    return () => controller.abort()
  }, [])

  return (
    <main className="dashboard">
      <header className="header">
        <h1>HELIOS</h1>
        <span>Industrial Digital Twin Platform</span>
      </header>

      <section className="status-card">
        <h2>System Overview</h2>

        {error ? (
          <p className="error">{error}</p>
        ) : system ? (
          <>
            <p>
              System Status:
              <strong className="online"> {system.status}</strong>
            </p>
            <p>Version: {system.version}</p>
            <p>{system.description}</p>
          </>
        ) : (
          <p>Connecting to backend...</p>
        )}
      </section>
    </main>
  )
}

export default App
