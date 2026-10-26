import { useState, type ReactNode } from 'react'
import { useContributions, useCycles, useHome, useMe, useStokvel, useStokvels } from '../api/queries'
import { logout } from '../auth/session'
import { ErrorView } from '../components/ErrorView'
import { ThemeToggle } from '../components/ThemeToggle'
import { Wordmark } from '../components/Wordmark'
import { ContributionForm } from '../contributions/ContributionForm'
import { formatCents, formatDay } from '../lib/money'

const card = 'rounded-3xl bg-white p-5 shadow-sm dark:bg-slate-900'
const muted = 'text-slate-500 dark:text-slate-400'
const pulse = 'animate-pulse rounded bg-slate-200 dark:bg-slate-800'

const FLASH = ['ring-2', 'ring-emerald-500', 'ring-offset-4', 'rounded-3xl']
const go = (id: string) => {
  const el = document.getElementById(id)
  if (!el) return
  el.scrollIntoView({ behavior: 'smooth', block: 'start' })
  el.classList.add(...FLASH)
  window.setTimeout(() => el.classList.remove(...FLASH), 1200)
}
const initials = (name: string) => name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]).join('').toUpperCase()

function statusTone(status: string) {
  const s = status.toLowerCase()
  if (s.includes('paid') || s.includes('confirm') || s.includes('complete')) return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900 dark:text-emerald-100'
  if (s.includes('pend') || s.includes('due')) return 'bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-100'
  if (s.includes('late') || s.includes('miss') || s.includes('overdue')) return 'bg-rose-100 text-rose-800 dark:bg-rose-900 dark:text-rose-100'
  return 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200'
}

function Hero({ activeId }: { activeId: string | undefined }) {
  const home = useHome()
  const entry = activeId ? home.data?.stokvels.find((s) => s.stokvelId === activeId) : undefined

  return (
    <header className="rounded-b-[2.5rem] bg-emerald-200 pb-8 text-emerald-950 dark:bg-emerald-900 dark:text-emerald-50">
      <div className="flex flex-wrap items-center justify-between gap-3 px-6 py-3">
        <h1><Wordmark className="text-xl" /></h1>
        <div className="flex items-center gap-3">
          {/* The toggle is only an icon: any background or border it brings is switched off here. */}
          <span className="inline-flex [&_button]:border-0! [&_button]:bg-transparent! [&_button]:shadow-none!">
            <ThemeToggle />
          </span>
          <button className="rounded bg-emerald-600 px-3 py-1.5 text-sm text-white hover:bg-emerald-700" onClick={() => void logout()}>
            Sign out
          </button>
        </div>
      </div>

      <div className="px-6 pt-4">
        <p className="text-sm opacity-80">Paid this cycle</p>
        {entry ? (
          <p className="text-4xl font-bold tabular-nums">{formatCents(entry.paidCents)}</p>
        ) : (
          <div className={`${pulse} mt-1 h-10 w-44 bg-emerald-300 dark:bg-emerald-800`} />
        )}
        {entry && entry.owedCents > 0 && (
          <span className="mt-2 inline-block rounded-full bg-emerald-300 px-3 py-1 text-xs dark:bg-emerald-800">
            Still owed {formatCents(entry.owedCents)}
          </span>
        )}

        <div className="mt-6 flex gap-3">
          <button type="button" onClick={() => go('record')} className="flex-1 rounded-full bg-emerald-600 px-4 py-3 text-sm font-semibold text-white hover:bg-emerald-700">
            Record contribution
          </button>
          <button type="button" onClick={() => go('contributions')} className="rounded-full bg-emerald-300 px-5 py-3 text-sm font-medium hover:bg-emerald-400 dark:bg-emerald-800 dark:hover:bg-emerald-700">
            Contributions
          </button>
        </div>
      </div>
    </header>
  )
}

