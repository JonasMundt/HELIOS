
import * as signalR from '@microsoft/signalr'
import { getSession } from './authService'

const HUB_URL = '/hubs/helios'

// --------------------------------------------------
// SIGNALR CONNECTION
// --------------------------------------------------

export const signalRConnection = new signalR.HubConnectionBuilder()
  .withUrl(HUB_URL, {
    accessTokenFactory: () => {
      const session = getSession()

      // Return the JWT of the currently signed-in user.
      // SignalR calls this again when reconnecting.
      return session?.accessToken ?? ''
    },
  })
  .withAutomaticReconnect()
  .configureLogging(signalR.LogLevel.Warning)
  .build()

let activeConsumers = 0
let connectionTask: Promise<void> = Promise.resolve()

// --------------------------------------------------
// CONNECTION OPERATION QUEUE
// --------------------------------------------------

function queueConnectionOperation(
  operation: () => Promise<void>,
): Promise<void> {
  connectionTask = connectionTask
    .catch(() => {})
    .then(operation)

  return connectionTask
}

// --------------------------------------------------
// START SIGNALR
// --------------------------------------------------

export function startSignalRConnection(): Promise<void> {
  activeConsumers++

  return queueConnectionOperation(async () => {
    if (activeConsumers === 0) return

    if (
      signalRConnection.state ===
      signalR.HubConnectionState.Disconnected
    ) {
      try {
        await signalRConnection.start()
        console.log('HELIOS SignalR connected.')
      } catch (error) {
        console.error(
          'HELIOS SignalR connection failed:',
          error,
        )
      }
    }
  })
}

// --------------------------------------------------
// STOP SIGNALR
// --------------------------------------------------

export function stopSignalRConnection(): Promise<void> {
  activeConsumers = Math.max(0, activeConsumers - 1)

  return queueConnectionOperation(async () => {
    if (activeConsumers > 0) return

    if (
      signalRConnection.state !==
      signalR.HubConnectionState.Disconnected
    ) {
      await signalRConnection.stop()
      console.log('HELIOS SignalR disconnected.')
    }
  })
}

// --------------------------------------------------
// RECONNECT SUBSCRIPTIONS
// --------------------------------------------------

type ReconnectHandler = () => void

const reconnectHandlers = new Set<ReconnectHandler>()

export function subscribeToReconnect(
  handler: ReconnectHandler,
): () => void {
  reconnectHandlers.add(handler)

  return () => {
    reconnectHandlers.delete(handler)
  }
}

// --------------------------------------------------
// SIGNALR CONNECTION EVENTS
// --------------------------------------------------

signalRConnection.onreconnecting(() => {
  console.log('HELIOS SignalR state: RECONNECTING')
})

signalRConnection.onreconnected(() => {
  console.log('HELIOS SignalR state: RECONNECTED')

  for (const handler of reconnectHandlers) {
    handler()
  }
})

signalRConnection.onclose(() => {
  console.log('HELIOS SignalR state: DISCONNECTED')
})

// --------------------------------------------------
// SYSTEM EVENTS
// --------------------------------------------------

signalRConnection.on('SystemMessage', (message: string) => {
  console.log('HELIOS LIVE MESSAGE:', message)
})

// --------------------------------------------------
// MACHINE EVENTS
// --------------------------------------------------

signalRConnection.on('MachinesChanged', () => {
  console.log('HELIOS LIVE: Machines changed!')
})

// --------------------------------------------------
// SIMULATION EVENTS
// --------------------------------------------------

signalRConnection.on('SimulationsChanged', () => {
  console.log('HELIOS LIVE: Simulations changed!')
})
