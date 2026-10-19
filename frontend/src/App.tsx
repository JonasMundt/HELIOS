
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
  const [busyMachineId, setBusyMachineId] = useState<string | null>(null)

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

        setSystem(await systemResponse.json())
        setMachines(await machinesResponse.json())
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

  async function controlMachine(id: string, action: 'start' | 'stop') {
    setBusyMachineId(id)
    setError(null)

    try {
      const response = await fetch(`/api/machines/${id}/${action}`, {
        method: 'POST',
      })

      if (!response.ok) {
        throw new Error(`Machine control failed: ${response.status}`)
      }

      const machinesResponse = await fetch('/api/machines')

      if (!machinesResponse.ok) {
        throw new Error('Failed to refresh machines')
      }

      const updatedMachines: Machine[] = await machinesResponse.json()
      setMachines(updatedMachines)
    } catch (err) {
      setError('Machine control failed. Please try again.')
      console.error(err)
    } finally {
      setBusyMachineId(null)
    }
  }

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
          <p>System status unavailable.</p>
        )}
      </section>

      {error && <p className="error" role="alert">{error}</p>}

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

              <div className="machine-actions">
                <button
                  className="start-button"
                  disabled={busyMachineId !== null || machine.status === 2}
                  onClick={() => controlMachine(machine.id, 'start')}
                >
                  Start
                </button>

                <button
                  className="stop-button"
                  disabled={busyMachineId !== null || machine.status === 0}
                  onClick={() => controlMachine(machine.id, 'stop')}
                >
                  Stop
                </button>
              </div>
            </article>
          ))}
        </div>
      </section>
    </main>
  )
}

export default App
