
import { useState, type FormEvent } from 'react'
import { login, type AuthSession } from '../services/authService'
import './LoginScreen.css'

interface LoginScreenProps {
  onLogin: (session: AuthSession) => void
}

export default function LoginScreen({
  onLogin,
}: LoginScreenProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (loading) return

    setLoading(true)
    setError(null)

    try {
      const session = await login(email, password)

      setPassword('')
      onLogin(session)
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'An unexpected error occurred.',
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="helios-login">
      <div className="helios-login-card">
        <div className="helios-login-brand">
          <div className="helios-login-symbol">H</div>

          <h1>HELIOS</h1>
          <p>Industrial Digital Twin Platform</p>
        </div>

        <div className="helios-login-heading">
          <h2>Welcome back</h2>
          <p>Sign in to access your control center.</p>
        </div>

        <form onSubmit={handleSubmit}>
          <label htmlFor="helios-email">Email address</label>
          <input
            id="helios-email"
            type="email"
            autoComplete="username"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="operator@helios.local"
            required
            disabled={loading}
          />

          <label htmlFor="helios-password">Password</label>
          <input
            id="helios-password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            placeholder="Enter your password"
            required
            disabled={loading}
          />

          {error && (
            <p className="helios-login-error" role="alert">
              {error}
            </p>
          )}

          <button type="submit" disabled={loading}>
            {loading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>

        <p className="helios-login-footer">
          HELIOS · Secure Industrial Platform
        </p>
      </div>
    </main>
  )
}
