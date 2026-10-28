'use client'

import { useEffect, useState } from 'react'
import type { ErrorDescription } from '@/lib/errorMessages'

// Takes a plain description (built on the server by describeError), never an ApiError instance:
// a class instance cannot cross the server/client boundary, plain data can.
type Props = { description: ErrorDescription; onRetry?: () => void }

function useCountdown(start: number | undefined) {
  const [left, setLeft] = useState(start ?? 0)
  useEffect(() => {
    if (!start) return
    const id = setInterval(() => setLeft((s) => Math.max(0, s - 1)), 1000)
    return () => clearInterval(id)
  }, [start])
  return left
}

export function ErrorView({ description: d, onRetry }: Props) {
  const left = useCountdown(d.retryAfter)
  const message =
    d.kind === 'rateLimited' && d.retryAfter !== undefined
      ? `Too many requests. Try again in ${left} ${left === 1 ? 'second' : 'seconds'}.`
      : d.message

  return (
    <div role="alert" className="rounded-lg border border-rose-300 bg-rose-50 p-4 text-rose-900 dark:border-rose-800 dark:bg-rose-950 dark:text-rose-100">
      <h2 className="text-lg font-semibold">{d.title}</h2>
      <p className="mt-1">{message}</p>
      {d.reference && (
        <p className="mt-2 text-sm">
          Reference: <code className="select-all font-mono">{d.reference}</code>
        </p>
      )}
      {onRetry && d.kind !== 'notFound' && (
        <div className="mt-3">
          <button
            className="rounded bg-rose-900 px-3 py-1.5 text-sm text-white disabled:opacity-50 dark:bg-rose-200 dark:text-rose-950"
            onClick={onRetry}
            disabled={d.kind === 'rateLimited' && left > 0}
          >
            Try again
          </button>
        </div>
      )}
    </div>
  )
}