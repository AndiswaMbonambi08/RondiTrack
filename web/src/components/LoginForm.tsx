import { useState, type FormEvent } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { login } from '../auth/session'
import { queryKeys } from '../api/queries'
import { describeError, type ErrorDescription } from '../lib/errorMessages'
import { Wordmark } from './Wordmark'
import { ThemeToggle } from './ThemeToggle'

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
    <div className="relative flex min-h-screen items-center justify-center bg-emerald-100 px-4 text-slate-900 dark:bg-emerald-950 dark:text-slate-100">
      <div className="absolute right-4 top-4"><ThemeToggle /></div>
      <div className="flex w-full max-w-sm flex-col items-center gap-6">
        <h1>
          <Wordmark className="text-4xl" />
        </h1>
        <form onSubmit={submit} className="flex w-full flex-col gap-3 rounded-lg bg-white p-6 shadow-sm dark:bg-slate-900">
          <h2 className="text-lg font-semibold">Sign in</h2>
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
          <button className="rounded bg-emerald-600 px-3 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50" disabled={pending}>
            {pending ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  )
}