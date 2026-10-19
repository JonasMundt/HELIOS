
import { useEffect, useState } from 'react'
import './App.css'

interface SystemStatus {
  name: string
  version: string
  status: string
  description: string
}

interface Machine {
  id: string
  name: string
  type: number
  status: number
  createdAt: string
}

const machineTypes = [
  'Conveyor Belt',
  'Scanner Station',
  'Hydraulic Press',
  'Robot Arm',
]

const machineStatuses = [
  'OFFLINE',
  'IDLE',
  'RUNNING',
  'WARNING',
  'ERROR',
]

function App() {
  const [system, setSystem] = useState<SystemStatus | null>(null)
  const [machines, setMachines] = useState<Machine[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function loadDashboard() {
      try {
        const [systemResponse, machinesResponse] = await Promise.all([
          fetch('/api/system/status', { signal: controller.signal }),
          fetch('/api/machines', { signal: controller.signal }),
        ])

        if (!systemResponse.ok || !machinesResponse.ok) {
          throw new Error('Failed to load dashboard data')
        }

        const systemData: SystemStatus = await systemResponse.json()
        const machinesData: Machine[] = await machinesResponse.json()

        setSystem(systemData)
        setMachines(machinesData)
      } catch (err) {
        if (controller.signal.aborted) return
        setError('Backend could not be reached.')
        console.error(err)
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    loadDashboard()

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

        {loading ? (
          <p>Connecting to backend...</p>
        ) : error ? (
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
        ) : null}
      </section>

      <section className="machines-section">
        <h2>Machines</h2>

        <div className="machine-grid">
          {machines.map((machine) => (
            <article className="machine-card" key={machine.id}>
              <h3>{machine.name}</h3>
              <p>Type: {machineTypes[machine.type] ?? 'Unknown'}</p>
              <p>
                Status:{' '}
                <span className={`machine-status status-${machine.status}`}>
                  {machineStatuses[machine.status] ?? 'UNKNOWN'}
                </span>
              </p>
              <small>ID: {machine.id.slice(0, 8)}...</small>
            </article>
          ))}
        </div>
      </section>
    </main>
  )
}

export default App
