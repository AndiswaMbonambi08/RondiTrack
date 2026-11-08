import { NextResponse, type NextRequest } from 'next/server'
import { rawFetch } from '@/lib/apiRaw'
import { unauthorized } from '@/lib/bff'
import { UUID_RE } from '@/lib/historyParsers'
import { getSession } from '@/lib/session'

const noStore = { 'Cache-Control': 'no-store' }

// Downloads go through this site: the session is checked here, the token is added here, and the file is
// streamed back as an attachment. There is no public storage link anywhere.
export async function GET(_request: NextRequest, { params }: { params: Promise<{ id: string; contributionId: string }> }) {
  const session = await getSession()
  if (!session) return unauthorized()

  const { id, contributionId } = await params
  if (!UUID_RE.test(id) || !UUID_RE.test(contributionId)) {
    return NextResponse.json({ error: 'not-found' }, { status: 404, headers: noStore })
  }

  const res = await rawFetch(session, `/api/v1/stokvels/${id}/contributions/${contributionId}/proof`)
  if (res === null) return NextResponse.json({ error: 'unavailable' }, { status: 502, headers: noStore })
  if (!res.ok) {
    // 403 and 404 look the same to the browser, exactly like a file that does not exist.
    const status = res.status === 401 ? 401 : res.status >= 500 ? 502 : 404
    return NextResponse.json({ error: status === 404 ? 'not-found' : 'request-failed' }, { status, headers: noStore })
  }

  return new Response(res.body, {
    status: 200,
    headers: {
      'Content-Type': res.headers.get('Content-Type') ?? 'application/octet-stream',
      'Content-Disposition': res.headers.get('Content-Disposition') ?? 'attachment; filename="proof-of-payment"',
      'X-Content-Type-Options': 'nosniff',
      'Cache-Control': 'private, no-store',
    },
  })
}