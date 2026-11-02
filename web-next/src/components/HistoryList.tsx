'use client'

import Link from 'next/link'
import { PagedList } from '@/components/PagedList'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDateTime } from '@/lib/dates'
import type { HistoryFilterValues } from '@/lib/historyParsers'
import { formatCents } from '@/lib/money'
import type { HistoryItem, Page } from '@/lib/paged'

// Dates stay as YYYY-MM-DD here. The Route Handler converts them (through bff.ts), never the browser.
function search(f: HistoryFilterValues, pageToken: string) {
  const p = new URLSearchParams({ sort: f.sort })
  if (f.member) p.set('member', f.member)
  if (f.from) p.set('from', f.from)
  if (f.to) p.set('to', f.to)
  if (pageToken) p.set('pageToken', pageToken)
  return p.toString()
}

type Props = { stokvelId: string; filters: HistoryFilterValues; initialPage: Page<HistoryItem> }

export function HistoryList({ stokvelId, filters, initialPage }: Props) {
  const filtered = filters.member !== '' || filters.from !== null || filters.to !== null

  return (
    <PagedList<HistoryItem>
      queryKey={['history', stokvelId, filters]}
      urlFor={(token) => `/bff/stokvels/${stokvelId}/history?${search(filters, token)}`}
      initialPage={initialPage}
      getKey={(c) => c.id}
      nouns={['contribution', 'contributions']}
      empty={
        <div className="space-y-2 rounded-3xl border border-dashed p-6">
          <p>{filtered ? 'No contributions match these filters.' : 'No contributions have been recorded yet.'}</p>
          {filtered && (
            <Link href={`/stokvels/${stokvelId}/contributions`} className="font-medium underline">
              Show everything
            </Link>
          )}
        </div>
      }
      renderItem={(c) => (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-card px-3 py-3">
          <div className="min-w-0">
            <p className="break-words font-medium">{c.memberName}</p>
            <p className="text-sm text-muted-foreground">
              Cycle {c.cycleLabel}, {formatDateTime(c.recordedAt)}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <span className="whitespace-nowrap font-medium tabular-nums">{formatCents(c.amountCents)}</span>
            <StatusBadge kind="contribution" status={String(c.status)} />
          </div>
        </div>
      )}
    />
  )
}