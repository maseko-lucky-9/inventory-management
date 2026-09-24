import { getToken, clearToken } from './session'
import type { ProblemDetails } from './types'

// The only module that knows HTTP: pages call these functions and handle ApiError.
const base = '/api'

/** A failed call, carrying the API's Problem Details (or a network failure as status 0). */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly errors: Readonly<Record<string, readonly string[]>>

  constructor(status: number, problem: Partial<ProblemDetails>) {
    super(problem.detail ?? `Request failed with status ${status}.`)
    this.status = status
    this.code = problem.code ?? 'unknown_error'
    this.errors = problem.errors ?? {}
  }
}

// Called when a non-login request returns 401 (token expired or missing).
// Set by the app after the router is ready; redirects to /login?redirect=<current path>.
let _onUnauthorized: (() => void) | null = null

export function setUnauthorizedHandler(fn: () => void): void {
  _onUnauthorized = fn
}

export function getJson<T>(path: string): Promise<T> {
  return send<T>(path, { method: 'GET' })
}

export function postJson<T>(path: string, body: unknown): Promise<T> {
  return send<T>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

async function send<T>(path: string, init: RequestInit): Promise<T> {
  const token = getToken()
  const authHeader: Record<string, string> = token ? { Authorization: `Bearer ${token}` } : {}

  let response: Response
  try {
    response = await fetch(base + path, {
      ...init,
      headers: { Accept: 'application/json', ...authHeader, ...init.headers },
    })
  } catch {
    throw new ApiError(0, { code: 'network_error', detail: 'The server could not be reached.' })
  }

  if (!response.ok) {
    const problem = await readProblem(response)
    // On 401 from any endpoint except the login endpoint itself, clear the token and
    // fire the redirect handler so the app sends the user to /login.
    if (response.status === 401 && path !== '/auth/login') {
      clearToken()
      _onUnauthorized?.()
    }
    throw new ApiError(response.status, problem)
  }

  return (await response.json()) as T
}

// A proxy or gateway error may not be JSON; the status alone is still a usable error.
async function readProblem(response: Response): Promise<Partial<ProblemDetails>> {
  try {
    return (await response.json()) as Partial<ProblemDetails>
  } catch {
    return {}
  }
}
