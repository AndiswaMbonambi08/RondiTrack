import { Suspense } from 'react'
import { redirect } from 'next/navigation'
import { logout } from '@/app/actions/auth'
import { AppShell } from '@/components/AppShell'
import { getSession } from '@/lib/session'

// The session is read inside <Suspense>, so the sidebar frame streams immediately.
async function SignedInAs() {
  const session = await getSession()
  if (!session) redirect('/login')
  return (
    <div className="space-y-3 text-sm">
      <p className="break-words">
        <span className="font-semibold">{session.fullName}</span>
        <br />
        <span className="opacity-75">{session.email}</span>
      </p>
      <form action={logout}>
        <button
          type="submit"
          className="min-h-10 rounded-full bg-emerald-600 px-5 py-2 text-sm font-semibold text-white hover:bg-emerald-700"
        >
          Sign out
        </button>
      </form>
    </div>
  )
}

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <AppShell
      footer={
        <Suspense fallback={<p className="text-sm opacity-75">Loading account...</p>}>
          <SignedInAs />
        </Suspense>
      }
    >
      {children}
    </AppShell>
  )
}