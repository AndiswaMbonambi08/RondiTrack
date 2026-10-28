import Link from 'next/link'
import { ErrorView } from '@/components/ErrorView'
import { StokvelCard } from '@/components/StokvelCard'
import { readOrFail, signedInApi } from '@/lib/api'
import { ApiError } from '@/lib/apiError'
import { describeError } from '@/lib/errorMessages'

export default async function StokvelsPage() {
  const { api } = await signedInApi()
  const home = readOrFail(await api.GET('/api/v1/me/home'))
  if (home instanceof ApiError) return <ErrorView description={describeError(home)} />

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">My stokvels</h1>
      {home.stokvels.length === 0 ? (
        <p className="rounded-lg border p-4">You are not in any stokvels yet.</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2">
          {home.stokvels.map((s) => (
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