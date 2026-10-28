import { ErrorView } from '@/components/ErrorView'
import { StokvelCard } from '@/components/StokvelCard'
import { readOrFail, signedInApi } from '@/lib/api'
import { ApiError } from '@/lib/apiError'
import { describeError } from '@/lib/errorMessages'

export default async function MemberDashboardPage() {
  const { api } = await signedInApi()
  const home = readOrFail(await api.GET('/api/v1/me/home'))
  if (home instanceof ApiError) return <ErrorView description={describeError(home)} />

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold tracking-tight">Member dashboard</h1>
      {home.stokvels.length === 0 ? (
        <p className="rounded-3xl border border-dashed p-6">You are not in any stokvels yet.</p>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {home.stokvels.map((s) => (
            <StokvelCard key={s.stokvelId} entry={s} href={`/stokvels/${s.stokvelId}`} />
          ))}
        </div>
      )}
    </section>
  )
}