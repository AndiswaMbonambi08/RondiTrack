import { describe, expect, it } from 'vitest'
import { formatCalendarDate, formatDate, formatDateTime } from './dates'

// These must pass with TZ=UTC and with TZ=Africa/Johannesburg: the zone is fixed in dates.ts,
// never taken from the machine.
describe('South African dates', () => {
  it('23:30 on 31 October (21:30Z) is still 31 October', () => {
    expect(formatDate('2026-10-31T21:30:00Z')).toContain('31')
    expect(formatDate('2026-10-31T21:30:00Z')).toContain('Oct')
    expect(formatDateTime('2026-10-31T21:30:00Z')).toContain('23:30')
  })

  it('00:00 on 1 November (22:00Z) is November', () => {
    expect(formatDate('2026-10-31T22:00:00Z')).toContain('Nov')
  })

  it('a calendar date never moves to another day', () => {
    expect(formatCalendarDate('2026-10-31')).toContain('31')
    expect(formatCalendarDate('2026-10-31')).toContain('Oct')
    expect(() => formatCalendarDate('31/10/2026')).toThrow()
  })
})