import { describe, expect, it } from 'vitest'
import { formatCents } from '../lib/money'
import { centsToInput, MAX_CENTS, parseRandsToCents } from './money'

const NBSP = '\u00a0'

describe('parseRandsToCents: the ways South Africans write amounts', () => {
  it.each([
    ['1500', 150000],
    ['1 500', 150000],
    ['1 500,50', 150050],
    ['1 500.50', 150050],
    ['R1500', 150000],
    ['R 1500', 150000],
    ['R 1 500,50', 150050],
    [`R${NBSP}1${NBSP}500,50`, 150050],
    [`1${NBSP}500`, 150000],
    ['1500,5', 150050],
    ['0,5', 50],
    ['R0,5', 50],
    ['19,99', 1999],
    ['19.99', 1999],
    ['0', 0],
    ['0,05', 5],
    ['  R 42  ', 4200],
  ])('reads %j as %i cents', (text, cents) => {
    expect(parseRandsToCents(text)).toBe(cents)
  })
})

describe('parseRandsToCents: refuses what it would have to guess at', () => {
  it.each([
    ['0,505', 'three decimal places'],
    ['150.505', 'three decimal places'],
    ['1,500', 'comma: thousands or decimals?'],
    ['1,500.50', 'comma: thousands or decimals?'],
    ['abc', 'letters'],
    ['1e3', 'exponent'],
    ['R', 'a currency sign alone'],
    ['', 'empty'],
    ['   ', 'blank'],
    ['-5', 'negative'],
    ['R-5', 'negative'],
    ['.5', 'no digit before the separator'],
    ['12 34', 'grouping that is not groups of 3'],
    ['r150', 'lower case r'],
  ])('refuses %j (%s) with null, never a rounded number', (text) => {
    expect(parseRandsToCents(text)).toBeNull()
  })

  it('refuses anything above the maximum, and accepts exactly the maximum', () => {
    expect(parseRandsToCents('1000000', MAX_CENTS)).toBe(MAX_CENTS)
    expect(parseRandsToCents('1000000,01', MAX_CENTS)).toBeNull()
    expect(parseRandsToCents('99999999999999999', MAX_CENTS)).toBeNull()
  })
})

describe('no floating point anywhere', () => {
  it('19,99 is exactly 1999 cents, where 19.99 * 100 is not', () => {
    expect(19.99 * 100).not.toBe(1999)
    expect(parseRandsToCents('19,99')).toBe(1999)
  })

  it('every rand and cent pair from R0 to R2000 reads back exactly', () => {
    const wrong: string[] = []
    for (let rands = 0; rands <= 2000; rands++) {
      for (let c = 0; c < 100; c++) {
        const text = `${rands},${String(c).padStart(2, '0')}`
        if (parseRandsToCents(text) !== rands * 100 + c) wrong.push(text)
      }
    }
    expect(wrong).toEqual([])
  })
})

describe('formatting round-trips through parsing', () => {
  it.each([0, 1, 99, 100, 150050, MAX_CENTS])('%i cents', (cents) => {
    expect(parseRandsToCents(formatCents(cents), MAX_CENTS)).toBe(cents)
  })

  it('formats with a no-break space between thousands, as Intl does', () => {
    expect(formatCents(150050)).toBe(`R${NBSP}1${NBSP}500,50`)
  })

  it('centsToInput feeds the parser', () => {
    for (const cents of [0, 1, 99, 100, 150050]) expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
  })
})