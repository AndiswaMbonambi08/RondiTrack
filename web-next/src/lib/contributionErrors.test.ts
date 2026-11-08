import { describe, expect, it } from 'vitest'
import { ApiError } from './apiError'
import { describeContributionError } from './contributionErrors'

type P = ConstructorParameters<typeof ApiError>[1]
const problem = (extra: Record<string, unknown>) => ({ correlationId: 'corr-1', ...extra }) as unknown as P

describe('describeContributionError', () => {
  it('puts an API field with a different name under the right form field', () => {
    const f = describeContributionError(new ApiError(400, problem({ errors: { AmountInCents: ['Too small.'] } }), null))
    expect(f.fieldErrors.amount).toBe('Too small.')
    expect(f.reference).toBe('corr-1')
  })

  it('maps userId to the member field', () => {
    const f = describeContributionError(new ApiError(400, problem({ errors: { userId: ['Not a member.'] } }), null))
    expect(f.fieldErrors.memberId).toBe('Not a member.')
  })

  it('a 409 carries its reference and does not say it may have landed', () => {
    const f = describeContributionError(new ApiError(409, problem({ detail: 'Already recorded.' }), null))
    expect(f.reference).toBe('corr-1')
    expect(f.mayHaveLanded).toBe(false)
  })

  it('a 500 and a lost connection may have landed, so retrying must reuse the key', () => {
    expect(describeContributionError(new ApiError(500, problem({}), null)).mayHaveLanded).toBe(true)
    expect(describeContributionError(new Error('network')).mayHaveLanded).toBe(true)
  })
})