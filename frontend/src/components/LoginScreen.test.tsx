
import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import {
  render,
  screen,
  waitFor,
} from '@testing-library/react'

import userEvent from '@testing-library/user-event'
import LoginScreen from './LoginScreen'
import { login } from '../services/authService'

// Mock the authentication service.
vi.mock('../services/authService', async (importOriginal) => {
  const original =
    await importOriginal<
      typeof import('../services/authService')
    >()

  return {
    ...original,
    login: vi.fn(),
  }
})

describe('HELIOS Login Screen', () => {
  beforeEach(() => {
    vi.mocked(login).mockReset()
  })

  // ------------------------------------------
  // TEST 1: LOGIN FORM
  // ------------------------------------------

  it('displays the login form', () => {
    render(<LoginScreen onLogin={vi.fn()} />)

    expect(
      screen.getByText(/welcome back/i),
    ).toBeInTheDocument()

    expect(
      screen.getByText(/email address/i),
    ).toBeInTheDocument()

    expect(
      screen.getByText(/^password$/i),
    ).toBeInTheDocument()

    expect(
      screen.getByRole('button', { name: /sign in/i }),
    ).toBeInTheDocument()
  })

  // ------------------------------------------
  // TEST 2: FAILED LOGIN
  // ------------------------------------------

  it('does not authenticate when login fails', async () => {
    vi.mocked(login).mockRejectedValue(
      new Error('Invalid login credentials'),
    )

    const onLogin = vi.fn()
    const user = userEvent.setup()

    render(<LoginScreen onLogin={onLogin} />)

    await user.type(
      screen.getByRole('textbox', { name: /email/i }),
      'test@example.com',
    )

    await user.type(
      screen.getByLabelText(/password/i),
      'wrong-password',
    )

    await user.click(
      screen.getByRole('button', { name: /sign in/i }),
    )

    await waitFor(() => {
      expect(login).toHaveBeenCalledTimes(1)
    })

    expect(onLogin).not.toHaveBeenCalled()
  })

  // ------------------------------------------
  // TEST 3: SUCCESSFUL LOGIN
  // ------------------------------------------

  it('passes the session on successful login', async () => {
    const mockSession = {
      accessToken: 'test-access-token',
      email: 'operator@example.com',
      roles: ['Operator'],
      expiresAtUtc: '2099-01-01T00:00:00Z',
    } as Awaited<ReturnType<typeof login>>

    vi.mocked(login).mockResolvedValue(mockSession)

    const onLogin = vi.fn()
    const user = userEvent.setup()

    render(<LoginScreen onLogin={onLogin} />)

    await user.type(
      screen.getByRole('textbox', { name: /email/i }),
      'operator@example.com',
    )

    await user.type(
      screen.getByLabelText(/password/i),
      'TestPassword!123',
    )

    await user.click(
      screen.getByRole('button', { name: /sign in/i }),
    )

    await waitFor(() => {
      expect(onLogin).toHaveBeenCalledWith(mockSession)
    })

    expect(login).toHaveBeenCalledTimes(1)
    expect(onLogin).toHaveBeenCalledTimes(1)
  })
})
