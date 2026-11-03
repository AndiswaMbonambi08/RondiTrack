import 'server-only'
import { rawFetch, readProblem } from './apiRaw'
import type { Session } from './sessionCookie'

export type JoinPreview = {
  stokvelName: string
  rules: string
  contributionCents: number
  schedule: string
  cycleEndDay: number
  memberCount: number
  alreadyMember: boolean
  stokvelId: string | null
}

export type PreviewResult =
  | { kind: 'ok'; preview: JoinPreview }
  | { kind: 'unknown' | 'expired' | 'busy' | 'session' }
  | { kind: 'error'; reference?: string }

export async function fetchJoinPreview(session: Session, code: string): Promise<PreviewResult> {
  const res = await rawFetch(session, `/api/v1/invites/${encodeURIComponent(code)}`)
  if (res === null) return { kind: 'error' }
  if (res.ok) return { kind: 'ok', preview: (await res.json()) as JoinPreview }
  if (res.status === 401) return { kind: 'session' }
  if (res.status === 404) return { kind: 'unknown' }
  if (res.status === 410) return { kind: 'expired' }
  if (res.status === 429) return { kind: 'busy' }
  return { kind: 'error', reference: (await readProblem(res)).correlationId }
}