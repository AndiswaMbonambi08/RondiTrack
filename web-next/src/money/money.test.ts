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
    ['1 500.50', 150050],
    ['  42 ', 4200],
    // The R prefix is accepted since 9.2 (see money.edge.test.ts for the full list).
    ['R150', 15000],
  ])('reads %j as %i cents', (text, cents) => {
    expect(parseRandsToCents(text)).toBe(cents)
  })

  it.each(['150.505', '1,500', '', ' ', 'abc', '-5', '1e3', '.5', '1.', '12 34'])('refuses %j', (text) => {
    expect(parseRandsToCents(text)).toBeNull()
  })

  it('refuses amounts that are not safe integers of cents', () => {
    expect(parseRandsToCents('9'.repeat(17))).toBeNull()
    expect(parseRandsToCents('90071992547409,92')).toBeNull()
  })
})

describe('centsToInput', () => {
  it('writes editable text', () => {
    expect(centsToInput(0)).toBe('0')
    expect(centsToInput(1)).toBe('0.01')
    expect(centsToInput(15000)).toBe('150')
    expect(centsToInput(15050)).toBe('150.50')
    expect(centsToInput(150050)).toBe('1500.50')
  })

  it('round-trips every whole number of cents from 0 to 100 000', () => {
    const wrong: number[] = []
    for (let cents = 0; cents <= 100_000; cents++) {
      if (parseRandsToCents(centsToInput(cents)) !== cents) wrong.push(cents)
    }
    expect(wrong).toEqual([])
  })
})