function RowsSkeleton() {
  return (
    <ul data-testid="stokvel-skeleton" aria-busy="true" className="mt-4 space-y-4">
      {[0, 1, 2].map((i) => (
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
  const cycles = useCycles(stokvelId)
  const home = useHome()
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

  const rows = contributions.data?.pages.flatMap((p) => p.items ?? []) ?? []
  const statuses = ['All', ...new Set(rows.map((r) => String(r.status)))]
  const active = statuses.includes(filter) ? filter : 'All'
  const shown = active === 'All' ? rows : rows.filter((r) => String(r.status) === active)
  const total = shown.reduce((sum, r) => sum + r.amountCents, 0)

  const currentCycleId = home.data?.stokvels.find((s) => s.stokvelId === stokvelId)?.currentCycle?.id
  const cycle = cycles.data?.find((c) => c.id === currentCycleId)
  // A fraction from 0 to 1, drawn with scaleX so no money arithmetic happens here.
  const ratio = cycle && cycle.amountDueCents > 0 ? Math.min(1, cycle.collectedCents / cycle.amountDueCents) : 0

  return (
    <section className="mt-6 space-y-4">
      <div className={card}>
        <div className="flex items-start justify-between gap-3">
          <div>
            {stokvel.data
              ? <h2 className="text-xl font-semibold">{stokvel.data.name}</h2>
              : <div className={`${pulse} h-6 w-48`} />}
            {contributions.data
              ? <p className={`text-sm ${muted}`}>{shown.length} {shown.length === 1 ? 'contribution' : 'contributions'}</p>
              : <div className={`${pulse} mt-2 h-4 w-24`} />}
          </div>
          {stokvel.data && (
            <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-medium text-emerald-800 dark:bg-emerald-900 dark:text-emerald-100">
              {stokvel.data.role}
            </span>
          )}
        </div>

        {cycle && (
          <div className="mt-4">
            <div className="flex justify-between text-sm">
              <span className={muted}>Cycle {cycle.label}</span>
              <span className="font-medium tabular-nums">{formatCents(cycle.collectedCents)} of {formatCents(cycle.amountDueCents)}</span>
            </div>
            <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
              <div className="h-full origin-left rounded-full bg-emerald-500" style={{ transform: `scaleX(${ratio})` }} />
            </div>
          </div>
        )}

        <div className="mt-4 flex items-end justify-between">
          <p className={`text-xs ${muted}`}>Total shown</p>
          {contributions.data
            ? <p className="text-xl font-bold tabular-nums">{formatCents(total)}</p>
            : <div className={`${pulse} h-6 w-24`} />}
        </div>
      </div>

      <div id="record" className="scroll-mt-4">
        <ContributionForm stokvelId={stokvelId} />
      </div>

      <h3 id="contributions" className="scroll-mt-4 pt-2 text-lg font-semibold">Contributions</h3>

      {!contributions.data ? (
        <RowsSkeleton />
      ) : rows.length === 0 ? (
        <div className="rounded-3xl border border-dashed border-slate-300 p-6 dark:border-slate-700">
          <p className="font-medium">No contributions yet.</p>
          <p className={muted}>Once the treasurer records the first contribution for this cycle, it will show up here.</p>
        </div>
      ) : (
        <>
          <div className="flex flex-wrap gap-2">
            {statuses.map((s) => (
              <button
                key={s}
                onClick={() => setFilter(s)}
                aria-pressed={s === active}
                className={`rounded-full px-4 py-1.5 text-sm ${
                  s === active
                    ? 'bg-emerald-600 text-white'
                    : 'bg-white text-slate-800 hover:bg-emerald-100 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700'
                }`}
              >
                {s === 'All' ? 'Show all' : s}
              </button>
            ))}
          </div>
          <ul className="space-y-4">
            {shown.map((r) => (
              <li key={r.id} className={card}>
                <div className="flex items-start justify-between gap-2">
                  <p className="font-semibold">{r.cycleLabel}</p>
                  <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${statusTone(String(r.status))}`}>{String(r.status)}</span>
                </div>
                <div className="mt-3 flex items-end justify-between">
                  <p className="text-2xl font-bold tabular-nums">{formatCents(r.amountCents)}</p>
                  <span className={`text-xs ${muted}`}>{formatDay(r.recordedAt)}</span>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}

      {contributions.hasNextPage && (
        <button
          className="rounded-full bg-white px-5 py-2 text-sm shadow-sm hover:bg-emerald-100 disabled:opacity-50 dark:bg-slate-800 dark:hover:bg-slate-700"
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

  const switcher = list && list.length > 0 && (
    <div className="mt-6 rounded-3xl bg-white p-5 shadow-sm dark:bg-slate-900">
      <h2 className="mb-4 text-base font-semibold">Your stokvels</h2>
      <div role="group" aria-label="Stokvel" className="grid grid-cols-4 gap-3">
        {list.map((s) => (
          <button
            key={s.id}
            type="button"
            aria-pressed={s.id === activeId}
            onClick={() => setSelectedId(s.id)}
            className="group flex flex-col items-center gap-1.5 text-center"
          >
            <span
              aria-hidden="true"
              className={`flex h-14 w-14 items-center justify-center rounded-full text-sm font-bold transition ${
                s.id === activeId
                  ? 'bg-emerald-600 text-white'
                  : 'bg-emerald-100 text-emerald-800 group-hover:bg-emerald-200 dark:bg-slate-800 dark:text-emerald-200'
              }`}
            >
              {initials(s.name)}
            </span>
            <span className="line-clamp-2 text-xs">{s.name}</span>
          </button>
        ))}
      </div>
    </div>
  )

  let body: ReactNode
  if (stokvels.isPending) body = <RowsSkeleton />
  else if (stokvels.isError) body = <ErrorView error={stokvels.error} onRetry={() => stokvels.refetch()} />
  else if (!activeId) {
    body = (
      <div className="mt-6 rounded-3xl border border-dashed border-slate-300 p-6 dark:border-slate-700">
        <p className="font-medium">You're not in a stokvel yet.</p>
        <p className={muted}>Ask your treasurer to add you, then come back.</p>
      </div>
    )
  } else {
    // No key={activeId}: one panel whose id prop changes, so the switch proof test is meaningful.
    body = <StokvelPanel stokvelId={activeId} onBack={() => { setSelectedId(null); void stokvels.refetch() }} />
  }

  return (
    <div className="min-h-screen bg-emerald-100 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <div className="mx-auto min-h-screen max-w-xl bg-emerald-50 pb-8 dark:bg-slate-950">
        <Hero activeId={activeId} />
        <main className="px-4">
          {switcher}
          {body}
        </main>
      </div>
    </div>
  )
}