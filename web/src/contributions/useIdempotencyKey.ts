import { useCallback, useRef } from 'react'

/**
 * One Idempotency-Key per intent to pay.
 *
 * - The key is created lazily inside keyFor(), which is only called from the submit
 *   path. Nothing is generated during render, so StrictMode's double render cannot
 *   make two keys for one intent.
 * - `intent` describes the payment (stokvel, cycle, member, amount in cents). Same
 *   intent returns the same key. A different intent is a different payment, so it
 *   gets a new key.
 * - After a 201, call discard(), so the next contribution gets a new key.
 * - Network error, 5xx, 429, 400, 409, 422: do nothing. The same key comes back.
 */
export function useIdempotencyKey() {
  const current = useRef<{ intent: string; key: string } | null>(null)

  const keyFor = useCallback((intent: string): string => {
    if (current.current === null || current.current.intent !== intent) {
      current.current = { intent, key: crypto.randomUUID() }
    }
    return current.current.key
  }, [])

  const discard = useCallback(() => {
    current.current = null
  }, [])

  return { keyFor, discard }
}
