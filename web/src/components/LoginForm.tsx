import { useState, type FormEvent } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { login } from '../auth/session'
import { queryKeys } from '../api/queries'
import { describeError, type ErrorDescription } from '../lib/errorMessages'

export function LoginForm() {
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [pending, setPending] = useState(false)
  const [problem, setProblem] = useState<ErrorDescription | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setPending(true)
    setProblem(null)
    try {
      await login(email, password)
      setPassword('')
      await queryClient.invalidateQueries({ queryKey: queryKeys.home })
    } catch (err) {
      setProblem(describeError(err, 'login'))
    } finally {
      setPending(false)
    }
  }

  const field = (name: string) => problem?.fieldErrors?.[name]?.join(' ')
  const input = 'rounded border border-slate-300 bg-white px-2 py-1.5 dark:border-slate-700 dark:bg-slate-800'

  return (
    <div className="flex min-h-screen items-start justify-center bg-slate-100 px-4 pt-24 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <form onSubmit={submit} className="flex w-full max-w-sm flex-col gap-3 rounded-lg bg-white p-6 shadow-sm dark:bg-slate-900">
        <h1 className="text-xl font-bold">Sign in to RondiTrack</h1>
        <label className="flex flex-col gap-1 text-sm">
          Email
          <input className={input} type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} required />
          {field('Email') && <span className="text-rose-700 dark:text-rose-300">{field('Email')}</span>}
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Password
          <input className={input} type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          {field('Password') && <span className="text-rose-700 dark:text-rose-300">{field('Password')}</span>}
        </label>
        {problem && !problem.fieldErrors && (
          <p role="alert" className="text-sm text-rose-700 dark:text-rose-300">
            {problem.message}
            {problem.reference && <> Reference: <code className="select-all font-mono">{problem.reference}</code></>}
          </p>
        )}
        <button className="rounded bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50" disabled={pending}>
          {pending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}