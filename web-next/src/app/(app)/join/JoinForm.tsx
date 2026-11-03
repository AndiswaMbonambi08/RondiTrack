'use client'

import { useActionState } from 'react'
import { joinStokvel, type JoinState } from '@/app/actions/join'

export function JoinForm({ code, name }: { code: string; name: string }) {
  const [state, formAction, pending] = useActionState<JoinState, FormData>(joinStokvel, null)

  return (
    <form action={formAction} className="space-y-3">
      <input type="hidden" name="code" value={code} />
      {state?.error && (
        <p role="alert" className="text-sm text-red-700 dark:text-red-300">
          {state.error}
          {state.reference && <span className="block text-xs">Reference: {state.reference}</span>}
        </p>
      )}
      <button
        type="submit"
        disabled={pending}
        className="min-h-11 rounded-full bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/80 disabled:opacity-60"
      >
        {pending ? 'Joining...' : `Join ${name}`}
      </button>
    </form>
  )
}