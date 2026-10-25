import { useState, type ReactNode } from 'react'
import { useContributions, useStokvel, useStokvels } from '../api/queries'
import { logout } from '../auth/session'
import { ErrorView } from '../components/ErrorView'
import { Wordmark } from '../components/Wordmark'
import { ThemeToggle } from '../components/ThemeToggle'
import { ContributionForm } from '../contributions/ContributionForm'
import { formatCents, formatDay } from '../lib/money'

const card = 'rounded-lg bg-white p-4 shadow-sm dark:bg-slate-900'
const muted = 'text-slate-500 dark:text-slate-400'
const pulse = 'animate-pulse rounded bg-slate-200 dark:bg-slate-800'

function statusTone(status: string) {
  const s = status.toLowerCase()
  if (s.includes('paid') || s.includes('confirm') || s.includes('complete')) return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900 dark:text-emerald-100'
  if (s.includes('pend') || s.includes('due')) return 'bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-100'
  if (s.includes('late') || s.includes('miss') || s.includes('overdue')) return 'bg-rose-100 text-rose-800 dark:bg-rose-900 dark:text-rose-100'
  return 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200'
}

function Header({ children }: { children?: ReactNode }) {
  return (
    <header className="bg-emerald-200 text-emerald-950 dark:bg-emerald-900 dark:text-emerald-50">
      <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-6 py-3">
        <h1><Wordmark className="text-xl" /></h1>
        <div className="flex items-center gap-3">
          {children}
          <ThemeToggle />
          <button className="rounded bg-emerald-600 px-3 py-1.5 text-sm text-white hover:bg-emerald-700" onClick={() => void logout()}>
            Sign out
          </button>
        </div>
      </div>
    </header>
  )
}

function RowsSkeleton() {
  return (
    <ul data-testid="stokvel-skeleton" aria-busy="true" className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {[0, 1, 2, 3, 4, 5].map((i) => (
        <li key={i} className={`${card} space-y-3`}>
          <div className={`${pulse} h-4 w-2/3`} />
          <div className={`${pulse} h-4 w-1/3`} />
        </li>
      ))}
    </ul>
  )
}

