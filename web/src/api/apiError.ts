import type { components } from './schema'
import { parseRetryAfter } from '../lib/retryAfter'

// Generated types only. If correlationId or errors are missing here, fix the API's OpenAPI
// annotations and run `npm run api:types`. Do not widen this by hand.
export type Problem =
  | components['schemas']['ProblemDetails']
  | components['schemas']['HttpValidationProblemDetails']

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: Problem | null,
    public readonly retryAfter: number | null,
  ) {
    super(problem?.title ?? `HTTP ${status}`)
    this.name = 'ApiError'
  }

  static async fromResponse(response: Response): Promise<ApiError> {
    const problem = (await response.json().catch(() => null)) as Problem | null
    return new ApiError(response.status, problem, parseRetryAfter(response.headers.get('Retry-After')))
  }
}