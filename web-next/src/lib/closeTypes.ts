export type ClosePreview = {
  cycleId: string
  cycleLabel: string
  asOf: string // YYYY-MM-DD, a South African calendar date
  totalArrearsCents: number
  items: { memberId: string; memberName: string; shortfallCents: number }[]
  version: string
}