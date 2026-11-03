'use server'

import { refresh, revalidatePath } from 'next/cache'
import { redirect } from 'next/navigation'
import { rawFetch, readProblem } from '@/lib/apiRaw'
import { UUID_RE } from '@/lib/historyParsers'
import { deleteSession, getSession } from '@/lib/session'

export type CloseResult = {
  status: 'closed' | 'stale' | 'already-closed' | 'forbidden' | 'rejected' | 'failed'
  message: string
  reference?: string
}

// The browser sends back the version it was shown and the key for this intent. The API decides.
export async function closeCycle(input: {
  stokvelId: string
  cycleId: string
  version: string
  key: string
}): Promise<CloseResult> {
  const session = await getSession()
  if (!session) redirect('/login')
  const { stokvelId, cycleId, version, key } = input
  if (!UUID_RE.test(stokvelId) || !UUID_RE.test(cycleId) || !version || key.length < 8 || key.length > 100) {
    return { status: 'rejected', message: 'That request was not valid. Reload the page and try again.' }
  }

  const res = await rawFetch(session, `/api/v1/stokvels/${stokvelId}/cycles/${cycleId}/close`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key },
    body: JSON.stringify({ version }),
  })

  // The answer never arrived: it may have closed. Sending again with the same key is safe.
  if (res === null) {
    return {
      status: 'failed',
      message: 'We could not reach RondiTrack, so we cannot tell if the cycle was closed. Trying again is safe: it will close once.',
    }
  }

  if (res.ok) {
    revalidatePath(`/stokvels/${stokvelId}`)
    revalidatePath(`/stokvels/${stokvelId}/contributions`)
    refresh()
    return { status: 'closed', message: 'The cycle was closed.' }
  }
  if (res.status === 401) {
    await deleteSession()
    redirect('/login?expired=1')
  }

  const problem = await readProblem(res)
  if (res.status === 409 && problem.code === 'stale-preview') {
    return { status: 'stale', message: problem.detail ?? 'Something changed since you opened the preview.' }
  }
  if (res.status === 409) return { status: 'already-closed', message: 'This cycle has already been closed.' }
  // A 403 is a message, never a sign-out.
  if (res.status === 403) return { status: 'forbidden', message: 'Only a treasurer of this stokvel can close a cycle.' }
  if (res.status === 400 || res.status === 404) {
    return { status: 'rejected', message: problem.detail ?? 'The cycle could not be closed.' }
  }
  return {
    status: 'failed',
    message: 'Something went wrong on our side, and the cycle may have been closed. Trying again is safe: it will close once.',
    reference: problem.correlationId,
  }
}