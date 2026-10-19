
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

interface SimulationRun {
  id: string
  startTime: string | null
  endTime: string | null
  status: number
  producedItems: number
  currentStage: number
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

const simulationStatuses = [
  'PENDING',
  'RUNNING',
  'COMPLETED',
  'FAILED',
  'CANCELLED',
]

const productionStages = [
  'Waiting',
  'Conveyor Belt',
  'Scanning',
  'Hydraulic Press',
  'Robot Arm',
  'Finished',
]

function App() {
  const [system, setSystem] = useState<SystemStatus | null>(null)
  const [machines, setMachines] = useState<Machine[]>([])
  const [simulations, setSimulations] = useState<SimulationRun[]>([])

  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [simulationsLoading, setSimulationsLoading] = useState(true)
  const [busyMachineId, setBusyMachineId] = useState<string | null>(null)
  const [simulationBusy, setSimulationBusy] = useState(false)

  // Initial dashboard loading
  useEffect(() => {
    const controller = new AbortController()

    async function loadDashboard() {
      try {
        const [
          systemResponse,
          machinesResponse,
          simulationsResponse,
        ] = await Promise.all([
          fetch('/api/system/status', {
            signal: controller.signal,
          }),
          fetch('/api/machines', {
            signal: controller.signal,
          }),
          fetch('/api/simulations', {
            signal: controller.signal,
          }),
        ])

        if (
          !systemResponse.ok ||
          !machinesResponse.ok ||
          !simulationsResponse.ok
        ) {
          throw new Error('Failed to load dashboard data')
        }

        const [systemData, machinesData, simulationsData] =
          await Promise.all([
            systemResponse.json(),
            machinesResponse.json(),
            simulationsResponse.json(),
          ])

        if (controller.signal.aborted) return

        setSystem(systemData)
        setMachines(machinesData)
        setSimulations(simulationsData)
        setError(null)
      } catch (err) {
        if (controller.signal.aborted) return

        setError('Backend could not be reached.')
        console.error(err)
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
          setSimulationsLoading(false)
        }
      }
    }

    loadDashboard()

    return () => controller.abort()
  }, [])

  // Refresh production simulations every two seconds
  useEffect(() => {
    const controller = new AbortController()

    async function refreshSimulations() {
      try {
        const response = await fetch('/api/simulations', {
          signal: controller.signal,
        })

        if (!response.ok) {
          throw new Error('Failed to refresh simulations')
        }

        const updatedSimulations: SimulationRun[] =
          await response.json()

        if (!controller.signal.aborted) {
          setSimulations(updatedSimulations)
        }
      } catch (err) {
        if (!controller.signal.aborted) {
          console.error('Simulation refresh failed:', err)
        }
      }
    }

    const interval = window.setInterval(
      refreshSimulations,
      2000,
    )

    return () => {
      controller.abort()
      window.clearInterval(interval)
    }
  }, [])

  // Machine control
  async function controlMachine(
    id: string,
    action: 'start' | 'stop',
  ) {
    setBusyMachineId(id)
    setError(null)

    try {
      const response = await fetch(
        `/api/machines/${id}/${action}`,
        {
          method: 'POST',
        },
      )

      if (!response.ok) {
        throw new Error(
          `Machine control failed: ${response.status}`,
        )
      }

      const machinesResponse = await fetch('/api/machines')

      if (!machinesResponse.ok) {
        throw new Error('Failed to refresh machines')
      }

      const updatedMachines: Machine[] =
        await machinesResponse.json()

      setMachines(updatedMachines)
    } catch (err) {
      setError('Machine control failed. Please try again.')
      console.error(err)
    } finally {
      setBusyMachineId(null)
    }
  }

  // Start automatic production simulation
  async function startSimulation() {
    setSimulationBusy(true)
    setError(null)

    try {
      const response = await fetch('/api/simulations', {
        method: 'POST',
      })

      if (!response.ok) {
        throw new Error(
          `Simulation start failed: ${response.status}`,
        )
      }

      const simulationsResponse = await fetch(
        '/api/simulations',
      )

      if (!simulationsResponse.ok) {
        throw new Error('Failed to refresh simulations')
      }

      const updatedSimulations: SimulationRun[] =
        await simulationsResponse.json()

      setSimulations(updatedSimulations)
    } catch (err) {
      setError('Could not start simulation.')
      console.error(err)
    } finally {
      setSimulationBusy(false)
    }
  }
  
  async function stopSimulation(id: string) {
    setSimulationBusy(true)
    setError(null)

    try {
      const response = await fetch(`/api/simulations/${id}/stop`, {
        method: 'POST',
      })

      if (!response.ok) {
        throw new Error(`Simulation stop failed: ${response.status}`)
      }

      const simulationsResponse = await fetch('/api/simulations')

      if (!simulationsResponse.ok) {
        throw new Error('Failed to refresh simulations')
      }

      const updatedSimulations: SimulationRun[] =
        await simulationsResponse.json()

      setSimulations(updatedSimulations)
    } catch (err) {
      setError('Could not stop simulation.')
      console.error(err)
    } finally {
      setSimulationBusy(false)
    }
  }


