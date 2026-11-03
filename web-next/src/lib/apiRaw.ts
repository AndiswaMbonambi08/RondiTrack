import 'server-only'
import type { Session } from './sessionCookie'

// For the 9.1 endpoints (not in the generated schema). Server to API only. Null means the API was unreachable.
export async function rawFetch(session: Session, path: string, init: RequestInit = {}): Promise<Response | null> {
  const base = process.env.API_BASE_URL
  if (!base) throw new Error('API_BASE_URL is not set')
  try {
    return await fetch(`${base}${path}`, {
      ...init,
      headers: { ...(init.headers as Record<string, string> | undefined), Authorization: `Bearer ${session.accessToken}` },
      cache: 'no-store',
    })
  } catch {
    return null
  }
}

export type ProblemBody = { code?: string; detail?: string; title?: string; correlationId?: string; stokvelId?: string }

export async function readProblem(res: Response): Promise<ProblemBody> {
  return ((await res.json().catch(() => null)) as ProblemBody | null) ?? {}
}