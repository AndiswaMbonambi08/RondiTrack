import Link from 'next/link'
import { ArrowLeft } from 'lucide-react'
import { ErrorView } from '@/components/ErrorView'
import { StatusBadge } from '@/components/StatusBadge'
import { readOrFail, signedInApi } from '@/lib/api'
import { ApiError } from '@/lib/apiError'
import { describeError } from '@/lib/errorMessages'
import { formatCents } from '@/lib/money'
import { roleText } from '@/lib/roles'

export default async function StokvelPage({ params }: PageProps<'/stokvels/[id]'>) {
  const { id } = await params
  const { api } = await signedInApi()

  // A 404 (not a member, no such stokvel, malformed id) becomes notFound() inside readOrFail.
  const stokvel = readOrFail(await api.GET('/api/v1/stokvels/{id}', { params: { path: { id } } }))
  if (stokvel instanceof ApiError) return <ErrorView description={describeError(stokvel)} />

  const cycle = stokvel.currentCycle

  return (
    <section className="space-y-4">
      {/* Arrow only. aria-label is invisible and is only read out by screen readers. */}
      <Link
        href="/stokvels"
        aria-label="Back to my stokvels"
        className="inline-flex size-11 items-center justify-center rounded-full bg-secondary text-secondary-foreground hover:bg-accent"
      >
        <ArrowLeft aria-hidden="true" className="size-5" />
      </Link>

      <div className="max-w-xl space-y-3 rounded-3xl bg-card p-5 shadow-sm">
        <h1 className="break-words text-2xl font-bold tracking-tight">{stokvel.name}</h1>
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
    </section>
  )
}