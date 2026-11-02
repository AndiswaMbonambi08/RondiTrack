// Shared by session.ts (server components and actions) and proxy.ts. No 'server-only' here on
// purpose: the proxy bundle must be able to import it. It never runs in the browser.
import { EncryptJWT, jwtDecrypt } from 'jose'

export const SESSION_COOKIE = 'session'
// Same as the refresh token's life (Jwt:RefreshTokenDays = 7). The cookie is rewritten on every
// refresh, so it lasts 7 days from the last refresh, exactly like the refresh token behind it.
export const SESSION_MAX_AGE_SECONDS = 60 * 60 * 24 * 7

// No role here: a role belongs to a person PER STOKVEL and only comes from the API.
export type Session = {
  accessToken: string
  refreshToken: string
  accessExpiresAt: number // epoch seconds
  userId: string
  email: string
  fullName: string
}

export const sessionCookieOptions = {
  httpOnly: true,
  secure: true,
  sameSite: 'lax' as const,
  path: '/',
  maxAge: SESSION_MAX_AGE_SECONDS,
}

function getKey(): Uint8Array {
  const secret = process.env.SESSION_SECRET
  if (!secret) throw new Error('SESSION_SECRET is not set')
  const bytes = Buffer.from(secret, 'base64url')
  if (bytes.length !== 32) throw new Error('SESSION_SECRET must be 32 bytes, base64url encoded')
  return new Uint8Array(bytes)
}

function isSession(value: unknown): value is Session {
  if (typeof value !== 'object' || value === null) return false
  const v = value as Record<string, unknown>
  return (
    typeof v.accessToken === 'string' &&
    typeof v.refreshToken === 'string' &&
    typeof v.accessExpiresAt === 'number' &&
    typeof v.userId === 'string' &&
    typeof v.email === 'string' &&
    typeof v.fullName === 'string'
  )
}

export async function encryptSession(session: Session): Promise<string> {
  return new EncryptJWT({ ...session })
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime(`${SESSION_MAX_AGE_SECONDS}s`)
    .encrypt(getKey())
}

// A cookie that fails to decrypt, or has the wrong shape, counts as no session at all.
export async function decryptSession(token: string): Promise<Session | null> {
  try {
    const { payload } = await jwtDecrypt(token, getKey())
    const session = {
      accessToken: payload.accessToken,
      refreshToken: payload.refreshToken,
      accessExpiresAt: payload.accessExpiresAt,
      userId: payload.userId,
      email: payload.email,
      fullName: payload.fullName,
    }
    return isSession(session) ? session : null
  } catch {
    return null
  }
}