
import { useEffect, useState } from 'react'
import './App.css'
import {
  signalRConnection,
  startSignalRConnection,
  stopSignalRConnection,
  subscribeToReconnect,
} from './services/signalRService'
import LoginScreen from './components/LoginScreen'
import {
  AuthRequestError,
  authenticatedFetch,
  getSession,
  logout,
  type AuthSession,
} from './services/authService'
import FactoryScene from './components/FactoryScene'

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
  const [session, setSession] = useState<AuthSession | null>(
    () => getSession(),
  )

  function handleLogout() {
    logout()
    setSession(null)
  }


  // --------------------------------------------------
  // AUTOMATIC SESSION EXPIRATION
  // --------------------------------------------------

  useEffect(() => {
    if (!session) return

    const expiresAt = Date.parse(session.expiresAtUtc)

    function checkSessionExpiration() {
      if (!getSession()) {
        logout()
        setSession(null)
      }
    }

    const remainingTime = Number.isFinite(expiresAt)
      ? Math.max(0, expiresAt - Date.now())
      : 0

    const timeoutId = window.setTimeout(
      checkSessionExpiration,
      remainingTime,
    )

    // Recheck when returning to an inactive browser tab.
    function handleVisibilityChange() {
      if (document.visibilityState === 'visible') {
        checkSessionExpiration()
      }
    }

    document.addEventListener(
      'visibilitychange',
      handleVisibilityChange,
    )

    return () => {
      window.clearTimeout(timeoutId)

      document.removeEventListener(
        'visibilitychange',
        handleVisibilityChange,
      )
    }
  }, [session])



  if (!session) {
    return <LoginScreen onLogin={setSession} />
  }

  return (
    <Dashboard
      session={session}
      onLogout={handleLogout}
    />
  )
}

interface DashboardProps {
  session: AuthSession
  onLogout: () => void
}

