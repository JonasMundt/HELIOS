
import * as signalR from '@microsoft/signalr'

const HUB_URL = '/hubs/helios'

export const signalRConnection = new signalR.HubConnectionBuilder()
  .withUrl(HUB_URL)
  .withAutomaticReconnect()
  .configureLogging(signalR.LogLevel.Information)
  .build()

let activeConsumers = 0
let connectionTask: Promise<void> = Promise.resolve()

function queueConnectionOperation(
  operation: () => Promise<void>,
): Promise<void> {
  connectionTask = connectionTask
    .catch(() => {})
    .then(operation)

  return connectionTask
}

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

signalRConnection.on('SystemMessage', (message: string) => {
  console.log('HELIOS LIVE MESSAGE:', message)
})

signalRConnection.on('MachinesChanged', () => {
  console.log('HELIOS LIVE: Machines changed!')
})

signalRConnection.on('SimulationsChanged', () => {
  console.log('HELIOS LIVE: Simulations changed!')
})
