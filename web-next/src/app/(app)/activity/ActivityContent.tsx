import { redirect } from 'next/navigation'
import { ActivityFeed } from '@/components/ActivityFeed'
import { ErrorView } from '@/components/ErrorView'
import { signedInApi } from '@/lib/api'
import { fetchActivityPage } from '@/lib/bff'

export async function ActivityContent() {
  const { session } = await signedInApi()
  const first = await fetchActivityPage(session, '')

  if (!first.ok) {
    if (first.status === 401) redirect('/login?expired=1')
    return (
      <ErrorView
        description={{
          kind: 'generic',
          title: 'Something went wrong',
          message: 'We could not load your activity. Try again, and quote the reference if it keeps happening.',
          reference: first.reference,
        }}
      />
    )
  }

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold tracking-tight">Activity</h1>
      <p className="text-sm text-muted-foreground">Recent activity in every stokvel you belong to, newest first.</p>
      <ActivityFeed initialPage={first.data} />
    </section>
  )
}