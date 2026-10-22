
import {
  Component,
  type ErrorInfo,
  type ReactNode,
} from 'react'

interface Props {
  children: ReactNode
}

interface State {
  hasError: boolean
}

export default class FactoryErrorBoundary extends Component<
  Props,
  State
> {
  state: State = {
    hasError: false,
  }

  static getDerivedStateFromError(): State {
    return {
      hasError: true,
    }
  }

  componentDidCatch(
    error: Error,
    info: ErrorInfo,
  ): void {
    console.error(
      'HELIOS 3D Factory failed:',
      error,
      info,
    )
  }

  render() {
    if (this.state.hasError) {
      return (
        <section className="factory-loading" role="alert">
          <div>
            <strong>3D Factory unavailable</strong>

            <p>
              The factory could not be loaded.
              Please refresh the page to try again.
            </p>

            <button
              type="button"
              onClick={() => window.location.reload()}
            >
              Reload Factory
            </button>
          </div>
        </section>
      )
    }

    return this.props.children
  }
}