function StokvelPanel({ stokvelId, onBack }: { stokvelId: string; onBack: () => void }) {
  const stokvel = useStokvel(stokvelId)
  const contributions = useContributions(stokvelId)
  const [filter, setFilter] = useState('All')

  const error = stokvel.error ?? contributions.error
  if (error) {
    return (
      <ErrorView
        error={error}
        onBack={onBack}
        onRetry={() => { void stokvel.refetch(); void contributions.refetch() }}
      />
    )
  }

  // Each part shows its own data or its own skeleton. Nothing is carried over from another stokvel.
  const rows = contributions.data?.pages.flatMap((p) => p.items ?? []) ?? []
  const statuses = ['All', ...new Set(rows.map((r) => String(r.status)))]
  const active = statuses.includes(filter) ? filter : 'All'
  const shown = active === 'All' ? rows : rows.filter((r) => String(r.status) === active)
  const total = shown.reduce((sum, r) => sum + r.amountCents, 0)

  return (
    <section>
      <div className={`${card} flex items-center justify-between gap-4`}>
        <div>
          {stokvel.data
            ? <h2 className="text-xl font-semibold">{stokvel.data.name}</h2>
            : <div className={`${pulse} h-6 w-48`} />}
          {contributions.data
            ? <p className={`text-sm ${muted}`}>{shown.length} {shown.length === 1 ? 'contribution' : 'contributions'}</p>
            : <div className={`${pulse} mt-2 h-4 w-24`} />}
        </div>
        <div className="text-right">
          <p className={`text-xs ${muted}`}>Total shown</p>
          {contributions.data
            ? <p className="text-xl font-bold tabular-nums">{formatCents(total)}</p>
            : <div className={`${pulse} ml-auto mt-1 h-6 w-24`} />}
        </div>
      </div>

      <div className="mt-4">
        <ContributionForm stokvelId={stokvelId} />
      </div>

      {!contributions.data ? (
        <RowsSkeleton />
      ) : rows.length === 0 ? (
        <div className="mt-4 rounded-lg border border-dashed border-slate-300 p-6 dark:border-slate-700">
          <p className="font-medium">No contributions yet.</p>
          <p className={muted}>Once the treasurer records the first contribution for this cycle, it will show up here.</p>
        </div>
      ) : (
        <>
          <div className="mt-4 flex flex-wrap gap-2">
            {statuses.map((s) => (
              <button
                key={s}
                onClick={() => setFilter(s)}
                aria-pressed={s === active}
                className={`rounded px-3 py-1.5 text-sm ${
                  s === active
                    ? 'bg-indigo-600 text-white'
                    : 'bg-slate-200 text-slate-800 hover:bg-slate-300 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700'
                }`}
              >
                {s === 'All' ? 'Show all' : s}
              </button>
            ))}
          </div>
          <ul className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {shown.map((r) => (
              <li key={r.id} className={card}>
                <div className="flex items-start justify-between gap-2">
                  <p className="font-semibold">{r.cycleLabel}</p>
                  <span className="rounded-full bg-emerald-600 px-2.5 py-0.5 text-xs font-bold tabular-nums text-white">
                    {formatCents(r.amountCents)}
                  </span>
                </div>
                <div className="mt-3 flex items-center justify-between text-xs">
                  <span className={`rounded-full px-2 py-0.5 ${statusTone(String(r.status))}`}>{String(r.status)}</span>
                  <span className={muted}>{formatDay(r.recordedAt)}</span>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}

      {contributions.hasNextPage && (
        <button
          className="mt-4 rounded bg-slate-200 px-3 py-1.5 text-sm hover:bg-slate-300 disabled:opacity-50 dark:bg-slate-800 dark:hover:bg-slate-700"
          onClick={() => contributions.fetchNextPage()}
          disabled={contributions.isFetchingNextPage}
        >
          {contributions.isFetchingNextPage ? 'Loading...' : 'Load more'}
        </button>
      )}
    </section>
  )
}

export function StokvelWorkspace() {
  const stokvels = useStokvels()
  const [selectedId, setSelectedId] = useState<string | null>(null) // in memory only

  const list = stokvels.data
  const activeId = list && list.length > 0 ? (selectedId ?? list[0].id) : undefined

  const switcher = list && list.length > 1 && (
    <div role="group" aria-label="Stokvel" className="mb-4 flex flex-wrap gap-2">
      {list.map((s) => (
        <button
          key={s.id}
          type="button"
          aria-pressed={s.id === activeId}
          onClick={() => setSelectedId(s.id)}
          className={`rounded-full px-4 py-1.5 text-sm font-medium ${
            s.id === activeId
              ? 'bg-emerald-600 text-white'
              : 'bg-white text-slate-800 hover:bg-emerald-50 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700'
          }`}
        >
          {s.name}
        </button>
      ))}
    </div>
  )

  let body: ReactNode
  if (stokvels.isPending) body = <RowsSkeleton />
  else if (stokvels.isError) body = <ErrorView error={stokvels.error} onRetry={() => stokvels.refetch()} />
  else if (!activeId) {
    body = (
      <div className="rounded-lg border border-dashed border-slate-300 p-6 dark:border-slate-700">
        <p className="font-medium">You're not in a stokvel yet.</p>
        <p className={muted}>Ask your treasurer to add you, then come back.</p>
      </div>
    )
  } else {
    // No key={activeId}: one panel whose id prop changes, so the switch proof test is meaningful.
    body = <StokvelPanel stokvelId={activeId} onBack={() => { setSelectedId(null); void stokvels.refetch() }} />
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <Header />
      <main className="mx-auto max-w-5xl p-6">{switcher}{body}</main>
    </div>
  )
}
