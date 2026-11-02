'use client'

import Link from 'next/link'
import { usePinsStore } from '@/components/PinsProvider'

export function PinnedStokvels({ onNavigate }: { onNavigate: () => void }) {
  const pins = usePinsStore((s) => s.pins)
  if (pins.length === 0) return null

  return (
    <nav aria-label="Pinned stokvels" className="mt-4 flex flex-col gap-1">
      <p className="px-4 text-xs font-semibold uppercase tracking-wide opacity-75">Pinned</p>
      {pins.map((p) => (
        <Link
          key={p.id}
          href={`/stokvels/${encodeURIComponent(p.id)}`}
          onClick={onNavigate}
          className="break-words rounded-full px-4 py-2.5 text-sm font-medium hover:bg-emerald-300 dark:hover:bg-emerald-800"
        >
          {p.name}
        </Link>
      ))}
    </nav>
  )
}