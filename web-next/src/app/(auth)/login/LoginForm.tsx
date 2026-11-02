'use client'

import Link from 'next/link'
import { useActionState } from 'react'
import { login, type LoginState } from '@/app/actions/auth'

const input =
  'rounded-xl border border-input bg-background px-3 py-2 text-foreground focus-visible:outline-2 focus-visible:outline-ring'

export function LoginForm({ expired, next }: { expired: boolean; next: string }) {
  const [state, formAction, pending] = useActionState<LoginState, FormData>(login, null)

  return (
    <form action={formAction} className="flex w-full max-w-sm flex-col gap-4 rounded-3xl bg-card p-6 shadow-sm">
      <h1 className="text-xl font-bold tracking-tight">Sign in to RondiTrack</h1>
      <input type="hidden" name="next" value={next} />

      {expired && !state && (
        <p role="status" className="text-sm text-amber-700 dark:text-amber-300">
          Your session ended. Please sign in again.
        </p>
      )}

      <div className="flex flex-col gap-1">
        <label htmlFor="email" className="text-sm font-medium">
          Email
        </label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="username"
          required
          defaultValue={state?.email ?? ''}
          className={input}
        />
      </div>
      <div className="flex flex-col gap-1">
        <label htmlFor="password" className="text-sm font-medium">
          Password
        </label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          className={input}
        />
      </div>

      {state?.error && (
        <p role="alert" className="text-sm text-red-700 dark:text-red-300">
          {state.error}
          {state.correlationId && <span className="block text-xs">Reference: {state.correlationId}</span>}
        </p>
      )}

      <button
        type="submit"
        disabled={pending}
        className="min-h-11 rounded-full bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/80 disabled:opacity-60"
      >
        {pending ? 'Signing in...' : 'Sign in'}
      </button>

      <p className="text-sm">
        New here?{' '}
        <Link href="/register" className="font-medium underline">
          Create an account
        </Link>
      </p>
    </form>
  )
}