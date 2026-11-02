import { NextResponse, type NextRequest } from 'next/server'
import { refreshOnce } from '@/lib/refresh'
import { decryptSession, encryptSession, SESSION_COOKIE, sessionCookieOptions } from '@/lib/sessionCookie'

const AUTH_PAGES = ['/login', '/register']

// Refresh when the 15-minute access token has less than this many seconds left. Default 2 minutes:
// more than the API's 30 s clock skew plus a slow page that makes several API calls, and still
// small enough that one refresh serves most of the token's life. Override only for experiments.
function marginSeconds(): number {
  const raw = process.env.REFRESH_MARGIN_SECONDS
  const n = Number(raw)
  return raw && Number.isFinite(n) && n >= 0 ? n : 120
}

// The proxy is a convenience: pages and actions still check the session themselves.
export async function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl
  const isAuthPage = AUTH_PAGES.some((p) => pathname === p || pathname.startsWith(`${p}/`))

  const raw = request.cookies.get(SESSION_COOKIE)?.value
  const session = raw ? await decryptSession(raw) : null

  // No session.
  if (!session) {
    if (isAuthPage) {
      const res = NextResponse.next()
      if (raw) res.cookies.delete(SESSION_COOKIE)
      return res
    }
    const url = new URL('/login', request.url)
    if (pathname !== '/') url.searchParams.set('next', pathname + search)
    const res = NextResponse.redirect(url, 303)
    if (raw) res.cookies.delete(SESSION_COOKIE)
    return res
  }

  if (isAuthPage) return NextResponse.next()

  // Still plenty of life left.
  const secondsLeft = session.accessExpiresAt - Math.floor(Date.now() / 1000)
  if (secondsLeft > marginSeconds()) return NextResponse.next()

  let outcome
  try {
    outcome = await refreshOnce(session.refreshToken)
  } catch {
    // The API could not be reached or had a bad moment. That says nothing about the session.
    console.error('[refresh] the API could not refresh right now; keeping the session')
    return NextResponse.next()
  }

  // Refused: the session is over.
  if (!outcome.ok) {
    const res = NextResponse.redirect(new URL('/login?expired=1', request.url), 303)
    res.cookies.delete(SESSION_COOKIE)
    return res
  }

  const token = await encryptSession({
    ...session,
    accessToken: outcome.accessToken,
    refreshToken: outcome.refreshToken,
    accessExpiresAt: outcome.accessExpiresAt,
  })

  // Written twice: into THIS request, so the page about to render sees the new session...
  request.cookies.set(SESSION_COOKIE, token)
  const response = NextResponse.next({ request: { headers: request.headers } })
  // ...and onto the response, so the browser keeps it.
  response.cookies.set(SESSION_COOKIE, token, sessionCookieOptions)
  return response
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|ico)$).*)'],
}