import { ApiError, type Problem } from '../api/apiError'
import { waitMessage } from '../lib/errorMessages'

export type ContributionField = 'amount' | 'memberId'
export type FailureKind =
  | 'validation' | 'conflict' | 'rateLimited' | 'forbidden'
  | 'notFound' | 'session' | 'network' | 'server' | 'generic'

export interface ContributionFailure {
  kind: FailureKind
  message: string                                  // shown at the top of the form
  hint?: string
  topErrors: string[]                              // 400 keys the form has no field for
  fieldErrors: Partial<Record<ContributionField, string>>
  reference?: string                               // the correlationId
  retryAfter?: number                              // seconds, from Retry-After
  /** True when the server may have recorded the payment before the answer got lost. */
  mayHaveLanded: boolean
}

const FIELD_FOR_KEY: Record<string, ContributionField> = {
  amountInCents: 'amount',
  amount: 'amount',
  userId: 'memberId',
}

type ProblemWithErrors = Problem & { errors?: Record<string, string[]> }

function splitErrors(errors: Record<string, string[]>) {
  const fieldErrors: Partial<Record<ContributionField, string>> = {}
  const topErrors: string[] = []
  for (const [rawKey, messages] of Object.entries(errors)) {
    const key = rawKey.charAt(0).toLowerCase() + rawKey.slice(1)
    const field = FIELD_FOR_KEY[key]
    const text = messages.join(' ')
    if (field) fieldErrors[field] = fieldErrors[field] ? `${fieldErrors[field]} ${text}` : text
    else topErrors.push(`${rawKey}: ${text}`)
  }
  return { fieldErrors, topErrors }
}

export function describeContributionError(error: unknown): ContributionFailure {
  if (!(error instanceof ApiError)) {
    return {
      kind: 'network',
      message:
        "We couldn't reach RondiTrack, so we can't tell whether this payment was recorded. " +
        "It may have gone through. Trying again is safe: the same payment can't be recorded twice.",
      topErrors: [],
      fieldErrors: {},
      mayHaveLanded: true,
    }
  }

  const problem = error.problem as ProblemWithErrors | null
  const reference = problem?.correlationId ?? undefined
  const detail = problem?.detail ?? undefined
  const base = { topErrors: [] as string[], fieldErrors: {} as ContributionFailure['fieldErrors'], reference }

  if (error.status === 429) {
    return {
      ...base,
      kind: 'rateLimited',
      message: `${waitMessage(error.retryAfter)} Nothing was recorded.`,
      retryAfter: error.retryAfter ?? undefined,
      mayHaveLanded: false,
    }
  }
  if (error.status === 400) {
    const split = problem?.errors ? splitErrors(problem.errors) : null
    return {
      ...base,
      kind: 'validation',
      message: split ? 'Check the form.' : (detail ?? 'The request was not valid.'),
      fieldErrors: split?.fieldErrors ?? {},
      topErrors: split?.topErrors ?? [],
      mayHaveLanded: false,
    }
  }
  if (error.status === 401) {
    return { ...base, kind: 'session', message: 'Your session ended. Sign in again to continue.', mayHaveLanded: false }
  }
  if (error.status === 403) {
    return { ...base, kind: 'forbidden', message: 'You are not allowed to record a contribution for this member.', mayHaveLanded: false }
  }
  if (error.status === 404) {
    return { ...base, kind: 'notFound', message: detail ?? "We couldn't find that stokvel, member or cycle.", mayHaveLanded: false }
  }
  if (error.status === 409) {
    return {
      ...base,
      kind: 'conflict',
      message: detail ?? "This contribution can't be recorded right now.",
      hint: 'If your last attempt lost its connection, that attempt may be the one that was recorded. Check the list before trying again.',
      mayHaveLanded: false,
    }
  }
  if (error.status === 422) {
    return { ...base, kind: 'conflict', message: detail ?? "This payment can't be recorded.", mayHaveLanded: false }
  }
  if (error.status >= 500) {
    return {
      ...base,
      kind: 'server',
      message:
        'Something went wrong on our side, and this payment may have gone through. ' +
        "Trying again is safe: the same payment can't be recorded twice.",
      mayHaveLanded: true,
    }
  }
  return {
    ...base,
    kind: 'generic',
    message: 'Something went wrong. Try again, and quote the reference if it keeps happening.',
    mayHaveLanded: false,
  }
}
