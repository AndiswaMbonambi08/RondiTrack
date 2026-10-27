import 'server-only'
import { cookies } from 'next/headers'
import { EncryptJWT, jwtDecrypt } from 'jose'

const COOKIE_NAME = 'session'
const MAX_AGE_SECONDS = 60 * 60 * 8 // 8 hours; the access token inside expires much sooner

// Who the person is and the two tokens. There is deliberately no role here:
// a role belongs to a person PER STOKVEL, so it can only come from the API's answer.
export type Session = {
  accessToken: string
  refreshToken: string
  userId: string
  email: string
  fullName: string
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
    typeof v.userId === 'string' &&
    typeof v.email === 'string' &&
    typeof v.fullName === 'string'
  )
}

// Server Actions and Route Handlers only: a Server Component cannot set a cookie.
export async function createSession(session: Session): Promise<void> {
  const token = await new EncryptJWT({ ...session })
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime(`${MAX_AGE_SECONDS}s`)
    .encrypt(getKey())

  const jar = await cookies()
  jar.set(COOKIE_NAME, token, {
    httpOnly: true,
    secure: true,
    sameSite: 'lax',
    path: '/',
    maxAge: MAX_AGE_SECONDS,
  })
}

// A cookie that fails to decrypt, or has the wrong shape, counts as no session at all.
export async function getSession(): Promise<Session | null> {
  const jar = await cookies()
  const token = jar.get(COOKIE_NAME)?.value
  if (!token) return null

  try {
    const { payload } = await jwtDecrypt(token, getKey())
    const session = {
      accessToken: payload.accessToken,
      refreshToken: payload.refreshToken,
      userId: payload.userId,
      email: payload.email,
      fullName: payload.fullName,
    }
    return isSession(session) ? session : null
  } catch {
    return null
  }
}

export async function deleteSession(): Promise<void> {
  const jar = await cookies()
  jar.delete(COOKIE_NAME)
}
