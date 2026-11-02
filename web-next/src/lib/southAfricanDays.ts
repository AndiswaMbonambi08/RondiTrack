// The ONE place a South African calendar date becomes UTC instants. Nothing else converts dates.
// South Africa is UTC+2 all year (no daylight saving), so 00:00 SAST is 22:00 UTC the day before.
// Only Date.UTC is used, never the machine's time zone, so every computer gives the same answer.
const SAST_OFFSET_HOURS = 2
const YMD = /^(\d{4})-(\d{2})-(\d{2})$/

type Day = { y: number; m: number; d: number }

function parseDay(text: string): Day | null {
  const match = YMD.exec(text)
  if (!match) return null
  const y = Number(match[1])
  const m = Number(match[2])
  const d = Number(match[3])
  // A date that does not exist (2026-02-30) rolls over in Date.UTC, so compare the parts back.
  const probe = new Date(Date.UTC(y, m - 1, d))
  const real = probe.getUTCFullYear() === y && probe.getUTCMonth() === m - 1 && probe.getUTCDate() === d
  return real ? { y, m, d } : null
}

export const isValidDay = (text: string): boolean => parseDay(text) !== null

const dayNumber = (day: Day) => Date.UTC(day.y, day.m - 1, day.d)

// 00:00 SAST on the given day (plus extra days), as a UTC instant.
const startOfDay = (day: Day, plusDays: number) =>
  new Date(Date.UTC(day.y, day.m - 1, day.d + plusDays, -SAST_OFFSET_HOURS)).toISOString()

export type UtcRange = { from: string | null; to: string | null }

/**
 * Two YYYY-MM-DD South African dates (both days included) become a half-open UTC range [from, to).
 * from = 00:00 SAST on the first day. to = 00:00 SAST on the day AFTER the last day. No 23:59:59.
 * Rules: an end that is null stays open. If to is before from the two are swapped.
 * A date that is not real throws a RangeError.
 */
export function southAfricanDaysToUtcRange(from: string | null, to: string | null): UtcRange {
  const first = from === null ? null : parseDay(from)
  const last = to === null ? null : parseDay(to)
  if (from !== null && first === null) throw new RangeError(`Not a real date: ${from}`)
  if (to !== null && last === null) throw new RangeError(`Not a real date: ${to}`)

  const [start, end]: [Day | null, Day | null] =
    first && last && dayNumber(last) < dayNumber(first) ? [last, first] : [first, last]

  return { from: start ? startOfDay(start, 0) : null, to: end ? startOfDay(end, 1) : null }
}