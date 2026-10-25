import { useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../api/queries'
import { recordContribution } from './recordContribution'
import { useIdempotencyKey } from './useIdempotencyKey'

export interface Payment {
  userId: string
  cycleId: string
  amountInCents: number
}

export const intentOf = (stokvelId: string, p: Payment) =>
  [stokvelId, p.cycleId, p.userId, p.amountInCents].join('|')

export function useRecordContribution(stokvelId: string) {
  const queryClient = useQueryClient()
  const { keyFor, discard } = useIdempotencyKey()
  const mutation = useMutation({ mutationFn: recordContribution })

  /**
   * Resolves with the recorded contribution after the queries have refetched, or
   * rejects with the error. A rejection keeps the key, so calling record() again
   * with the same payment retries with the SAME key.
   */
  async function record(payment: Payment) {
    const idempotencyKey = keyFor(intentOf(stokvelId, payment))
    const recorded = await mutation.mutateAsync({ ...payment, stokvelId, idempotencyKey })
    discard() // that intent is done: the next contribution gets a new key
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.home }),
      queryClient.invalidateQueries({ queryKey: queryKeys.contributions(stokvelId) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.cycles(stokvelId) }),
    ])
    return recorded
  }

  return { record }
}
