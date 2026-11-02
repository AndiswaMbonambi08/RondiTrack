import { NextResponse, type NextRequest } from 'next/server'
import { fetchHistoryPage, respond, unauthorized } from '@/lib/bff'
import { historyLoader, UUID_RE } from '@/lib/historyParsers'
import { getSession } from '@/lib/session'

export async function GET(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const session = await getSession()
  if (!session) return unauthorized()

  const { id } = await params
  if (!UUID_RE.test(id)) return NextResponse.json({ error: 'not-found' }, { status: 404 })

  // The same loader the page uses: nonsense in the query falls back to the defaults.
  const filters = await historyLoader(request.nextUrl.searchParams)
  const pageToken = request.nextUrl.searchParams.get('pageToken') ?? ''
  return respond(await fetchHistoryPage(session, id, filters, pageToken))
}