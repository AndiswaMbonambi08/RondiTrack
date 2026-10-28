import type { components } from './schema'
import { parseRetryAfter } from './retryAfter'

// Generated types only. If correlationId or errors are missing here, fix the API's OpenAPI
// annotations and run `npm run api:types`. Do not widen this by hand.
export type Problem = components['schemas']['ProblemDetails']

export class ApiError extends Error {
  readonly status: number
  readonly problem: Problem | null
  readonly retryAfter: number | null

  constructor(status: number, problem: Problem | null, retryAfter: number | null) {
    super(problem?.title ?? `HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
    this.retryAfter = retryAfter
  }

  static async fromResponse(response: Response): Promise<ApiError> {
    const problem = (await response.json().catch(() => null)) as Problem | null
    return new ApiError(response.status, problem, parseRetryAfter(response.headers.get('Retry-After')))
  }
}