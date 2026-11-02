import Link from 'next/link'
import { notFound, redirect } from 'next/navigation'
import { ArrowLeft } from 'lucide-react'
import { ErrorView } from '@/components/ErrorView'
import { HistoryFilters } from '@/components/HistoryFilters'
import { HistoryList } from '@/components/HistoryList'
import { readOrFail, signedInApi } from '@/lib/api'
import { ApiError } from '@/lib/apiError'
import { fetchHistoryPage, loadMembers } from '@/lib/bff'
import { describeError } from '@/lib/errorMessages'
import { historyLoader } from '@/lib/historyParsers'
import { canRecordForOthers } from '@/lib/roles'

type Props = {
  params: Promise<{ id: string }>
  searchParams: Promise<Record<string, string | string[] | undefined>>
}

export async function ContributionHistory({ params, searchParams }: Props) {
  const { id } = await params
  // Nonsense in the URL falls back to the defaults here: no error page, no 400 shown.
  const filters = await historyLoader(searchParams)
  const { api, session } = await signedInApi()

  const stokvel = readOrFail(await api.GET('/api/v1/stokvels/{id}', { params: { path: { id } } }))
  if (stokvel instanceof ApiError) return <ErrorView description={describeError(stokvel)} />

  const [members, first] = await Promise.all([
    canRecordForOthers(stokvel.role) ? loadMembers(session, id) : Promise.resolve(null),
    fetchHistoryPage(session, id, filters, ''),
  ])

  if (!first.ok) {
    if (first.status === 401) redirect('/login?expired=1')
    if (first.status === 404) notFound()
    if (first.status === 403) {
      return (
        <div className="space-y-2 rounded-3xl border border-dashed p-6">
          <p>You can only see your own contributions in this stokvel.</p>
          <Link href={`/stokvels/${id}/contributions`} className="font-medium underline">
            Show my contributions
          </Link>
        </div>
      )
    }
    return (
      <ErrorView
        description={{
          kind: 'generic',
          title: 'Something went wrong',
          message: 'We could not load the contributions. Try again, and quote the reference if it keeps happening.',
          reference: first.reference,
        }}
      />
    )
  }

  return (
    <section className="space-y-4">
      <Link
        href={`/stokvels/${id}`}
        aria-label="Back to the stokvel"
        className="inline-flex size-11 items-center justify-center rounded-full bg-secondary text-secondary-foreground hover:bg-accent"
      >
        <ArrowLeft aria-hidden="true" className="size-5" />
      </Link>
      <h1 className="break-words text-2xl font-bold tracking-tight">{stokvel.name}: contribution history</h1>

      <HistoryFilters members={members} />

      {/* A new key starts the list again from page one: nothing from the old filter can remain. */}
      <HistoryList key={JSON.stringify(filters)} stokvelId={id} filters={filters} initialPage={first.data} />
    </section>
  )
}