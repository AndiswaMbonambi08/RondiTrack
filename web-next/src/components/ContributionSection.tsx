import { connection } from 'next/server'
import { ContributionForm } from '@/components/ContributionForm'
import { signedInApi } from '@/lib/api'
import { formatCents } from '@/lib/money'
import { canRecordForOthers } from '@/lib/roles'
import { centsToInput } from '@/money/money'

type Props = {
  stokvelId: string
  /** This person's role in THIS stokvel, from the API's answer for this stokvel. */
  role: string
  cycle: { id: string; label: string; status: string } | null
  contributionCents: number
  owedCents: number
  selfId: string
  selfName: string
}

function Sentence({ text }: { text: string }) {
  return (
    <div className="max-w-xl rounded-3xl bg-card p-5 shadow-sm">
      <h2 className="text-lg font-semibold">Record a contribution</h2>
      <p className="mt-1 text-sm text-muted-foreground">{text}</p>
    </div>
  )
}

// Only ids and names reach the browser, never whole API responses.
async function loadMembers(stokvelId: string): Promise<{ userId: string; fullName: string }[] | null> {
  try {
    const { api } = await signedInApi()
    const out: { userId: string; fullName: string }[] = []
    let token: string | undefined
    for (let page = 0; page < 10; page++) {
      const res = await api.GET('/api/v1/stokvels/{stokvelId}/members', {
        params: { path: { stokvelId }, query: token ? { PageToken: token } : {} },
      })
      // A 403 here only means "no picker". It never signs anyone out.
      if (!res.response.ok || !res.data) return null
      out.push(...res.data.items.map((m) => ({ userId: m.userId, fullName: m.fullName })))
      if (!res.data.nextPageToken) break
      token = res.data.nextPageToken
    }
    return out
  } catch {
    return null
  }
}

export async function ContributionSection(p: Props) {
  if (!p.cycle) return <Sentence text="There is no open cycle to record a contribution into right now." />

  const status = p.cycle.status.toLowerCase()
  if (status === 'closed') return <Sentence text={`Cycle ${p.cycle.label} is closed, so contributions can no longer be recorded for it.`} />
  if (status === 'paidout') return <Sentence text={`Cycle ${p.cycle.label} has already been paid out, so contributions can't be recorded for it.`} />
  if (status !== 'open') return <Sentence text={`Cycle ${p.cycle.label} is ${p.cycle.status}, so it is not taking contributions.`} />

  // The picker appears only when this stokvel's role allows it.
  const members = canRecordForOthers(p.role) ? await loadMembers(p.stokvelId) : null

  await connection() // the key is random per render, so this part is never prerendered
  const initialKey = crypto.randomUUID()

  return (
    <ContributionForm
      stokvelId={p.stokvelId}
      cycleId={p.cycle.id}
      cycleLabel={p.cycle.label}
      selfId={p.selfId}
      selfName={p.selfName}
      owedText={p.owedCents > 0 ? `You still owe ${formatCents(p.owedCents)} this cycle.` : null}
      members={members}
      initialKey={initialKey}
      initialAmount={centsToInput(p.owedCents > 0 ? p.owedCents : p.contributionCents)}
    />
  )
}