'use server'

import { redirect } from 'next/navigation'
import { rawFetch, readProblem } from '@/lib/apiRaw'
import { normaliseCode, stepUrl } from '@/lib/joinMachine'
import { deleteSession, getSession } from '@/lib/session'

export type JoinState = { error: string; reference?: string } | null

// One click is one join: the button is disabled while this runs, and a repeat (a double click, a retry
// after a dropped connection) is answered "already a member", which lands on the stokvel just the same.
export async function joinStokvel(_prev: JoinState, formData: FormData): Promise<JoinState> {
  const code = normaliseCode(String(formData.get('code') ?? ''))
  if (!code) redirect(stepUrl('code', null, 'unknown'))

  const session = await getSession()
  if (!session) redirect(`/login?next=${encodeURIComponent(stepUrl('confirm', code))}`)

  const res = await rawFetch(session, `/api/v1/invites/${encodeURIComponent(code)}/join`, { method: 'POST' })
  if (res === null) return { error: 'We could not reach RondiTrack, so we cannot tell if you joined. Try again: joining twice is safe.' }

  if (res.ok) {
    const body = (await res.json()) as { stokvelId: string }
    redirect(`/stokvels/${body.stokvelId}`)
  }
  if (res.status === 401) {
    await deleteSession()
    redirect('/login?expired=1')
  }

  const problem = await readProblem(res)
  if (res.status === 409 && problem.code === 'already-member' && problem.stokvelId) redirect(`/stokvels/${problem.stokvelId}`)
  if (res.status === 404) redirect(stepUrl('code', code, 'unknown'))
  if (res.status === 410) redirect(stepUrl('code', code, 'expired'))
  if (res.status === 429) return { error: 'Too many tries. Wait a moment, then try again.' }
  return {
    error: 'Something went wrong on our side. Try again, and quote the reference if it keeps happening.',
    reference: problem.correlationId,
  }
}

export type InviteState = { status: 'ok'; code: string; expiresAtUtc: string } | { status: 'error'; message: string }

export async function createInvite(stokvelId: string): Promise<InviteState> {
  const session = await getSession()
  if (!session) redirect('/login')
  if (!/^[0-9a-f-]{36}$/i.test(stokvelId)) return { status: 'error', message: 'That stokvel was not found.' }

  const res = await rawFetch(session, `/api/v1/stokvels/${stokvelId}/invites`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ expiresInDays: 7 }),
  })
  if (res === null) return { status: 'error', message: 'We could not reach RondiTrack. Try again.' }
  if (res.status === 401) {
    await deleteSession()
    redirect('/login?expired=1')
  }
  if (res.status === 403) return { status: 'error', message: 'Only a treasurer of this stokvel can create invites.' }
  if (!res.ok) return { status: 'error', message: 'We could not create the invite. Try again.' }
  const body = (await res.json()) as { code: string; expiresAtUtc: string }
  return { status: 'ok', code: body.code, expiresAtUtc: body.expiresAtUtc }
}