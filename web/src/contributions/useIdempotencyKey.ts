import { useCallback, useRef } from 'react'

/**
 * One Idempotency-Key per intent to pay.
 *
 * - The key is created lazily inside getKey(), which is only called from the
 *   submit handler. Nothing is generated during render, so StrictMode's double
 *   render cannot make two keys for one intent.
 * - `intent` should describe the payment (stokvel, cycle, member, amount in
 *   cents). When it changes, the person means a different payment, so the
 *   next getKey() returns a new key.
 * - After a 201 call discard(), so the next contribution gets a new key.
 * - Network error, 5xx, 429: do nothing. The same key is returned again.
 */
export function useIdempotencyKey(intent: string) {
  const current = useRef<{ intent: string; key: string } | null>(null)

  const getKey = useCallback((): string => {
    if (current.current === null || current.current.intent !== intent) {
      current.current = { intent, key: crypto.randomUUID() }
    }
    return current.current.key
  }, [intent])

  const discard = useCallback(() => {
    current.current = null
  }, [])

  return { getKey, discard }
}
