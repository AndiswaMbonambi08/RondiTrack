'use client'

import { PagedList } from '@/components/PagedList'
import { formatDateTime } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import type { ActivityItem, Page } from '@/lib/paged'

function sentence(a: ActivityItem): string {
  const who = a.actorName ?? 'A member'
  if (a.kind === 'Contribution') return `${who} contributed`
  if (a.kind === 'Payout') return `Payout to ${a.actorName ?? 'a member'}`
  if (a.kind === 'MemberJoined') return `${who} joined`
  return a.kind
}

export function ActivityFeed({ initialPage }: { initialPage: Page<ActivityItem> }) {
  return (
    <PagedList<ActivityItem>
      queryKey={['activity']}
      urlFor={(token) => `/bff/activity${token ? `?pageToken=${encodeURIComponent(token)}` : ''}`}
      initialPage={initialPage}
      // A joining event has the member's id, so the stokvel and kind are part of the key.
      getKey={(a) => `${a.kind}:${a.stokvelId}:${a.id}`}
      nouns={['item', 'items']}
      empty={<p className="rounded-3xl border border-dashed p-6">No activity yet in your stokvels.</p>}
      renderItem={(a) => (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-card px-3 py-3">
          <div className="min-w-0">
            <p className="break-words font-medium">{sentence(a)}</p>
            <p className="text-sm text-muted-foreground">
              {a.stokvelName}, {formatDateTime(a.occurredAt)}
            </p>
          </div>
          {a.amountCents !== null && (
            <span className="whitespace-nowrap font-medium tabular-nums">{formatCents(a.amountCents)}</span>
          )}
        </div>
      )}
    />
  )
}