import Link from 'next/link'
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
      <Link href="/stokvels" className="text-sm underline">
        Back to my stokvels
      </Link>
      <h1 className="break-words text-2xl font-semibold">{stokvel.name}</h1>
      <p className="text-muted-foreground">{roleText(stokvel.role)}</p>

      <dl className="grid max-w-md grid-cols-2 gap-2 text-sm">
        <dt>Contribution</dt>
        <dd className="whitespace-nowrap">{formatCents(stokvel.contributionCents)}</dd>
        <dt>Members</dt>
        <dd>{stokvel.memberCount}</dd>
      </dl>

      <div className="max-w-md space-y-2 rounded-lg border p-4 text-sm">
        <h2 className="font-semibold">Current cycle</h2>
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
              Collected: <span className="whitespace-nowrap">{formatCents(cycle.collectedCents)}</span>
            </p>
          </>
        ) : (
          <p>No open cycle</p>
        )}
      </div>
    </section>
  )
}