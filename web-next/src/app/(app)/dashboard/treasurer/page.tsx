import Link from 'next/link'
import { ErrorView } from '@/components/ErrorView'
import { StokvelCard } from '@/components/StokvelCard'
import { readOrFail, signedInApi } from '@/lib/api'
import { ApiError } from '@/lib/apiError'
import { describeError } from '@/lib/errorMessages'
import { stokvelsWhereTreasurer } from '@/lib/treasurer'

// Everyone may open this page. It is a filtered view, not a door with a role check:
// it lists the stokvels where THIS person is Treasurer, and for most people that is none.
export default async function TreasurerDashboardPage() {
  const { api } = await signedInApi()
  const home = readOrFail(await api.GET('/api/v1/me/home'))
  if (home instanceof ApiError) return <ErrorView description={describeError(home)} />

  const mine = stokvelsWhereTreasurer(home.stokvels)

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">Treasurer dashboard</h1>
      {mine.length === 0 ? (
        <div className="space-y-2 rounded-lg border p-4">
          <p>You don&rsquo;t run any stokvels yet.</p>
          <Link href="/dashboard/member" className="underline">
            Go to your member dashboard
          </Link>
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2">
          {mine.map((s) => (
            <div key={s.stokvelId} className="space-y-2">
              <StokvelCard entry={s} />
              <Link href={`/stokvels/${s.stokvelId}`} className="text-sm underline">
                View stokvel
              </Link>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}