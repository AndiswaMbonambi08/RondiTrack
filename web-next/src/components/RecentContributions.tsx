import { ContributionCard } from '@/components/ContributionCard'
import { ErrorView } from '@/components/ErrorView'
import type { signedInApi } from '@/lib/api'
import { readOrFail } from '@/lib/api'
import { ApiError } from '@/lib/apiError'
import { describeError } from '@/lib/errorMessages'
import { nowMs } from '@/lib/timing'

type Api = Awaited<ReturnType<typeof signedInApi>>['api']

// Started at the same moment as the other requests, and only AWAITED inside <RecentContributions>,
// so the top of the page never waits for it. A network failure becomes null, not an unhandled rejection.
export async function startRecent(api: Api, stokvelId: string) {
  try {
    return await api.GET('/api/v1/stokvels/{stokvelId}/contributions', {
      params: { path: { stokvelId }, query: { PageSize: 5 } },
    })
  } catch {
    return null
  }
}

type Recent = Awaited<ReturnType<typeof startRecent>>

export function RecentSkeleton() {
  return (
    <div className="max-w-xl space-y-3" aria-busy="true" aria-label="Loading recent contributions">
      <h2 className="text-lg font-semibold">Recent contributions</h2>
      <ul className="space-y-2">
        {[0, 1, 2].map((i) => (
          <li key={i} className="rounded-lg border bg-card px-3 py-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="space-y-2">
                <div className="h-5 w-24 animate-pulse rounded bg-muted" />
                <div className="h-4 w-36 animate-pulse rounded bg-muted" />
              </div>
              <div className="h-6 w-16 animate-pulse rounded-full bg-muted" />
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}

export async function RecentContributions({ result, startedAt }: { result: Promise<Recent>; startedAt: number }) {
  const r = await result
  console.log(`[stokvel] recent contributions ready ${Math.round(nowMs() - startedAt)} ms after the request started`)

  if (r === null) return <ErrorView description={describeError(new Error('network'))} />
  // 404 -> not-found page (from inside this boundary), 401 -> /login?expired=1.
  const page = readOrFail(r)
  if (page instanceof ApiError) return <ErrorView description={describeError(page)} />

  return (
    <section className="max-w-xl space-y-3" aria-labelledby="recent-heading">
      <h2 id="recent-heading" className="text-lg font-semibold">
        Recent contributions
      </h2>
      {page.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">You have not recorded any contributions here yet.</p>
      ) : (
        <ul className="space-y-2">
          {page.items.map((c) => (
            <li key={c.id} className="space-y-1">
              <p className="text-xs text-muted-foreground">Cycle {c.cycleLabel}</p>
              <ContributionCard contribution={c} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}