// `next` is followed only if it is a plain path on THIS site.
// Refused: //evil.example, /\evil.example, https://..., javascript:..., control characters,
// and the login/register pages themselves (that would loop).
export function safeNext(value: string | null | undefined, fallback = '/dashboard/member'): string {
  if (!value) return fallback
  if (!value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return fallback
  if (/[\u0000-\u001f\u007f]/.test(value)) return fallback
  if (/^\/(login|register)(\/|\?|#|$)/.test(value)) return fallback
  try {
    if (new URL(value, 'http://site.invalid').origin !== 'http://site.invalid') return fallback
  } catch {
    return fallback
  }
  return value
}