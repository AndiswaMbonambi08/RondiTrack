'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useEffect, useState, type ReactNode } from 'react'
import { Menu, X } from 'lucide-react'
import { ThemeToggle } from '@/components/ThemeToggle'
import { Wordmark } from '@/components/Wordmark'

// The treasurer link is shown to everyone. Hiding it would only be a convenience: the page is
// safe either way, because the API decides what each person may see.
const links = [
  { href: '/dashboard/member', label: 'Member dashboard' },
  { href: '/dashboard/treasurer', label: 'Treasurer dashboard' },
  { href: '/stokvels', label: 'My stokvels' },
]

function NavLinks({ onNavigate }: { onNavigate: () => void }) {
  const pathname = usePathname()
  return (
    <nav aria-label="Main" className="mt-6 flex flex-col gap-1">
      {links.map((l) => {
        const active = pathname === l.href || pathname.startsWith(`${l.href}/`)
        return (
          <Link
            key={l.href}
            href={l.href}
            onClick={onNavigate}
            aria-current={active ? 'page' : undefined}
            className={`rounded-full px-4 py-2.5 text-sm font-medium transition ${
              active ? 'bg-emerald-600 text-white' : 'hover:bg-emerald-300 dark:hover:bg-emerald-800'
            }`}
          >
            {l.label}
          </Link>
        )
      })}
    </nav>
  )
}

export function AppShell({ footer, children }: { footer: ReactNode; children: ReactNode }) {
  const [open, setOpen] = useState(false)
  const close = () => setOpen(false)

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open])

  return (
    <div className="min-h-screen">
      <header className="sticky top-0 z-30 rounded-b-3xl bg-emerald-200 text-emerald-950 dark:bg-emerald-900 dark:text-emerald-50">
        <div className="mx-auto flex max-w-4xl items-center justify-between px-4 py-3">
          <Link href="/dashboard/member" className="text-xl">
            <Wordmark />
          </Link>
          <div className="flex items-center gap-1">
            <ThemeToggle className="size-11" />
            <button
              type="button"
              aria-label={open ? 'Close menu' : 'Open menu'}
              aria-expanded={open}
              aria-controls="menu-drawer"
              onClick={() => setOpen((o) => !o)}
              className="flex size-11 items-center justify-center rounded-full hover:bg-emerald-300 dark:hover:bg-emerald-800"
            >
              {open ? <X aria-hidden="true" className="size-6" /> : <Menu aria-hidden="true" className="size-6" />}
            </button>
          </div>
        </div>
      </header>

      {open && (
        <div className="fixed inset-0 z-40">
          <button type="button" aria-label="Close menu" className="absolute inset-0 bg-black/40" onClick={close} />
          <aside
            id="menu-drawer"
            className="absolute inset-y-0 right-0 flex w-72 max-w-[85%] flex-col overflow-y-auto rounded-l-3xl bg-emerald-200 p-5 text-emerald-950 shadow-xl dark:bg-emerald-900 dark:text-emerald-50"
          >
            <div className="flex items-center justify-between">
              <span className="text-xl">
                <Wordmark />
              </span>
              <button
                type="button"
                aria-label="Close menu"
                onClick={close}
                className="flex size-10 items-center justify-center rounded-full hover:bg-emerald-300 dark:hover:bg-emerald-800"
              >
                <X aria-hidden="true" className="size-5" />
              </button>
            </div>
            <NavLinks onNavigate={close} />
            <div className="mt-auto border-t border-emerald-300 pt-4 dark:border-emerald-800">{footer}</div>
          </aside>
        </div>
      )}

      <main className="mx-auto max-w-4xl p-4 md:p-8">{children}</main>
    </div>
  )
}