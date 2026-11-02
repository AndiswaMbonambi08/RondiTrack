'use client'

import { usePinsStore } from '@/components/PinsProvider'

export function PinButton({ stokvelId, name }: { stokvelId: string; name: string }) {
  const pinned = usePinsStore((s) => s.pins.some((p) => p.id === stokvelId))
  const pin = usePinsStore((s) => s.pin)
  const unpin = usePinsStore((s) => s.unpin)

  return (
    <button
      type="button"
      aria-pressed={pinned}
      onClick={() => (pinned ? unpin(stokvelId) : pin({ id: stokvelId, name }))}
      className="min-h-11 rounded-full bg-secondary px-5 py-2 text-sm font-semibold text-secondary-foreground hover:bg-accent aria-pressed:bg-emerald-600 aria-pressed:text-white"
    >
      {pinned ? 'Unpin from sidebar' : 'Pin to sidebar'}
    </button>
  )
}