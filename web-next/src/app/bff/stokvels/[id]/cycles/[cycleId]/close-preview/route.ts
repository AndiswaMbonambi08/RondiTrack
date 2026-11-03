import { NextResponse, type NextRequest } from 'next/server'
import { rawFetch } from '@/lib/apiRaw'
import { unauthorized } from '@/lib/bff'
import { UUID_RE } from '@/lib/historyParsers'
import { getSession } from '@/lib/session'

const noStore = { 'Cache-Control': 'no-store' }

// The browser asks this site. The session is checked here and the token is added here.
export async function GET(_request: NextRequest, { params }: { params: Promise<{ id: string; cycleId: string }> }) {
  const session = await getSession()
  if (!session) return unauthorized()

  const { id, cycleId } = await params
  if (!UUID_RE.test(id) || !UUID_RE.test(cycleId)) return NextResponse.json({ error: 'not-found' }, { status: 404, headers: noStore })

  const res = await rawFetch(session, `/api/v1/stokvels/${id}/cycles/${cycleId}/close-preview`)
  if (res === null) return NextResponse.json({ error: 'unavailable' }, { status: 502, headers: noStore })
  if (res.ok) return NextResponse.json(await res.json(), { headers: noStore })

  const status = [401, 403, 404, 409].includes(res.status) ? res.status : 502
  return NextResponse.json({ error: 'request-failed' }, { status, headers: noStore })
}