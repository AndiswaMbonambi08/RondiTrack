import { Suspense } from 'react'
import Link from 'next/link'
import { ArrowLeft } from 'lucide-react'
import { ContributionSection } from '@/components/ContributionSection'
import { ErrorView } from '@/components/ErrorView'
import { RecentContributions, RecentSkeleton, startRecent } from '@/components/RecentContributions'
import { PinButton } from '@/components/PinButton'
import { StatusBadge } from '@/components/StatusBadge'
import { readOrFail, signedInApi } from '@/lib/api'
import { ApiError } from '@/lib/apiError'
import { describeError } from '@/lib/errorMessages'
import { formatCents } from '@/lib/money'
import { CloseCycleButton } from '@/components/CloseCycleButton'
import { InviteButton } from '@/components/InviteButton'
import { canRecordForOthers, roleText } from '@/lib/roles'
import { nowMs } from '@/lib/timing'

export async function StokvelContent({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  const { api, session } = await signedInApi()

  // The detail answer already carries the member count and the current cycle (8.1, question 6), so
  // the requests that are really independent are: the stokvel, the caller's own figures
  // (/me/home) and the recent contributions. FETCH_MODE=sequential awaits them one per line,
  // only to measure the difference.
  const sequential = process.env.FETCH_MODE === 'sequential'
  const t0 = nowMs()

  const detailReq = () => api.GET('/api/v1/stokvels/{id}', { params: { path: { id } } })
  const homeReq = () => api.GET('/api/v1/me/home')

  const recentEarly = sequential ? null : startRecent(api, id) // starts NOW, with the others
  const [detailRes, homeRes] = sequential
    ? ([await detailReq(), await homeReq()] as const)
    : await Promise.all([detailReq(), homeReq()])
  const recent = recentEarly ?? Promise.resolve(await startRecent(api, id))

  console.log(`[stokvel ${sequential ? 'sequential' : 'parallel'}] top of the page ready in ${Math.round(nowMs() - t0)} ms`)

  // A 404 (not a member, no such stokvel, malformed id) becomes notFound() inside readOrFail.
  const stokvel = readOrFail(detailRes)
  if (stokvel instanceof ApiError) return <ErrorView description={describeError(stokvel)} />
  const home = readOrFail(homeRes)
  if (home instanceof ApiError) return <ErrorView description={describeError(home)} />

  const mine = home.stokvels.find((s) => s.stokvelId === id)
  const cycle = stokvel.currentCycle

  return (
    <section className="space-y-4">
      <Link
        href="/stokvels"
        aria-label="Back to my stokvels"
        className="inline-flex size-11 items-center justify-center rounded-full bg-secondary text-secondary-foreground hover:bg-accent"
      >
        <ArrowLeft aria-hidden="true" className="size-5" />
      </Link>

      <div className="max-w-xl space-y-3 rounded-3xl bg-card p-5 shadow-sm">
        <h1 className="break-words text-2xl font-bold tracking-tight">{stokvel.name}</h1>
        <div className="flex flex-wrap gap-2">
          <PinButton stokvelId={id} name={stokvel.name} />
          <Link
            href={`/stokvels/${id}/contributions`}
            className="inline-flex min-h-11 items-center rounded-full bg-primary px-5 text-sm font-semibold text-primary-foreground hover:bg-primary/80"
          >
            Contribution history
          </Link>
        </div>
        <p className="text-muted-foreground">{roleText(stokvel.role)}</p>

        <dl className="grid max-w-md grid-cols-2 gap-2 text-sm">
          <dt>Contribution</dt>
          <dd className="whitespace-nowrap font-semibold tabular-nums">{formatCents(stokvel.contributionCents)}</dd>
          <dt>Members</dt>
          <dd className="font-semibold">{stokvel.memberCount}</dd>
        </dl>
      </div>

      <div className="max-w-xl space-y-2 rounded-3xl bg-card p-5 text-sm shadow-sm">
        <h2 className="text-lg font-semibold">Current cycle</h2>
        {cycle ? (
          <>
            <p className="flex flex-wrap items-center gap-2">
              <span>{cycle.label}</span>
              <StatusBadge kind="cycle" status={cycle.status} />
            </p>
            <p>
              Paid so far: {cycle.contributorCount} of {stokvel.memberCount} members
            </p>
            <p>
              Collected:{' '}
              <span className="whitespace-nowrap font-semibold tabular-nums">{formatCents(cycle.collectedCents)}</span>
            </p>
          </>
        ) : (
          <p>No open cycle</p>
        )}
      </div>

      {/* Only a treasurer of THIS stokvel sees these. The API refuses anyone else who sends the request anyway. */}
      {canRecordForOthers(stokvel.role) && (
        <div className="max-w-xl space-y-3 rounded-3xl bg-card p-5 shadow-sm">
          <h2 className="text-lg font-semibold">Treasurer actions</h2>
          {cycle && cycle.status === 'Open' && (
            <CloseCycleButton stokvelId={id} cycleId={cycle.id} cycleLabel={cycle.label} />
          )}
          <InviteButton stokvelId={id} />
        </div>
      )}

      {/* The form's own boundary: the member list (Treasurer only) never holds up the top. */}
      <Suspense fallback={<div className="h-48 max-w-xl animate-pulse rounded-3xl bg-muted" aria-busy="true" />}>
        <ContributionSection
          stokvelId={id}
          role={stokvel.role}
          cycle={cycle ? { id: cycle.id, label: cycle.label, status: cycle.status } : null}
          contributionCents={stokvel.contributionCents}
          owedCents={mine?.owedCents ?? 0}
          selfId={session.userId}
          selfName={session.fullName}
        />
      </Suspense>

      <Suspense fallback={<RecentSkeleton />}>
        <RecentContributions result={recent} startedAt={t0} />
      </Suspense>
    </section>
  )
}