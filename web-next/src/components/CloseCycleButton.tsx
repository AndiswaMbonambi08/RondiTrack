'use client'

import { useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import { AlertDialog } from 'radix-ui'
import { closeCycle } from '@/app/actions/close'
import { useToast } from '@/components/ToastProvider'
import type { ClosePreview } from '@/lib/closeTypes'
import { formatCalendarDate } from '@/lib/dates'
import { formatCents } from '@/lib/money'

type Phase = 'idle' | 'loading' | 'ready' | 'error'
type Props = { stokvelId: string; cycleId: string; cycleLabel: string }

const quiet = 'min-h-11 rounded-full border px-5 py-2 text-sm font-semibold disabled:opacity-60'

export function CloseCycleButton({ stokvelId, cycleId, cycleLabel }: Props) {
  const router = useRouter()
  const toast = useToast()
  const [open, setOpen] = useState(false)
  const [phase, setPhase] = useState<Phase>('idle')
  const [preview, setPreview] = useState<ClosePreview | null>(null)
  const [closing, setClosing] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  // One Idempotency-Key per preview: a retry of the same close reuses it, a new preview is a new intent.
  const key = useRef('')

  async function loadPreview(message: string | null) {
    setPhase('loading')
    setPreview(null)
    setNotice(message)
    try {
      const res = await fetch(`/bff/stokvels/${stokvelId}/cycles/${cycleId}/close-preview`, { cache: 'no-store' })
      if (res.status === 401) {
        router.push('/login?expired=1')
        return
      }
      if (res.status === 403) {
        setNotice('Only a treasurer of this stokvel can close a cycle.')
        setPhase('error')
        return
      }
      if (res.status === 409) {
        setNotice('This cycle has already been closed.')
        setPhase('error')
        return
      }
      if (!res.ok) {
        setNotice('We could not load the preview. Try again.')
        setPhase('error')
        return
      }
      setPreview((await res.json()) as ClosePreview)
      key.current = crypto.randomUUID()
      setPhase('ready')
    } catch {
      setNotice('We could not reach RondiTrack. Try again.')
      setPhase('error')
    }
  }

  function onOpenChange(next: boolean) {
    if (closing) return // the dialog stays open while the close runs
    setOpen(next)
    if (next) void loadPreview(null)
  }

  async function confirm() {
    if (!preview || closing) return
    setClosing(true)
    setNotice(null)
    try {
      const result = await closeCycle({ stokvelId, cycleId, version: preview.version, key: key.current })
      switch (result.status) {
        case 'closed':
          setOpen(false)
          toast.success(`Closed ${cycleLabel}.`)
          router.refresh()
          break
        case 'stale':
          // What the treasurer saw is not what would happen now: show the new preview to confirm again.
          await loadPreview('Something changed since you opened this. Check the new numbers, then confirm again.')
          break
        case 'failed':
          toast.failure(result.message, result.reference)
          break
        default:
          setNotice(result.message)
      }
    } catch {
      toast.failure('The request did not go through. Trying again is safe: the cycle closes once.')
    } finally {
      setClosing(false)
    }
  }

  const total = preview?.totalArrearsCents ?? 0
  const confirmLabel = closing
    ? 'Closing...'
    : phase === 'ready' && preview
      ? total > 0
        ? `Close ${cycleLabel} and record ${formatCents(total)} in arrears`
        : `Close ${cycleLabel}, no arrears to record`
      : `Close ${cycleLabel}`

  return (
    <AlertDialog.Root open={open} onOpenChange={onOpenChange}>
      <AlertDialog.Trigger asChild>
        <button
          type="button"
          className="min-h-11 rounded-full bg-primary px-5 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/80"
        >
          Close {cycleLabel}
        </button>
      </AlertDialog.Trigger>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <AlertDialog.Content className="fixed left-1/2 top-1/2 z-50 max-h-[85vh] w-[90vw] max-w-md -translate-x-1/2 -translate-y-1/2 space-y-4 overflow-y-auto rounded-3xl bg-card p-6 text-card-foreground shadow-xl">
          <AlertDialog.Title className="text-xl font-bold">Close {cycleLabel}?</AlertDialog.Title>
          <AlertDialog.Description className="text-sm">
            This cannot be undone. Closing records arrears for everyone who has not paid in full, exactly as listed here.
          </AlertDialog.Description>

          {phase === 'loading' && (
            <div aria-busy="true" aria-label="Loading the preview" className="space-y-2">
              {[0, 1, 2].map((i) => (
                <div key={i} className="flex justify-between gap-3">
                  <div className="h-5 w-36 animate-pulse rounded bg-muted" />
                  <div className="h-5 w-20 animate-pulse rounded bg-muted" />
                </div>
              ))}
            </div>
          )}

          {phase === 'ready' && preview && (
            <div className="space-y-2 text-sm">
              {preview.items.length === 0 ? (
                <p className="font-medium">Nobody goes into arrears. Everyone has paid in full.</p>
              ) : (
                <>
                  <ul className="space-y-1">
                    {preview.items.map((i) => (
                      <li key={i.memberId} className="flex justify-between gap-3">
                        <span className="break-words">{i.memberName}</span>
                        <span className="whitespace-nowrap font-medium tabular-nums">{formatCents(i.shortfallCents)}</span>
                      </li>
                    ))}
                  </ul>
                  <p className="flex justify-between gap-3 border-t pt-2 font-semibold">
                    <span>Total arrears</span>
                    <span className="whitespace-nowrap tabular-nums">{formatCents(preview.totalArrearsCents)}</span>
                  </p>
                </>
              )}
              <p className="text-muted-foreground">Counted up to {formatCalendarDate(preview.asOf)} (South African time).</p>
            </div>
          )}

          {notice && (
            <p role="alert" className="text-sm text-red-700 dark:text-red-300">
              {notice}
            </p>
          )}

          <div className="flex flex-wrap justify-end gap-2">
            {phase === 'error' && (
              <button type="button" onClick={() => void loadPreview(null)} className={quiet}>
                Try again
              </button>
            )}
            <AlertDialog.Cancel disabled={closing} className={quiet}>
              Cancel
            </AlertDialog.Cancel>
            <button
              type="button"
              onClick={() => void confirm()}
              disabled={phase !== 'ready' || closing}
              className="min-h-11 rounded-full bg-rose-700 px-5 py-2 text-sm font-semibold text-white hover:bg-rose-800 disabled:opacity-50"
            >
              {confirmLabel}
            </button>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  )
}