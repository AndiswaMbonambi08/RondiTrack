'use server'

import { refresh, revalidatePath } from 'next/cache'
import { redirect } from 'next/navigation'
import { z } from 'zod'
import { signedInApi } from '@/lib/api'
import { ApiError } from '@/lib/apiError'
import { describeContributionError } from '@/lib/contributionErrors'
import { parseRetryAfter } from '@/lib/retryAfter'
import { deleteSession } from '@/lib/session'
import { parseRandsToCents } from '@/money/money'

// What the form shows after every attempt. Plain data only.
export type ContributionFormState = {
  status: 'idle' | 'success' | 'error'
  /** What the person typed, so the form can show it again after React resets it. */
  values: { memberId: string; amount: string }
  /** The Idempotency-Key the NEXT submit will carry. Written by the server into a hidden field. */
  key: string
  /** The payment (stokvel|cycle|member|cents) that key was last used for. Empty = key unused. */
  keyIntent: string
  message?: string
  hint?: string
  topErrors: string[]
  fieldErrors: { amount?: string; memberId?: string }
  reference?: string
  mayHaveLanded?: boolean
  recordedCents?: number
}

// Same ceiling as the API's validator (R1 000 000). The API is still the one that decides.
const MAX_CENTS = 100_000_000

// The 7.4 schema, now running on the server.
const schema = z.object({
  stokvelId: z.string().min(1),
  cycleId: z.string().min(1),
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

function done(
  base: Pick<ContributionFormState, 'values' | 'key' | 'keyIntent'>,
  rest: Partial<ContributionFormState> & Pick<ContributionFormState, 'status'>,
): ContributionFormState {
  return { topErrors: [], fieldErrors: {}, ...base, ...rest }
}

// It is a public endpoint: a hidden form proves nothing. Session first, then the schema, then the API.
export async function recordContribution(
  _prev: ContributionFormState,
  formData: FormData,
): Promise<ContributionFormState> {
  // 1. Session. No session means /login (signedInApi redirects).
  const { api } = await signedInApi()

  const text = (name: string) => String(formData.get(name) ?? '')
  const raw = {
    stokvelId: text('stokvelId'),
    cycleId: text('cycleId'),
    memberId: text('memberId'),
    amount: text('amount'),
  }
  const keyIn = text('idempotencyKey') || crypto.randomUUID()
  const keyIntentIn = text('keyIntent')
  const values = { memberId: raw.memberId, amount: raw.amount }

  // 2. Schema. Nothing is sent, so the key and its intent stay exactly as they were.
  const parsed = schema.safeParse(raw)
  if (!parsed.success) {
    const fieldErrors: ContributionFormState['fieldErrors'] = {}
    const topErrors: string[] = []
    for (const issue of parsed.error.issues) {
      const field = String(issue.path[0])
      if (field === 'amount' || field === 'memberId') {
        if (!fieldErrors[field]) fieldErrors[field] = issue.message
      } else if (topErrors.length === 0) {
        topErrors.push('Something is wrong with this form. Reload the page and try again.')
      }
    }
    return done({ values, key: keyIn, keyIntent: keyIntentIn }, { status: 'error', message: 'Check the form.', fieldErrors, topErrors })
  }

  const cents = parseRandsToCents(raw.amount)
  if (cents === null) {
    return done({ values, key: keyIn, keyIntent: keyIntentIn }, { status: 'error', message: 'Check the form.', fieldErrors: { amount: 'Enter an amount.' } })
  }

  // The 7.4 key table: the same payment keeps its key, a changed amount or member is a new payment.
  const intent = [raw.stokvelId, raw.cycleId, raw.memberId, cents].join('|')
  const key = keyIntentIn !== '' && keyIntentIn !== intent ? crypto.randomUUID() : keyIn

  // 3. The API.
  let result
  try {
    result = await api.POST('/api/v1/stokvels/{id}/contributions', {
      params: { path: { id: raw.stokvelId }, header: { 'Idempotency-Key': key } },
      body: { userId: raw.memberId, contributionCycleId: raw.cycleId, amountInCents: cents },
    })
  } catch {
    // The answer never arrived: the payment may have landed. Keep the key so a retry is safe.
    const f = describeContributionError(new Error('network'))
    return done({ values, key, keyIntent: intent }, { status: 'error', message: f.message, mayHaveLanded: f.mayHaveLanded })
  }

  if (result.response.ok && result.data) {
    // The page is not cached, so refresh() re-renders it and the new total comes back in this same response.
    refresh()
    revalidatePath('/activity')
    revalidatePath('/stokvels/' + raw.stokvelId + '/contributions')
    return done(
      { values: { memberId: raw.memberId, amount: '' }, key: crypto.randomUUID(), keyIntent: '' },
      { status: 'success', recordedCents: result.data.amountCents },
    )
  }

  // 401: the session is over. Sign out and go to /login. Never shown as "no access".
  if (result.response.status === 401) {
    await deleteSession()
    redirect('/login?expired=1')
  }

  // Every other failure is RETURNED, not thrown, and keeps the key (the table: do nothing).
  const problem = (typeof result.error === 'object' ? result.error : null) as ConstructorParameters<typeof ApiError>[1]
  const failure = describeContributionError(
    new ApiError(result.response.status, problem, parseRetryAfter(result.response.headers.get('Retry-After'))),
  )
  return done(
    { values, key, keyIntent: intent },
    {
      status: 'error',
      message: failure.message,
      hint: failure.hint,
      topErrors: failure.topErrors,
      fieldErrors: failure.fieldErrors,
      reference: failure.reference,
      mayHaveLanded: failure.mayHaveLanded,
    },
  )
}