import { describe, expect, it } from 'vitest'
import { centsToInput, parseRandsToCents } from './money'

describe('parseRandsToCents', () => {
  it.each([
    ['150.50', 15050],
    ['150.5', 15050],
    ['19.99', 1999],
    ['150', 15000],
    ['0', 0],
    ['0.05', 5],
    ['150,50', 15050],
    ['1 500,50', 150050],
    ['1\u00a0500.50', 150050],
    ['  42 ', 4200],
  ])('reads %j as %i cents', (text, cents) => {
    expect(parseRandsToCents(text)).toBe(cents)
  })

  it.each(['150.505', '1,500', '', ' ', 'abc', '-5', '1e3', '.5', '1.', '12 34', 'R150'])(
    'refuses %j',
    (text) => {
      expect(parseRandsToCents(text)).toBeNull()
    },
  )

  it('refuses amounts that are not safe integers of cents', () => {
    expect(parseRandsToCents('99999999999999999')).toBeNull()
  })
})

describe('centsToInput', () => {
  it('writes editable text', () => {
    expect(centsToInput(15050)).toBe('150.50')
    expect(centsToInput(15000)).toBe('150')
    expect(centsToInput(5)).toBe('0.05')
    expect(centsToInput(0)).toBe('0')
  })

  it('round-trips every whole number of cents from 0 to 100 000', () => {
    for (let cents = 0; cents <= 100_000; cents++) {
      expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
    }
  })
})
