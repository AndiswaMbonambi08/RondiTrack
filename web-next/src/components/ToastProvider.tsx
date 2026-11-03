'use client'

import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'

type Toast = { id: number; kind: 'success' | 'failure'; message: string; reference?: string }
type ToastApi = { success: (message: string) => void; failure: (message: string, reference?: string) => void }

const ToastContext = createContext<ToastApi | null>(null)

// Success toasts go away by themselves. A failure you did not expect stays until it is closed,
// and shows the correlationId so it can be quoted.
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const next = useRef(1)

  const dismiss = useCallback((id: number) => setToasts((all) => all.filter((t) => t.id !== id)), [])
  const add = useCallback(
    (toast: Omit<Toast, 'id'>, autoCloseMs?: number) => {
      const id = next.current++
      setToasts((all) => [...all, { ...toast, id }])
      if (autoCloseMs) setTimeout(() => dismiss(id), autoCloseMs)
    },
    [dismiss],
  )
  const api = useMemo<ToastApi>(
    () => ({
      success: (message) => add({ kind: 'success', message }, 6000),
      failure: (message, reference) => add({ kind: 'failure', message, reference }),
    }),
    [add],
  )

  return (
    <ToastContext value={api}>
      {children}
      <div className="fixed bottom-4 right-4 z-[60] flex w-80 max-w-[90vw] flex-col gap-2">
        {toasts.map((t) => (
          <div
            key={t.id}
            role={t.kind === 'failure' ? 'alert' : 'status'}
            className={`rounded-xl border p-3 text-sm shadow-lg ${
              t.kind === 'failure'
                ? 'border-rose-300 bg-rose-50 text-rose-900 dark:border-rose-800 dark:bg-rose-950 dark:text-rose-100'
                : 'border-emerald-300 bg-emerald-50 text-emerald-900 dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-100'
            }`}
          >
            <p className="font-medium">{t.message}</p>
            {t.reference && <p className="mt-1 text-xs">Reference: {t.reference}</p>}
            {t.kind === 'failure' && (
              <button
                type="button"
                onClick={() => dismiss(t.id)}
                className="mt-2 min-h-9 rounded-full border px-3 text-xs font-semibold"
              >
                Close
              </button>
            )}
          </div>
        ))}
      </div>
    </ToastContext>
  )
}

export function useToast(): ToastApi {
  const api = useContext(ToastContext)
  if (!api) throw new Error('useToast must be used inside <ToastProvider>')
  return api
}