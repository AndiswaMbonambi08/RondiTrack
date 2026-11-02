'use client'

import { useTransition } from 'react'
import { useQueryStates } from 'nuqs'
import { historyParsers } from '@/lib/historyParsers'
import type { MemberOption } from '@/lib/paged'

const field =
  'mt-1 w-full rounded-xl border border-input bg-background px-3 py-2 text-foreground focus-visible:outline-2 focus-visible:outline-ring'

export function HistoryFilters({ members }: { members: MemberOption[] | null }) {
  const [isPending, startTransition] = useTransition()
  // shallow: false asks the server to render the page again with the new URL.
  // push: each filter change is a step the person may want to go Back from.
  const [filters, setFilters] = useQueryStates(historyParsers, {
    shallow: false,
    history: 'push',
    clearOnDefault: true,
    startTransition,
  })

  return (
    <div className="space-y-3 rounded-3xl bg-card p-5 shadow-sm">
      <h2 className="text-lg font-semibold">Filters</h2>
      <div className="grid gap-3 sm:grid-cols-2">
        {members && (
          <div>
            <label htmlFor="member" className="text-sm font-medium">Member</label>
            <select
              id="member"
              className={field}
              value={filters.member}
              onChange={(e) => void setFilters({ member: e.target.value })}
            >
              <option value="">Everyone</option>
              {members.map((m) => (
                <option key={m.userId} value={m.userId}>
                  {m.fullName || m.userId}
                </option>
              ))}
            </select>
          </div>
        )}
        <div>
          <label htmlFor="sort" className="text-sm font-medium">Order</label>
          <select
            id="sort"
            className={field}
            value={filters.sort}
            onChange={(e) => void setFilters({ sort: e.target.value === 'oldest' ? 'oldest' : 'newest' })}
          >
            <option value="newest">Newest first</option>
            <option value="oldest">Oldest first</option>
          </select>
        </div>
        <div>
          <label htmlFor="from" className="text-sm font-medium">From (South African date)</label>
          <input
            id="from"
            type="date"
            className={field}
            value={filters.from ?? ''}
            onChange={(e) => void setFilters({ from: e.target.value || null })}
          />
        </div>
        <div>
          <label htmlFor="to" className="text-sm font-medium">To (South African date, included)</label>
          <input
            id="to"
            type="date"
            className={field}
            value={filters.to ?? ''}
            onChange={(e) => void setFilters({ to: e.target.value || null })}
          />
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={() => void setFilters(null)}
          className="min-h-11 rounded-full bg-secondary px-5 py-2 text-sm font-semibold text-secondary-foreground hover:bg-accent"
        >
          Clear filters
        </button>
        <p role="status" className="min-h-5 text-sm text-muted-foreground">
          {isPending ? 'Updating the list...' : ''}
        </p>
      </div>
    </div>
  )
}