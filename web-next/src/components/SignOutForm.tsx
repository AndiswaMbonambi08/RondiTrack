'use client'

import { usePinsStore } from '@/components/PinsProvider'
import { PINS_STORAGE_KEY } from '@/lib/pinsKey'

// The Server Action cannot touch localStorage, so the browser clears the pins as the form is sent.
export function SignOutForm({ action }: { action: () => Promise<void> }) {
  const reset = usePinsStore((s) => s.reset)

  return (
    <form
      action={action}
      onSubmit={() => {
        reset()
        try {
          localStorage.removeItem(PINS_STORAGE_KEY)
        } catch {
          // Storage can be blocked: the sign out still goes ahead.
        }
      }}
    >
      <button
        type="submit"
        className="min-h-10 rounded-full bg-emerald-600 px-5 py-2 text-sm font-semibold text-white hover:bg-emerald-700"
      >
        Sign out
      </button>
    </form>
  )
}