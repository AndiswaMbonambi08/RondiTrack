// Shared by the page (createLoader), the Route Handler and the filter controls.
// Every parser is an allow-list or a strict format: anything else falls back to the default.
import { createLoader, createParser, parseAsStringLiteral } from 'nuqs/server'
import { isValidDay } from './southAfricanDays'

export const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// Dates stay text (YYYY-MM-DD) in the URL. parseAsIsoDate would make midnight UTC, which is the bug.
const parseAsMemberId = createParser({
  parse: (v) => (UUID_RE.test(v) ? v.toLowerCase() : null),
  serialize: (v) => v,
})
const parseAsCalendarDay = createParser({
  parse: (v) => (isValidDay(v) ? v : null),
  serialize: (v) => v,
})

export const historyParsers = {
  member: parseAsMemberId.withDefault(''),
  from: parseAsCalendarDay,
  to: parseAsCalendarDay,
  sort: parseAsStringLiteral(['newest', 'oldest'] as const).withDefault('newest'),
}

export const historyLoader = createLoader(historyParsers)

export type HistoryFilterValues = {
  member: string
  from: string | null
  to: string | null
  sort: 'newest' | 'oldest'
}