  const simulationRunning = simulations.some(
    (simulation) => simulation.status === 1,
  )

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
              <strong className="online">
                {' '}
                {system.status}
              </strong>
            </p>

            <p>Version: {system.version}</p>
            <p>{system.description}</p>
          </>
        ) : (
          <p>System status unavailable.</p>
        )}
      </section>

      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      <section className="machines-section">
        <h2>Machines</h2>

        <div className="machine-grid">
          {machines.map((machine) => (
            <article
              className="machine-card"
              key={machine.id}
            >
              <h3>{machine.name}</h3>

              <p>
                Type:{' '}
                {machineTypes[machine.type] ?? 'Unknown'}
              </p>

              <p>
                Status:{' '}
                <span
                  className={`machine-status status-${machine.status}`}
                >
                  {machineStatuses[machine.status] ??
                    'UNKNOWN'}
                </span>
              </p>

              <small>
                ID: {machine.id.slice(0, 8)}...
              </small>

              <div className="machine-actions">
                <button
                  className="start-button"
                  disabled={
                    busyMachineId !== null ||
                    machine.status === 2
                  }
                  onClick={() =>
                    controlMachine(machine.id, 'start')
                  }
                >
                  Start
                </button>

                <button
                  className="stop-button"
                  disabled={
                    busyMachineId !== null ||
                    machine.status === 0
                  }
                  onClick={() =>
                    controlMachine(machine.id, 'stop')
                  }
                >
                  Stop
                </button>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section className="simulations-section">
        <h2>Production Simulations</h2>

        <button
          className="start-button"
          disabled={simulationBusy || simulationRunning}
          onClick={startSimulation}
        >
          {simulationBusy
            ? 'Starting...'
            : 'Start Simulation'}
        </button>

        {simulationsLoading ? (
          <p>Loading simulations...</p>
        ) : simulations.length === 0 ? (
          <p>No production simulations available.</p>
        ) : (
          <div className="simulation-grid">
            {simulations.map((simulation) => (
              <article
                className="simulation-card"
                key={simulation.id}
              >
                <h3>
                  Simulation {simulation.id.slice(0, 8)}
                </h3>

                <p>
                  Status:{' '}
                  <strong className={`simulation-status simulation-status-${simulation.status}`}>
                    {simulationStatuses[simulation.status] ?? 'UNKNOWN'}
                  </strong>
                </p>

                <p>
                  Current Stage:{' '}
                  {productionStages[
                    simulation.currentStage
                  ] ?? 'Unknown'}
                </p>
                
                <div className="simulation-progress">
                  <div className="simulation-progress-info">
                    <span>Production Progress</span>
                    <span>{Math.round((simulation.currentStage / 5) * 100)}%</span>
                  </div>

                  <div
                    className="simulation-progress-track"
                    role="progressbar"
                    aria-label="Production progress"
                    aria-valuemin={0}
                    aria-valuemax={100}
                    aria-valuenow={Math.round((simulation.currentStage / 5) * 100)}
                  >
                    <div
                      className={`simulation-progress-fill ${
                        simulation.status === 4 ? 'cancelled' : ''
                      }`}
                      style={{
                        width: `${Math.round((simulation.currentStage / 5) * 100)}%`,
                      }}
                    />
                  </div>
                </div>


                
                <div className="simulation-progress">
                  <div className="simulation-progress-info">
                    <span>Production Progress</span>
                    <span>{Math.round((simulation.currentStage / 5) * 100)}%</span>
                  </div>

                  <div
                    className="simulation-progress-track"
                    role="progressbar"
                    aria-label="Production progress"
                    aria-valuemin={0}
                    aria-valuemax={100}
                    aria-valuenow={Math.round((simulation.currentStage / 5) * 100)}
                  >
                    <div
                      className={`simulation-progress-fill ${
                        simulation.status === 4 ? 'cancelled' : ''
                      }`}
                      style={{
                        width: `${Math.round((simulation.currentStage / 5) * 100)}%`,
                      }}
                    />
                  </div>
                </div>


                <p>
                  Produced Items:{' '}
                  {simulation.producedItems}
                </p>
                
                  {simulation.status === 1 && (
                    <div className="simulation-actions">
                      <button
                        className="stop-button"
                        disabled={simulationBusy}
                        onClick={() => stopSimulation(simulation.id)}
                      >
                        {simulationBusy ? 'Stopping...' : 'Stop Simulation'}
                      </button>
                    </div>
                  )}


                <small>
                  Started:{' '}
                  {simulation.startTime
                    ? new Date(
                        simulation.startTime,
                      ).toLocaleString()
                    : 'Not started'}
                </small>
              </article>
            ))}
          </div>
        )}
      </section>
    </main>
  )
}

export default App
