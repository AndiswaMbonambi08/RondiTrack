import 'server-only'
import { NextResponse } from 'next/server'
import type { HistoryFilterValues } from './historyParsers'
import type { ActivityItem, HistoryItem, MemberOption, Page } from './paged'
import { parseRetryAfter } from './retryAfter'
import type { Session } from './sessionCookie'
import { southAfricanDaysToUtcRange } from './southAfricanDays'

export type Fetched<T> =
  | { ok: true; data: T }
  | { ok: false; status: number; retryAfter: number | null; reference?: string }

// Server to API only. The browser never sees this URL or the token.
async function get<T>(session: Session, path: string, params: URLSearchParams): Promise<Fetched<T>> {
  const base = process.env.API_BASE_URL
  if (!base) throw new Error('API_BASE_URL is not set')

  let res: Response
  try {
    res = await fetch(`${base}${path}?${params}`, {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      cache: 'no-store',
    })
  } catch {
    return { ok: false, status: 502, retryAfter: null }
  }
  if (res.ok) return { ok: true, data: (await res.json()) as T }

  const problem = (await res.json().catch(() => null)) as { correlationId?: string } | null
  return {
    ok: false,
    status: res.status,
    retryAfter: parseRetryAfter(res.headers.get('Retry-After')),
    reference: problem?.correlationId,
  }
}

const PAGE_SIZE = '20'

// The only caller of southAfricanDaysToUtcRange. The page and the Route Handler both come through here.
export function fetchHistoryPage(session: Session, stokvelId: string, f: HistoryFilterValues, pageToken: string) {
  const range = southAfricanDaysToUtcRange(f.from, f.to)
  const params = new URLSearchParams({ pageSize: PAGE_SIZE, sort: f.sort })
  if (f.member) params.set('member', f.member)
  if (range.from) params.set('from', range.from)
  if (range.to) params.set('to', range.to)
  if (pageToken) params.set('pageToken', pageToken)
  return get<Page<HistoryItem>>(
    session,
    `/api/v1/stokvels/${encodeURIComponent(stokvelId)}/contribution-history`,
    params,
  )
}

export function fetchActivityPage(session: Session, pageToken: string) {
  const params = new URLSearchParams({ pageSize: PAGE_SIZE })
  if (pageToken) params.set('pageToken', pageToken)
  return get<Page<ActivityItem>>(session, '/api/v1/me/activity', params)
}

// Treasurer or Admin only. Anything else (403) just means no member picker.
export async function loadMembers(session: Session, stokvelId: string): Promise<MemberOption[] | null> {
  const out: MemberOption[] = []
  let token = ''
  for (let page = 0; page < 10; page++) {
    const params = new URLSearchParams({ pageSize: '100' })
    if (token) params.set('pageToken', token)
    const res = await get<Page<MemberOption>>(
      session,
      `/api/v1/stokvels/${encodeURIComponent(stokvelId)}/members`,
      params,
    )
    if (!res.ok) return null
    out.push(...res.data.items.map((m) => ({ userId: m.userId, fullName: m.fullName })))
    if (!res.data.nextPageToken) break
    token = res.data.nextPageToken
  }
  return out
}

const noStore = { 'Cache-Control': 'no-store' }

// A public endpoint: no session is a JSON 401, never an HTML redirect.
export function unauthorized() {
  return NextResponse.json({ error: 'unauthenticated' }, { status: 401, headers: noStore })
}

// Plain JSON out. Problem details from the API are never passed on, only a status and a reference.
export function respond<T>(result: Fetched<T>) {
  if (result.ok) return NextResponse.json(result.data, { headers: noStore })

  const headers: Record<string, string> = { ...noStore }
  if (result.status === 429 && result.retryAfter !== null) headers['Retry-After'] = String(result.retryAfter)
  const status = [400, 401, 403, 404, 429].includes(result.status) ? result.status : 502
  return NextResponse.json({ error: 'request-failed', reference: result.reference ?? null }, { status, headers })
}