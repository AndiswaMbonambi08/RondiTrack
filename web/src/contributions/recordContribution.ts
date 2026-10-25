import { api, unwrap } from '../api/client'

export interface RecordContributionInput {
  stokvelId: string
  userId: string
  cycleId: string
  amountInCents: number
  idempotencyKey: string
}

/** The one place a contribution is sent. The key is passed in, never made here. */
export function recordContribution(input: RecordContributionInput) {
  return unwrap(
    api.POST('/api/v1/stokvels/{id}/contributions', {
      params: {
        path: { id: input.stokvelId },
        header: { 'Idempotency-Key': input.idempotencyKey },
      },
      body: {
        userId: input.userId,
        contributionCycleId: input.cycleId,
        amountInCents: input.amountInCents,
      },
    }),
  )
}
