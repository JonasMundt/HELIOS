
export interface AuthSession {
  accessToken: string
  expiresAtUtc: string
  email: string
  roles: string[]
}

export class AuthRequestError extends Error {
  readonly status: 401 | 403

  constructor(
    status: 401 | 403,
    message: string,
  ) {
    super(message)

    this.name = 'AuthRequestError'
    this.status = status
  }
}


// Keep the access token in memory.
let currentSession: AuthSession | null = null

// --------------------------------------------------
// LOGIN
// --------------------------------------------------

export async function login(
  email: string,
  password: string,
): Promise<AuthSession> {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      email: email.trim(),
      password,
    }),
  })

  if (response.status === 401) {
    throw new Error('Invalid email or password.')
  }

  if (!response.ok) {
    throw new Error('Login failed. Please try again.')
  }

  const session: AuthSession = await response.json()

  currentSession = session

  return session
}

// --------------------------------------------------
// CURRENT SESSION
// --------------------------------------------------

export function getSession(): AuthSession | null {
  if (!currentSession) {
    return null
  }

  const expiresAt = Date.parse(currentSession.expiresAtUtc)

  if (!Number.isFinite(expiresAt) || expiresAt <= Date.now()) {
    currentSession = null
    return null
  }

  return currentSession
}

// --------------------------------------------------
// LOGOUT
// --------------------------------------------------

export function logout(): void {
  currentSession = null
}

// --------------------------------------------------
// AUTHENTICATED API REQUESTS
// --------------------------------------------------

export async function authenticatedFetch(
  input: RequestInfo | URL,
  options: RequestInit = {},
): Promise<Response> {
  const session = getSession()

  if (!session) {
    throw new AuthRequestError(
      401,
      'Your session has expired. Please sign in again.',
    )
  }

  const headers = new Headers(options.headers)

  headers.set(
    'Authorization',
    `Bearer ${session.accessToken}`,
  )

  const response = await fetch(input, {
    ...options,
    headers,
  })

  if (response.status === 401) {
    logout()

    throw new AuthRequestError(
      401,
      'Your session has expired. Please sign in again.',
    )
  }

  if (response.status === 403) {
    throw new AuthRequestError(
      403,
      'Access denied. Your account does not have permission to perform this action.',
    )
  }

  return response
}
