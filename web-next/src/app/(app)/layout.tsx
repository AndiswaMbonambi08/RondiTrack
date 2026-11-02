import { Suspense } from 'react'
import { redirect } from 'next/navigation'
import { logout } from '@/app/actions/auth'
import { AppShell } from '@/components/AppShell'
import { PinsOwner } from '@/components/PinsOwner'
import { PinsProvider } from '@/components/PinsProvider'
import { QueryProvider } from '@/components/QueryProvider'
import { SignOutForm } from '@/components/SignOutForm'
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
      <SignOutForm action={logout} />
    </div>
  )
}

// Tells the pins store who is signed in, so pins saved by someone else are cleared.
async function ClaimPins() {
  const session = await getSession()
  return session ? <PinsOwner userId={session.userId} /> : null
}

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <QueryProvider>
      <PinsProvider>
        <AppShell
          footer={
            <Suspense fallback={<p className="text-sm opacity-75">Loading account...</p>}>
              <SignedInAs />
            </Suspense>
          }
        >
          {children}
        </AppShell>
        <Suspense fallback={null}>
          <ClaimPins />
        </Suspense>
      </PinsProvider>
    </QueryProvider>
  )
}