import type { NextRequest } from 'next/server'
import { fetchActivityPage, respond, unauthorized } from '@/lib/bff'
import { getSession } from '@/lib/session'

export async function GET(request: NextRequest) {
  const session = await getSession()
  if (!session) return unauthorized()
  return respond(await fetchActivityPage(session, request.nextUrl.searchParams.get('pageToken') ?? ''))
}