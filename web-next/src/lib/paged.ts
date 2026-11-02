// Shapes of the 8.3 endpoints (contribution-history and me/activity). Plain types, safe in the browser.
export type Page<T> = { items: T[]; nextPageToken: string }

export type HistoryItem = {
  id: string
  memberId: string
  memberName: string
  amountCents: number
  status: string
  recordedAt: string // UTC instant
  cycleId: string
  cycleLabel: string
}

export type ActivityItem = {
  id: string
  kind: string // Contribution, Payout or MemberJoined
  stokvelId: string
  stokvelName: string
  occurredAt: string // UTC instant
  actorName: string | null
  amountCents: number | null
}

export type MemberOption = { userId: string; fullName: string }