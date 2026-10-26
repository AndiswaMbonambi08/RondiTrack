import { useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useCycles, useHome, useMe, useMembers } from '../api/queries'
import { formatCents } from '../lib/money'
import { centsToInput, parseRandsToCents } from '../money/money'
import { describeContributionError, type ContributionFailure } from './contributionErrors'
import { useRecordContribution } from './useRecordContribution'

// Same ceiling as the API's validator (R1 000 000). The API is still the one that decides.
export const MAX_CENTS = 100_000_000

const schema = z.object({
  memberId: z.string().min(1, 'Choose a member.'),
  amount: z
    .string()
    .trim()
    .min(1, 'Enter an amount.')
    .refine((t) => parseRandsToCents(t) !== null, {
      message: 'Enter rands like 150 or 150.50, with at most two decimals.',
    })
    .refine((t) => (parseRandsToCents(t) ?? 0) > 0, { message: 'The amount must be more than R0.' })
    .refine((t) => (parseRandsToCents(t) ?? 0) <= MAX_CENTS, {
      message: 'That is more than R1 000 000, the most that can be recorded at once.',
    }),
})

type FormValues = z.infer<typeof schema>

const card = 'rounded-lg bg-white p-4 shadow-sm dark:bg-slate-900'
const muted = 'text-slate-500 dark:text-slate-400'
const field =
  'mt-1 w-full rounded border border-slate-300 bg-white px-3 py-2 text-slate-900 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100'
const fieldError = 'mt-1 text-sm text-rose-700 dark:text-rose-300'

const amountText = (cents: number | null | undefined) => (cents && cents > 0 ? centsToInput(cents) : '')

interface BodyProps {
  stokvelId: string
  selfId: string
  selfName: string
  canPickMember: boolean
  cycle: { id: string; label: string }
  cycleDueCents: number | null
  owedCents: number
}