function Dashboard({ session, onLogout }: DashboardProps) {
  const [system, setSystem] = useState<SystemStatus | null>(null)
  const [machines, setMachines] = useState<Machine[]>([])
  const [simulations, setSimulations] = useState<SimulationRun[]>([])

  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [simulationsLoading, setSimulationsLoading] = useState(true)
  const [busyMachineId, setBusyMachineId] = useState<string | null>(null)
  const [simulationBusy, setSimulationBusy] = useState(false)

  // --------------------------------------------------
  // INITIAL DASHBOARD DATA
  // --------------------------------------------------

  useEffect(() => {
    const controller = new AbortController()

    async function loadDashboard() {
      try {
        const [
          systemResponse,
          machinesResponse,
          simulationsResponse,
        ] = await Promise.all([
          authenticatedFetch('/api/system/status', {
            signal: controller.signal,
          }),
          authenticatedFetch('/api/machines', {
            signal: controller.signal,
          }),
          authenticatedFetch('/api/simulations', {
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

        const [systemData, machinesData, simulationsData]: [
          SystemStatus,
          Machine[],
          SimulationRun[],
        ] = await Promise.all([
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

    void loadDashboard()

    return () => controller.abort()
  }, [])

  // --------------------------------------------------
  // SIGNALR CONNECTION
  // --------------------------------------------------

  useEffect(() => {
    void startSignalRConnection()

    return () => {
      void stopSignalRConnection()
    }
  }, [])

  // --------------------------------------------------
  // LIVE MACHINE UPDATES
  // --------------------------------------------------

  useEffect(() => {
    async function refreshMachines() {
      try {
        const response = await authenticatedFetch('/api/machines')

        if (!response.ok) {
          throw new Error('Failed to refresh machines')
        }

        const updatedMachines: Machine[] =
          await response.json()

        setMachines(updatedMachines)

        console.log('HELIOS LIVE: Machine dashboard updated!')
      } catch (err) {
        console.error(
          'SignalR machine refresh failed:',
          err,
        )
      }
    }

    signalRConnection.on(
      'MachinesChanged',
      refreshMachines,
    )

    return () => {
      signalRConnection.off(
        'MachinesChanged',
        refreshMachines,
      )
    }
  }, [])

  // --------------------------------------------------
  // LIVE SIMULATION UPDATES
  // --------------------------------------------------

  useEffect(() => {
    async function refreshSimulations() {
      try {
        const response = await authenticatedFetch('/api/simulations')

        if (!response.ok) {
          throw new Error('Failed to refresh simulations')
        }

        const updatedSimulations: SimulationRun[] =
          await response.json()

        setSimulations(updatedSimulations)

        console.log('HELIOS LIVE: Simulation dashboard updated!')
      } catch (err) {
        console.error(
          'SignalR simulation refresh failed:',
          err,
        )
      }
    }

    signalRConnection.on(
      'SimulationsChanged',
      refreshSimulations,
    )

    return () => {
      signalRConnection.off(
        'SimulationsChanged',
        refreshSimulations,
      )
    }
  }, [])

  // --------------------------------------------------
  // SYNCHRONIZE AFTER SIGNALR RECONNECT
  // --------------------------------------------------

  useEffect(() => {
    let active = true

    async function refreshAfterReconnect() {
      try {
        const [machinesResponse, simulationsResponse] =
          await Promise.all([
            authenticatedFetch('/api/machines'),
            authenticatedFetch('/api/simulations'),
          ])

        if (!machinesResponse.ok || !simulationsResponse.ok) {
          throw new Error('Failed to synchronize dashboard')
        }

        const [updatedMachines, updatedSimulations]: [
          Machine[],
          SimulationRun[],
        ] = await Promise.all([
          machinesResponse.json(),
          simulationsResponse.json(),
        ])

        if (!active) return

        setMachines(updatedMachines)
        setSimulations(updatedSimulations)

        console.log(
          'HELIOS LIVE: Dashboard synchronized after reconnect!',
        )
      } catch (err) {
        if (active) {
          console.error(
            'SignalR reconnect synchronization failed:',
            err,
          )
        }
      }
    }

    const unsubscribe = subscribeToReconnect(() => {
      void refreshAfterReconnect()
    })

    return () => {
      active = false
      unsubscribe()
    }
  }, [])


  // --------------------------------------------------
  // CONTROL ERROR HANDLING
  // --------------------------------------------------

  function handleControlError(
    err: unknown,
    fallbackMessage: string,
  ) {
    if (err instanceof AuthRequestError) {
      if (err.status === 401) {
        onLogout()
        return
      }

      if (err.status === 403) {
        setError(err.message)
        return
      }
    }

    setError(fallbackMessage)
    console.error(err)
  }


  // --------------------------------------------------
  // MANUAL MACHINE CONTROL
  // --------------------------------------------------

  async function controlMachine(
    id: string,
    action: 'start' | 'stop',
  ) {
    if (!canOperate || simulationRunning || simulationBusy) return

    setBusyMachineId(id)
    setError(null)

    try {
      const response = await authenticatedFetch(
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

      const machinesResponse = await authenticatedFetch('/api/machines')

      if (!machinesResponse.ok) {
        throw new Error('Failed to refresh machines')
      }

      const updatedMachines: Machine[] =
        await machinesResponse.json()

      setMachines(updatedMachines)
    } catch (err) {
    handleControlError(
    err,
    'Machine control failed. Please try again.',
    )
    } finally {
      setBusyMachineId(null)
    }
  }

  // --------------------------------------------------
  // START AUTOMATIC SIMULATION
  // --------------------------------------------------

  async function startSimulation() {
    if (!canOperate || simulationRunning || simulationBusy) return

    setSimulationBusy(true)
    setError(null)

    try {
      const response = await authenticatedFetch('/api/simulations', {
          method: 'POST',
        })

      if (!response.ok) {
        throw new Error(
          `Simulation start failed: ${response.status}`,
        )
      }

      const simulationsResponse =
        await authenticatedFetch('/api/simulations')

      if (!simulationsResponse.ok) {
        throw new Error('Failed to refresh simulations')
      }

      const updatedSimulations: SimulationRun[] =
        await simulationsResponse.json()

      setSimulations(updatedSimulations)
      } catch (err) {
      handleControlError(
        err,
        'Could not start simulation.',
      )
    } finally {
      setSimulationBusy(false)
    }
  }

  // --------------------------------------------------
  // STOP AUTOMATIC SIMULATION
  // --------------------------------------------------

  async function stopSimulation(id: string) {
    if (!canOperate || simulationBusy) return

    setSimulationBusy(true)
    setError(null)

    try {
      const response = await authenticatedFetch(
        `/api/simulations/${id}/stop`,
        {
          method: 'POST',
        },
      )

      if (!response.ok) {
        throw new Error(
          `Simulation stop failed: ${response.status}`,
        )
      }

      const [simulationsResponse, machinesResponse] =
        await Promise.all([
          authenticatedFetch('/api/simulations'),
          authenticatedFetch('/api/machines'),
        ])

      if (
        !simulationsResponse.ok ||
        !machinesResponse.ok
      ) {
        throw new Error('Failed to refresh production data')
      }

      const [updatedSimulations, updatedMachines]: [
        SimulationRun[],
        Machine[],
      ] = await Promise.all([
        simulationsResponse.json(),
        machinesResponse.json(),
      ])

      setSimulations(updatedSimulations)
      setMachines(updatedMachines)
    } catch (err) {
      handleControlError(
        err,
        'Could not stop simulation.',
      )
    } finally {
      setSimulationBusy(false)
    }
  }

  // --------------------------------------------------
  // DERIVED DASHBOARD STATE
  // --------------------------------------------------

  const simulationRunning = simulations.some(
    (simulation) => simulation.status === 1,
  )

  // Choose the running simulation first. After it finishes,
  // retain the most recent run so the completed workpiece
  // remains visible on the robot output station.
  const currentFactorySimulation =
    simulations.find((simulation) => simulation.status === 1) ??
    simulations.find((simulation) => simulation.status === 0) ??
    [...simulations].sort(
      (a, b) =>
        (Date.parse(b.startTime ?? '') || 0) -
        (Date.parse(a.startTime ?? '') || 0),
    )[0] ??
    null

  const canOperate =
  session.roles.includes('Admin') ||
  session.roles.includes('Operator')

  const machineControlsDisabled =
  !canOperate ||
  loading ||
  simulationsLoading ||
  simulationRunning ||
  simulationBusy ||
  busyMachineId !== null

  // --------------------------------------------------
  // DASHBOARD UI
  // --------------------------------------------------

  return (
    <main className="dashboard">
      <header className="header">
        <h1>HELIOS</h1>
        <span>Industrial Digital Twin Platform</span>

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '12px',
            flexWrap: 'wrap',
          }}
        >
          <span>
            {session.email} ({session.roles.join(', ')})
          </span>

          <button
            type="button"
            onClick={onLogout}
          >
            Log out
          </button>
        </div>
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

      <FactoryScene
        machines={machines}
        activeStage={
          currentFactorySimulation?.status === 1
            ? currentFactorySimulation.currentStage
            : null
        }
        simulationId={currentFactorySimulation?.id ?? null}
        simulationStatus={currentFactorySimulation?.status ?? null}
      />

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
                    machineControlsDisabled ||
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
                    machineControlsDisabled ||
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
          disabled={
          !canOperate ||
          loading ||
          simulationsLoading ||
          simulationBusy ||
          simulationRunning ||
          busyMachineId !== null
        }
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
            {simulations.map((simulation) => {
              const progress = Math.round(
                (simulation.currentStage / 5) * 100,
              )

              return (
                <article
                  className="simulation-card"
                  key={simulation.id}
                >
                  <h3>
                    Simulation {simulation.id.slice(0, 8)}
                  </h3>

                  <p>
                    Status:{' '}
                    <strong
                      className={`simulation-status simulation-status-${simulation.status}`}
                    >
                      {simulationStatuses[simulation.status] ??
                        'UNKNOWN'}
                    </strong>
                  </p>

                  <p>
                    Current Stage:{' '}
                    {productionStages[simulation.currentStage] ??
                      'Unknown'}
                  </p>

                  <div className="simulation-progress">
                    <div className="simulation-progress-info">
                      <span>Production Progress</span>
                      <span>{progress}%</span>
                    </div>

                    <div
                      className="simulation-progress-track"
                      role="progressbar"
                      aria-label="Production progress"
                      aria-valuemin={0}
                      aria-valuemax={100}
                      aria-valuenow={progress}
                    >
                      <div
                        className={`simulation-progress-fill ${
                          simulation.status === 4
                            ? 'cancelled'
                            : ''
                        }`}
                        style={{
                          width: `${progress}%`,
                        }}
                      />
                    </div>
                  </div>

                  <p>
                    Produced Items: {simulation.producedItems}
                  </p>

                  {simulation.status === 1 && (
                    <div className="simulation-actions">
                      <button
                        className="stop-button"
                        disabled={!canOperate || simulationBusy}
                        onClick={() =>
                          stopSimulation(simulation.id)
                        }
                      >
                        {simulationBusy
                          ? 'Stopping...'
                          : 'Stop Simulation'}
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
              )
            })}
          </div>
        )}
      </section>
    </main>
  )
}

export default App
