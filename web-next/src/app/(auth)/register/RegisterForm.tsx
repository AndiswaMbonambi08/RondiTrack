'use client'

import Link from 'next/link'
import { useActionState } from 'react'
import { register, type RegisterState } from '@/app/actions/auth'

const input =
  'rounded-xl border border-input bg-background px-3 py-2 text-foreground focus-visible:outline-2 focus-visible:outline-ring'
const fieldError = 'text-sm text-red-700 dark:text-red-300'

export function RegisterForm() {
  const [state, formAction, pending] = useActionState<RegisterState, FormData>(register, null)
  const errors = state?.fieldErrors ?? {}

  // Real labels, paste never blocked, and autocomplete tokens so a password manager knows what to fill.
  return (
    <form action={formAction} className="flex w-full max-w-sm flex-col gap-4 rounded-3xl bg-card p-6 shadow-sm">
      <h1 className="text-xl font-bold tracking-tight">Create your RondiTrack account</h1>

      <div className="flex flex-col gap-1">
        <label htmlFor="fullName" className="text-sm font-medium">
          Full name
        </label>
        <input
          id="fullName"
          name="fullName"
          type="text"
          autoComplete="name"
          required
          defaultValue={state?.values.fullName ?? ''}
          aria-invalid={errors.fullName ? true : undefined}
          aria-describedby={errors.fullName ? 'fullName-error' : undefined}
          className={input}
        />
        {errors.fullName && (
          <p id="fullName-error" className={fieldError}>
            {errors.fullName}
          </p>
        )}
      </div>

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
          defaultValue={state?.values.email ?? ''}
          aria-invalid={errors.email ? true : undefined}
          aria-describedby={errors.email ? 'email-error' : undefined}
          className={input}
        />
        {errors.email && (
          <p id="email-error" className={fieldError}>
            {errors.email}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor="password" className="text-sm font-medium">
          Password (at least 12 characters)
        </label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="new-password"
          required
          aria-invalid={errors.password ? true : undefined}
          aria-describedby={errors.password ? 'password-error' : undefined}
          className={input}
        />
        {errors.password && (
          <p id="password-error" className={fieldError}>
            {errors.password}
          </p>
        )}
      </div>

      {state?.error && (
        <p role="alert" className={fieldError}>
          {state.error}
          {state.correlationId && <span className="block text-xs">Reference: {state.correlationId}</span>}
        </p>
      )}

      <button
        type="submit"
        disabled={pending}
        className="min-h-11 rounded-full bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/80 disabled:opacity-60"
      >
        {pending ? 'Creating account...' : 'Create account'}
      </button>

      <p className="text-sm">
        Already have an account?{' '}
        <Link href="/login" className="font-medium underline">
          Sign in
        </Link>
      </p>
    </form>
  )
}