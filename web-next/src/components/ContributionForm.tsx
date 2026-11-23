'use client'

import { useActionState, useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { recordContribution, type ContributionFormState } from '@/app/actions/contributions'
import { formatCents } from '@/lib/money'

type Member = { userId: string; fullName: string }

type Props = {
  stokvelId: string
  cycleId: string
  cycleLabel: string
  selfId: string
  selfName: string
  owedText: string | null
  /** Only passed when THIS stokvel's API answer says the person may record for others. */
  members: Member[] | null
  initialKey: string
  initialAmount: string
}

const field =
  'mt-1 w-full rounded-xl border border-input bg-background px-3 py-2 text-foreground focus-visible:outline-2 focus-visible:outline-ring'
const fieldError = 'mt-1 text-sm text-red-700 dark:text-red-300'

export function ContributionForm(p: Props) {
  // The initial key was written by the server when it rendered this form. useActionState keeps the
  // first initial state, so a later re-render with a new random key cannot swap the key mid-retry.
  const initial: ContributionFormState = {
    status: 'idle',
    values: { memberId: p.selfId, amount: p.initialAmount },
    key: p.initialKey,
    keyIntent: '',
    topErrors: [],
    fieldErrors: {},
  }
  const [state, formAction, pending] = useActionState(recordContribution, initial)
  const queryClient = useQueryClient()
  useEffect(() => {
    if (state.status !== 'success') return
    // Later pages cached for the history and the feed do not include this payment.
    queryClient.removeQueries({ queryKey: ['history'] })
    queryClient.removeQueries({ queryKey: ['activity'] })
  }, [state, queryClient])

  const nameOf = (id: string) =>
    id === p.selfId ? p.selfName : (p.members?.find((m) => m.userId === id)?.fullName ?? 'the member')
  const retrying = state.status === 'error' && (state.mayHaveLanded || state.fieldErrors.amount === undefined && state.message?.startsWith('Too many'))

  return (
    <div className="max-w-xl rounded-3xl bg-card p-5 shadow-sm">
      <h2 className="text-lg font-semibold">Record a contribution</h2>
      <p className="text-sm text-muted-foreground">
        Cycle {p.cycleLabel}.{p.owedText ? ` ${p.owedText}` : ''}
      </p>

      <form action={formAction} className="mt-3 space-y-3">
        <input type="hidden" name="stokvelId" value={p.stokvelId} />
        <input type="hidden" name="cycleId" value={p.cycleId} />
        {/* Written by the server. Remounted whenever the server hands out a different key. */}
        <input key={`k-${state.key}`} type="hidden" name="idempotencyKey" defaultValue={state.key} />
        <input key={`i-${state.keyIntent}`} type="hidden" name="keyIntent" defaultValue={state.keyIntent} />

        {p.members ? (
          <div>
            <label htmlFor="memberId" className="text-sm font-medium">
              Member
            </label>
            <select
              id="memberId"
              name="memberId"
              defaultValue={state.values.memberId}
              aria-invalid={state.fieldErrors.memberId ? true : undefined}
              aria-describedby={state.fieldErrors.memberId ? 'memberId-error' : undefined}
              className={field}
            >
              {p.members.map((m) => (
                <option key={m.userId} value={m.userId}>
                  {m.fullName || m.userId}
                  {m.userId === p.selfId ? ' (you)' : ''}
                </option>
              ))}
            </select>
            {state.fieldErrors.memberId && <p id="memberId-error" className={fieldError}>{state.fieldErrors.memberId}</p>}
          </div>
        ) : (
          <input type="hidden" name="memberId" value={p.selfId} />
        )}

        <div>
          <label htmlFor="amount" className="text-sm font-medium">
            Amount (rands)
          </label>
          <input
            id="amount"
            name="amount"
            inputMode="decimal"
            autoComplete="off"
            defaultValue={state.values.amount}
            aria-invalid={state.fieldErrors.amount ? true : undefined}
            aria-describedby={state.fieldErrors.amount ? 'amount-error' : undefined}
            className={field}
          />
          {state.fieldErrors.amount && (
            <p id="amount-error" className={fieldError}>
              {state.fieldErrors.amount}
            </p>
          )}
        </div>

        {state.status === 'error' && (
          <div
            role="alert"
            className="rounded border border-rose-300 bg-rose-50 p-3 text-sm text-rose-900 dark:border-rose-800 dark:bg-rose-950 dark:text-rose-100"
          >
            <p className="font-medium">{state.message}</p>
            {state.hint && <p className="mt-1">{state.hint}</p>}
            {state.topErrors.length > 0 && (
              <ul className="mt-1 list-disc pl-5">
                {state.topErrors.map((e) => (
                  <li key={e}>{e}</li>
                ))}
              </ul>
            )}
            {state.reference && <p className="mt-1 text-xs">Reference: {state.reference}</p>}
          </div>
        )}

        {state.status === 'success' && state.recordedCents !== undefined && (
          <p role="status" className="rounded bg-emerald-100 p-3 text-sm text-emerald-900 dark:bg-emerald-900 dark:text-emerald-50">
            Recorded {formatCents(state.recordedCents)} for {nameOf(state.values.memberId)} in cycle {p.cycleLabel}.
          </p>
        )}

        {/* One click, one request: disabled while the action is pending. */}
        <button
          type="submit"
          disabled={pending}
          className="min-h-11 rounded-full bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/80 disabled:opacity-50"
        >
          {pending ? 'Recording...' : retrying ? 'Try again' : 'Record contribution'}
        </button>
      </form>
    </div>
  )
}