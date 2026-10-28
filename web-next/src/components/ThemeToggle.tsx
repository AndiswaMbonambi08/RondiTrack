'use client'

import { useSyncExternalStore } from 'react'
import { Moon, Sun } from 'lucide-react'

// The <html> class is the source of truth. The inline script in the root layout sets it before
// first paint, and this component only reads it (so there is no flash and no hydration mismatch).
const subscribe = (cb: () => void) => {
  const observer = new MutationObserver(cb)
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
  return () => observer.disconnect()
}
const isDark = () => document.documentElement.classList.contains('dark')
const serverSnapshot = () => false

export function ThemeToggle({ className = '' }: { className?: string }) {
  const dark = useSyncExternalStore(subscribe, isDark, serverSnapshot)
  const label = dark ? 'Switch to light mode' : 'Switch to dark mode'

  function toggle() {
    const next = !dark
    document.documentElement.classList.toggle('dark', next)
    try {
      localStorage.setItem('theme', next ? 'dark' : 'light')
    } catch {
      // Private mode or blocked storage: the theme still changes, it just is not remembered.
    }
  }

  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      onClick={toggle}
      className={`flex items-center justify-center rounded-full hover:bg-emerald-300 dark:hover:bg-emerald-800 ${className}`}
    >
      {dark ? <Sun aria-hidden="true" className="size-5" /> : <Moon aria-hidden="true" className="size-5" />}
    </button>
  )
}