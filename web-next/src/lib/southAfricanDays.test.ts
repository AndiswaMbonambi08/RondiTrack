import { describe, expect, it } from 'vitest'
import { isValidDay, southAfricanDaysToUtcRange, type UtcRange } from './southAfricanDays'

const inside = (instant: string, range: UtcRange) => {
  const t = Date.parse(instant)
  return (range.from === null || t >= Date.parse(range.from)) && (range.to === null || t < Date.parse(range.to))
}

describe('southAfricanDaysToUtcRange', () => {
  const october = southAfricanDaysToUtcRange('2026-10-01', '2026-10-31')

  it('turns 1 to 31 October into [30 Sep 22:00Z, 31 Oct 22:00Z)', () => {
    expect(october.from).toBe('2026-09-30T22:00:00.000Z')
    expect(october.to).toBe('2026-10-31T22:00:00.000Z')
    expect(inside('2026-09-30T22:00:00Z', october)).toBe(true)
    expect(inside('2026-09-30T21:59:59Z', october)).toBe(false)
    expect(inside('2026-10-31T21:59:59Z', october)).toBe(true)
    expect(inside('2026-10-31T22:00:00Z', october)).toBe(false)
  })

  it('keeps 23:30 SAST on 31 October inside and 00:00 SAST on 1 November outside', () => {
    expect(inside('2026-10-31T21:30:00Z', october)).toBe(true)
    expect(inside('2026-10-31T22:00:00Z', october)).toBe(false)
  })

  it('covers 24 hours for a single day', () => {
    const day = southAfricanDaysToUtcRange('2026-10-15', '2026-10-15')
    expect(Date.parse(day.to!) - Date.parse(day.from!)).toBe(24 * 60 * 60 * 1000)
  })

  it('leaves the other end open when only one is given', () => {
    expect(southAfricanDaysToUtcRange('2026-10-01', null)).toEqual({ from: '2026-09-30T22:00:00.000Z', to: null })
    expect(southAfricanDaysToUtcRange(null, '2026-10-31')).toEqual({ from: null, to: '2026-10-31T22:00:00.000Z' })
    expect(southAfricanDaysToUtcRange(null, null)).toEqual({ from: null, to: null })
  })

  it('swaps the dates when to is before from', () => {
    expect(southAfricanDaysToUtcRange('2026-10-31', '2026-10-01')).toEqual(october)
  })

  it('works across a year end and on a leap day', () => {
    expect(southAfricanDaysToUtcRange(null, '2026-12-31').to).toBe('2026-12-31T22:00:00.000Z')
    expect(southAfricanDaysToUtcRange('2027-01-01', null).from).toBe('2026-12-31T22:00:00.000Z')
    expect(southAfricanDaysToUtcRange('2028-02-29', '2028-02-29').to).toBe('2028-02-29T22:00:00.000Z')
  })

  it.each(['2026-02-30', '2027-02-29', '2026-13-01', '2026-00-10', 'yesterday', '2026-1-5', '2026-10-31T00:00:00Z', ''])(
    'rejects %j instead of rolling it over',
    (text) => {
      expect(isValidDay(text)).toBe(false)
      expect(() => southAfricanDaysToUtcRange(text, null)).toThrow(RangeError)
      expect(() => southAfricanDaysToUtcRange(null, text)).toThrow(RangeError)
    },
  )
})