function ContributionFormBody({
  stokvelId, selfId, selfName, canPickMember, cycle, cycleDueCents, owedCents,
}: BodyProps) {
  const members = useMembers(stokvelId, canPickMember)
  const { record } = useRecordContribution(stokvelId)
  const inFlight = useRef(false) // a second click in the same tick must not send a second request
  const [failure, setFailure] = useState<ContributionFailure | null>(null)
  const [confirmation, setConfirmation] = useState<string | null>(null)

  // What this member still owes if it is the person themselves, else the cycle's amount due.
  const prefillFor = (memberId: string) => (memberId === selfId && owedCents > 0 ? owedCents : cycleDueCents)

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { memberId: selfId, amount: amountText(prefillFor(selfId)) },
  })
  const { isSubmitting, errors } = form.formState

  const memberList = members.data?.pages.flatMap((p) => p.items) ?? []
  const nameOf = (memberId: string) =>
    memberId === selfId ? selfName : (memberList.find((m) => m.userId === memberId)?.fullName || 'the member')

  async function onSubmit(values: FormValues) {
    if (inFlight.current) return
    const cents = parseRandsToCents(values.amount)
    if (cents === null) return

    inFlight.current = true
    setFailure(null)
    setConfirmation(null)
    try {
      // Resolves after the API answered AND the queries refetched, so the button stays disabled till then.
      const recorded = await record({ userId: values.memberId, cycleId: cycle.id, amountInCents: cents })
      setConfirmation(
        `Recorded ${formatCents(recorded.amountCents)} for ${nameOf(values.memberId)} in cycle ${cycle.label}.`,
      )
      // keepFieldsRef: the React Compiler and reset() disagree without it, and the amount would not clear.
      form.reset({ memberId: selfId, amount: amountText(cycleDueCents) }, { keepFieldsRef: true })
    } catch (error) {
      const described = describeContributionError(error)
      setFailure(described)
      if (described.fieldErrors.amount) form.setError('amount', { type: 'server', message: described.fieldErrors.amount })
      if (described.fieldErrors.memberId) form.setError('memberId', { type: 'server', message: described.fieldErrors.memberId })
    } finally {
      inFlight.current = false
    }
  }

  const retrying = failure?.mayHaveLanded === true || failure?.kind === 'rateLimited'
  const label = isSubmitting ? 'Recording...' : retrying ? 'Try again' : 'Record contribution'

  return (
    <div className={card}>
      <h3 className="text-lg font-semibold">Record a contribution</h3>
      <p className={`text-sm ${muted}`}>
        Cycle {cycle.label}.
        {owedCents > 0 ? ` You still owe ${formatCents(owedCents)} this cycle.` : ''}
      </p>

      <form onSubmit={form.handleSubmit(onSubmit)} noValidate className="mt-3 space-y-3">
        {canPickMember && (
          <div>
            <label htmlFor="memberId" className="text-sm font-medium">Member</label>
            <select
              id="memberId"
              className={field}
              aria-invalid={errors.memberId ? true : undefined}
              {...form.register('memberId', {
                onChange: (e) => form.setValue('amount', amountText(prefillFor(e.target.value))),
              })}
            >
              {memberList.map((m) => (
                <option key={m.userId} value={m.userId}>
                  {m.fullName || m.userId}{m.userId === selfId ? ' (you)' : ''}
                </option>
              ))}
            </select>
            {errors.memberId && <p className={fieldError}>{errors.memberId.message}</p>}
            {members.hasNextPage && (
              <button type="button" className="mt-1 text-sm underline" onClick={() => members.fetchNextPage()}>
                Load more members
              </button>
            )}
          </div>
        )}

        <div>
          <label htmlFor="amount" className="text-sm font-medium">Amount (rands)</label>
          <input
            id="amount"
            inputMode="decimal"
            autoComplete="off"
            className={field}
            aria-invalid={errors.amount ? true : undefined}
            aria-describedby={errors.amount ? 'amount-error' : undefined}
            {...form.register('amount')}
          />
          {errors.amount && <p id="amount-error" className={fieldError}>{errors.amount.message}</p>}
        </div>

        {failure && (
          <div
            role="alert"
            className="rounded border border-rose-300 bg-rose-50 p-3 text-sm text-rose-900 dark:border-rose-800 dark:bg-rose-950 dark:text-rose-100"
          >
            <p className="font-medium">{failure.message}</p>
            {failure.hint && <p className="mt-1">{failure.hint}</p>}
            {failure.topErrors.length > 0 && (
              <ul className="mt-1 list-disc pl-5">
                {failure.topErrors.map((e) => <li key={e}>{e}</li>)}
              </ul>
            )}
            {failure.reference && <p className="mt-1 text-xs">Reference: {failure.reference}</p>}
          </div>
        )}

        {confirmation && (
          <p role="status" className="rounded bg-emerald-100 p-3 text-sm text-emerald-900 dark:bg-emerald-900 dark:text-emerald-50">
            {confirmation}
          </p>
        )}

        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
        >
          {label}
        </button>
      </form>
    </div>
  )
}

export function ContributionForm({ stokvelId }: { stokvelId: string }) {
  const me = useMe()
  const home = useHome()
  const cycles = useCycles(stokvelId)

  const entry = home.data?.stokvels.find((s) => s.stokvelId === stokvelId)
  if (!me.data || !entry || cycles.isPending) return null

  const cycle = entry.currentCycle
  const sentence = (text: string) => (
    <div className={card}>
      <h3 className="text-lg font-semibold">Record a contribution</h3>
      <p className={`mt-1 text-sm ${muted}`}>{text}</p>
    </div>
  )

  if (!cycle) return sentence('There is no open cycle to record a contribution into right now.')
  const status = cycle.status.toLowerCase()
  if (status === 'closed') return sentence(`Cycle ${cycle.label} is closed, so contributions can no longer be recorded for it.`)
  if (status === 'paidout') return sentence(`Cycle ${cycle.label} has already been paid out, so contributions can't be recorded for it.`)
  if (status !== 'open') return sentence(`Cycle ${cycle.label} is ${cycle.status}, so it is not taking contributions.`)

  const role = entry.role.toLowerCase()
  const cycleDueCents = cycles.data?.find((c) => c.id === cycle.id)?.amountDueCents ?? null

  return (
    <ContributionFormBody
      key={`${stokvelId}:${cycle.id}`}
      stokvelId={stokvelId}
      selfId={me.data.id}
      selfName={me.data.fullName}
      canPickMember={role === 'treasurer' || role === 'admin'}
      cycle={{ id: cycle.id, label: cycle.label }}
      cycleDueCents={cycleDueCents}
      owedCents={entry.owedCents}
    />
  )
}
