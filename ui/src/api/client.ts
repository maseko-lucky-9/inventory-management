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

export function getJson<T>(path: string): Promise<T> {
  return send<T>(path, { method: 'GET' })
}

export function postJson<T>(path: string, body: unknown): Promise<T> {
  return send<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
}

async function send<T>(path: string, init: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(base + path, { ...init, headers: { Accept: 'application/json', ...init.headers } })
  } catch {
    throw new ApiError(0, { code: 'network_error', detail: 'The server could not be reached.' })
  }
  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
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
