import { CloseCycleButton } from '@/components/CloseCycleButton'
import { InviteButton } from '@/components/InviteButton'
import { canRecordForOthers } from '@/lib/roles'

type Props = {
  /** This person's role in THIS stokvel, from the API's answer for this stokvel. */
  role: string
  stokvelId: string
  cycle: { id: string; label: string; status: string } | null
}

// Nothing at all for an ordinary member: no heading, no disabled button, no hint that it exists.
// The API refuses the same actions with 403 anyway.
export function TreasurerActions({ role, stokvelId, cycle }: Props) {
  if (!canRecordForOthers(role)) return null
  return (
    <div className="max-w-xl space-y-3 rounded-3xl bg-card p-5 shadow-sm">
      <h2 className="text-lg font-semibold">Treasurer actions</h2>
      {cycle && cycle.status === 'Open' && (
        <CloseCycleButton stokvelId={stokvelId} cycleId={cycle.id} cycleLabel={cycle.label} />
      )}
      <InviteButton stokvelId={stokvelId} />
    </div>
  )
}