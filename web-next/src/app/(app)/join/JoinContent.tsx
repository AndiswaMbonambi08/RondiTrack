import Form from 'next/form'
import Link from 'next/link'
import { redirect } from 'next/navigation'
import { ErrorView } from '@/components/ErrorView'
import { fetchJoinPreview, type JoinPreview } from '@/lib/join'
import { JOIN_STEPS, joinTransition, normaliseCode, resolveStep, stepNumber, stepUrl, type JoinStep } from '@/lib/joinMachine'
import { formatCents } from '@/lib/money'
import { getSession } from '@/lib/session'
import { JoinForm } from './JoinForm'
import { StepHeading } from './StepHeading'

type Props = { searchParams: Promise<Record<string, string | string[] | undefined>> }

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v)
const linkButton =
  'inline-flex min-h-11 items-center rounded-full bg-primary px-5 text-sm font-semibold text-primary-foreground hover:bg-primary/80'
const linkQuiet =
  'inline-flex min-h-11 items-center rounded-full bg-secondary px-5 text-sm font-semibold text-secondary-foreground hover:bg-accent'

function Steps({ step }: { step: JoinStep }) {
  return (
    <ol aria-label="Steps" className="flex flex-wrap gap-2 text-sm">
      {JOIN_STEPS.map((s) => (
        <li
          key={s.id}
          aria-current={s.id === step ? 'step' : undefined}
          className={`rounded-full px-3 py-1 ${s.id === step ? 'bg-emerald-600 font-semibold text-white' : 'bg-muted text-muted-foreground'}`}
        >
          {stepNumber(s.id)}. {s.label}
        </li>
      ))}
    </ol>
  )
}

function Summary({ preview }: { preview: JoinPreview }) {
  return (
    <dl className="grid max-w-md grid-cols-2 gap-2 text-sm">
      <dt>Stokvel</dt>
      <dd className="break-words font-semibold">{preview.stokvelName}</dd>
      <dt>Contribution</dt>
      <dd className="whitespace-nowrap font-semibold tabular-nums">{formatCents(preview.contributionCents)}</dd>
      <dt>Schedule</dt>
      <dd>{preview.schedule}</dd>
      <dt>Members</dt>
      <dd className="font-semibold">{preview.memberCount}</dd>
    </dl>
  )
}

const problemText: Record<string, string> = {
  unknown: 'Check the code and try again.',
  expired: 'Ask for a new invite. This one has expired.',
  busy: 'Too many tries. Wait a moment, then try again.',
}

export async function JoinContent({ searchParams }: Props) {
  const sp = await searchParams
  const rawCode = one(sp.code)
  const code = normaliseCode(rawCode)
  const requested = one(sp.step) ?? null
  const problem = one(sp.problem) ?? null

  // Something typed in the code field that no code could be: back to step 1 with the message.
  if (rawCode && code === null) redirect(stepUrl('code', null, 'unknown'))

  // The URL never shows one step while saying another.
  const step = resolveStep(requested, code)
  if (requested !== step) redirect(stepUrl(step, code))

  const total = JOIN_STEPS.length
  const title = (n: JoinStep, text: string) => `Step ${stepNumber(n)} of ${total}: ${text}`

  if (step === 'code') {
    return (
      <section key="code" className="max-w-xl space-y-4">
        <Steps step={step} />
        <StepHeading>{title('code', 'Your invite code')}</StepHeading>
        <Form action="/join" className="space-y-3 rounded-3xl bg-card p-5 shadow-sm">
          <input type="hidden" name="step" value="rules" />
          <div className="flex flex-col gap-1">
            <label htmlFor="code" className="text-sm font-medium">Invite code</label>
            <input
              id="code"
              name="code"
              defaultValue={code ?? ''}
              required
              autoComplete="off"
              autoCapitalize="characters"
              aria-describedby={problem ? 'code-problem' : undefined}
              aria-invalid={problem ? true : undefined}
              className="rounded-xl border border-input bg-background px-3 py-2 uppercase text-foreground focus-visible:outline-2 focus-visible:outline-ring"
            />
          </div>
          {problem && problemText[problem] && (
            <p id="code-problem" role="alert" className="text-sm text-red-700 dark:text-red-300">
              {problemText[problem]}
            </p>
          )}
          <button type="submit" className={linkButton}>Continue</button>
        </Form>
      </section>
    )
  }

  const session = await getSession()
  if (!session) redirect(`/login?next=${encodeURIComponent(stepUrl(step, code))}`)

  const result = await fetchJoinPreview(session, code!)
  if (result.kind === 'session') redirect('/login?expired=1')
  if (result.kind === 'unknown' || result.kind === 'expired' || result.kind === 'busy') {
    redirect(stepUrl('code', code, result.kind))
  }
  if (result.kind === 'error') {
    return (
      <ErrorView
        description={{
          kind: 'generic',
          title: 'Something went wrong',
          message: 'We could not check that invite. Try again, and quote the reference if it keeps happening.',
          reference: result.reference,
        }}
      />
    )
  }

  const preview = result.preview

  if (preview.alreadyMember && preview.stokvelId) {
    return (
      <section className="max-w-xl space-y-4">
        <StepHeading>You are already a member of {preview.stokvelName}</StepHeading>
        <Link href={`/stokvels/${preview.stokvelId}`} className={linkButton}>Open {preview.stokvelName}</Link>
      </section>
    )
  }

  if (step === 'rules') {
    return (
      <section key="rules" className="max-w-xl space-y-4">
        <Steps step={step} />
        <StepHeading>{title('rules', 'The rules and the amount')}</StepHeading>
        <div className="space-y-4 rounded-3xl bg-card p-5 shadow-sm">
          <Summary preview={preview} />
          <div>
            <h2 className="text-lg font-semibold">Rules</h2>
            <p className="whitespace-pre-wrap break-words text-sm">
              {preview.rules || 'This stokvel has not written any rules yet.'}
            </p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link href={stepUrl(joinTransition('rules', 'back'), code)} className={linkQuiet}>Back</Link>
          <Link href={stepUrl(joinTransition('rules', 'next'), code)} className={linkButton}>Continue</Link>
        </div>
      </section>
    )
  }

  return (
    <section key="confirm" className="max-w-xl space-y-4">
      <Steps step={step} />
      <StepHeading>{title('confirm', 'Confirm and join')}</StepHeading>
      <div className="space-y-4 rounded-3xl bg-card p-5 shadow-sm">
        <Summary preview={preview} />
        <JoinForm code={code!} name={preview.stokvelName} />
      </div>
      <Link href={stepUrl(joinTransition('confirm', 'back'), code)} className={linkQuiet}>Back</Link>
    </section>
  )
}