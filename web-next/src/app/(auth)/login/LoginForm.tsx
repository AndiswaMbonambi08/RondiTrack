'use client'

import { useActionState } from 'react'
import { login, type LoginState } from '@/app/actions/auth'

export function LoginForm({ expired }: { expired: boolean }) {
  const [state, formAction, pending] = useActionState<LoginState, FormData>(login, null)

  return (
    <form action={formAction} className="flex w-full max-w-sm flex-col gap-4 rounded-xl border p-6">
      <h1 className="text-xl font-semibold">Sign in to RondiTrack</h1>

      {expired && !state && (
        <p role="status" className="text-sm text-amber-700">
          Your session ended. Please sign in again.
        </p>
      )}

      <label className="flex flex-col gap-1 text-sm">
        Email
        <input name="email" type="email" autoComplete="username" required className="rounded border px-3 py-2" />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        Password
        <input name="password" type="password" autoComplete="current-password" required className="rounded border px-3 py-2" />
      </label>

      {state?.error && (
        <p role="alert" className="text-sm text-red-700">
          {state.error}
          {state.correlationId && <span className="block text-xs">Reference: {state.correlationId}</span>}
        </p>
      )}

      <button type="submit" disabled={pending} className="rounded bg-emerald-700 px-4 py-2 text-white disabled:opacity-60">
        {pending ? 'Signing in...' : 'Sign in'}
      </button>
    </form>
  )
}
