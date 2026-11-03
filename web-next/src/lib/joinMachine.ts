// The join flow as a small state machine. The step lives in the URL (/join?code=...&step=...),
// so Back, a reload and the invite link itself all land on the right step.
export type JoinStep = 'code' | 'rules' | 'confirm'
export type JoinEvent = 'next' | 'back'

export const JOIN_STEPS: readonly { id: JoinStep; label: string }[] = [
  { id: 'code', label: 'Invite code' },
  { id: 'rules', label: 'Rules and amount' },
  { id: 'confirm', label: 'Confirm' },
]

const ORDER = JOIN_STEPS.map((s) => s.id)

export const stepNumber = (step: JoinStep) => ORDER.indexOf(step) + 1

// What moves the person on: next goes forward, back goes back, and neither goes past the ends.
export function joinTransition(step: JoinStep, event: JoinEvent): JoinStep {
  const i = ORDER.indexOf(step)
  return ORDER[event === 'next' ? Math.min(i + 1, ORDER.length - 1) : Math.max(i - 1, 0)]
}

const CODE_RE = /^[A-Za-z0-9]{6,20}$/

// A code the API could possibly know. Anything else is null.
export function normaliseCode(value: string | null | undefined): string | null {
  const v = (value ?? '').trim()
  return CODE_RE.test(v) ? v.toUpperCase() : null
}

// The one function that decides which step a URL may show. A step the person cannot be on yet
// (no valid code) resolves to the first step, and the page redirects there.
export function resolveStep(requested: string | null | undefined, code: string | null): JoinStep {
  if (code === null) return 'code'
  if (requested === 'code' || requested === 'rules' || requested === 'confirm') return requested
  return 'rules' // an invite link with only a code: the code is step 1, already answered
}

export function stepUrl(step: JoinStep, code: string | null, problem?: string): string {
  const p = new URLSearchParams()
  if (code) p.set('code', code)
  p.set('step', step)
  if (problem) p.set('problem', problem)
  return `/join?${p.toString()}`
}