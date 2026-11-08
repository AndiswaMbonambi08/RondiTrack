'use client'

import { useState } from 'react'
import { createInvite } from '@/app/actions/join'
import { stepUrl } from '@/lib/joinMachine'
import { formatDate } from '@/lib/dates'

export function InviteButton({ stokvelId }: { stokvelId: string }) {
  const [link, setLink] = useState<string | null>(null)
  const [expires, setExpires] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function make() {
    setPending(true)
    setError(null)
    try {
      const r = await createInvite(stokvelId)
      if (r.status === 'ok') {
        setLink(`${window.location.origin}${stepUrl('rules', r.code)}`)
        setExpires(r.expiresAtUtc)
      } else {
        setError(r.message)
      }
    } catch {
      setError('We could not create the invite. Try again.')
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="space-y-2">
      <button
        type="button"
        onClick={() => void make()}
        disabled={pending}
        className="min-h-11 rounded-full bg-secondary px-5 py-2 text-sm font-semibold text-secondary-foreground hover:bg-accent disabled:opacity-60"
      >
        {pending ? 'Creating...' : 'Create invite link'}
      </button>
      {error && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{error}</p>}
      {link && (
        <div className="space-y-1">
          <label htmlFor="invite-link" className="text-sm font-medium">Invite link{expires ? `, valid until ${formatDate(expires)}` : ''}</label>
          <input id="invite-link" readOnly value={link} onFocus={(e) => e.currentTarget.select()}
            className="w-full rounded-xl border border-input bg-background px-3 py-2 text-sm" />
        </div>
      )}
    </div>
  )
}