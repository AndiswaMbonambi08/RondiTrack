import { Suspense } from 'react'
import Link from 'next/link'
import { redirect } from 'next/navigation'
import { logout } from '@/app/actions/auth'
import { getSession } from '@/lib/session'

// The session is read inside <Suspense>, so the sidebar frame streams immediately.
async function SignedInAs() {
  const session = await getSession()
  if (!session) redirect('/login')
  return (
    <div className="space-y-3 text-sm">
      <p className="break-words">
        <span className="font-medium">{session.fullName}</span>
        <br />
        <span className="text-muted-foreground">{session.email}</span>
      </p>
      <form action={logout}>
        <button type="submit" className="rounded border px-3 py-1.5 text-sm hover:bg-muted">
          Sign out
        </button>
      </form>
    </div>
  )
}

// The treasurer link is shown to everyone. Hiding it would only be a convenience: the page is
// safe either way, because the API decides what each person may see.
const links = [
  { href: '/dashboard/member', label: 'Member dashboard' },
  { href: '/dashboard/treasurer', label: 'Treasurer dashboard' },
  { href: '/stokvels', label: 'My stokvels' },
]

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col md:flex-row">
      <aside className="flex flex-col gap-6 border-b p-4 md:w-64 md:border-b-0 md:border-r">
        <p className="text-lg font-semibold">RondiTrack</p>
        <nav className="flex flex-col gap-1 text-sm">
          {links.map((l) => (
            <Link key={l.href} href={l.href} className="rounded px-2 py-1.5 hover:bg-muted">
              {l.label}
            </Link>
          ))}
        </nav>
        <div className="md:mt-auto">
          <Suspense fallback={<p className="text-sm text-muted-foreground">Loading account...</p>}>
            <SignedInAs />
          </Suspense>
        </div>
      </aside>
      <main className="min-w-0 flex-1 p-4 md:p-8">{children}</main>
    </div>
  )
}