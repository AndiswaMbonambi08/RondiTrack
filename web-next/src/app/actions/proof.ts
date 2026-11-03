'use server'

import { refresh, revalidatePath } from 'next/cache'
import { redirect } from 'next/navigation'
import { rawFetch, readProblem } from '@/lib/apiRaw'
import { UUID_RE } from '@/lib/historyParsers'
import { deleteSession, getSession } from '@/lib/session'

export type ProofResult =
  | { status: 'ok' }
  | { status: 'rejected'; message: string }                    // shown under the input
  | { status: 'failed'; message: string; reference?: string }  // unexpected: shown as a toast

const failed = (reference?: string): ProofResult => ({
  status: 'failed',
  message: 'Something went wrong on our side, or we could not reach RondiTrack. Try again.',
  reference,
})

async function settle(res: Response | null, stokvelId: string): Promise<ProofResult> {
  if (res === null) return failed()
  if (res.ok) {
    revalidatePath(`/stokvels/${stokvelId}/contributions`)
    refresh()
    return { status: 'ok' }
  }
  if (res.status === 401) {
    await deleteSession()
    redirect('/login?expired=1')
  }
  const problem = await readProblem(res)
  if (res.status === 404) return { status: 'rejected', message: 'We could not find that contribution.' }
  if ([400, 403, 409, 413].includes(res.status)) {
    return { status: 'rejected', message: problem.detail ?? 'The file was not accepted.' }
  }
  return failed(problem.correlationId)
}

// A public endpoint: the session first, then the ids, then the API (which checks the file's bytes again).
export async function uploadProof(formData: FormData): Promise<ProofResult> {
  const session = await getSession()
  if (!session) redirect('/login')

  const stokvelId = String(formData.get('stokvelId') ?? '')
  const contributionId = String(formData.get('contributionId') ?? '')
  const file = formData.get('file')
  if (!UUID_RE.test(stokvelId) || !UUID_RE.test(contributionId)) return { status: 'rejected', message: 'We could not find that contribution.' }
  if (!(file instanceof File) || file.size === 0) return { status: 'rejected', message: 'Choose a file first.' }

  const body = new FormData()
  body.set('file', file, 'proof')
  const res = await rawFetch(session, `/api/v1/stokvels/${stokvelId}/contributions/${contributionId}/proof`, { method: 'POST', body })
  return settle(res, stokvelId)
}

export async function removeProof(stokvelId: string, contributionId: string): Promise<ProofResult> {
  const session = await getSession()
  if (!session) redirect('/login')
  if (!UUID_RE.test(stokvelId) || !UUID_RE.test(contributionId)) return { status: 'rejected', message: 'We could not find that contribution.' }

  const res = await rawFetch(session, `/api/v1/stokvels/${stokvelId}/contributions/${contributionId}/proof`, { method: 'DELETE' })
  return settle(res, stokvelId)
}