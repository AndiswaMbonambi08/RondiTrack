// Money in the browser is integer cents. No fractional numbers anywhere in this file, and no
// multiplying a decimal: 19.99 * 100 is 1998.9999999999998 in JavaScript.
//
// Accepted: "150", "150.5", "150.50", "150,50", "1 500,50", "R1500", "R 1 500,50"
// (spaces or no-break spaces group thousands in groups of exactly 3 digits).
// Refused:  more than 2 decimals ("150.505"), "1,500" and "1,500.50" (would need guessing),
// negatives, letters, empty text, and anything over the maximum when one is given.

export const MAX_CENTS = 100_000_000 // R1 000 000, the API's ceiling

const AMOUNT = /^(?:R\s*)?(\d{1,3}(?:[ \u00a0\u202f]\d{3})+|\d+)(?:[.,](\d{1,2}))?$/

/** Typed rands -> integer cents, or null when the text can't be read exactly (or is over `max`). */
export function parseRandsToCents(text: string, max?: number): number | null {
  const match = AMOUNT.exec(text.trim())
  if (!match) return null

  const whole = Number(match[1].replace(/[ \u00a0\u202f]/g, ''))
  const fraction = Number((match[2] ?? '').padEnd(2, '0'))

  // Whole numbers only, so this stays exact up to Number.MAX_SAFE_INTEGER. Anything bigger
  // is refused instead of being silently rounded.
  const cents = whole * 100 + fraction
  if (!Number.isSafeInteger(cents)) return null
  if (max !== undefined && cents > max) return null
  return cents
}

/** Integer cents -> text a person can edit and parseRandsToCents reads back exactly. */
export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new RangeError('cents must be a non-negative safe integer')
  }
  const remainder = cents % 100
  const rands = (cents - remainder) / 100
  return remainder === 0 ? String(rands) : `${rands}.${String(remainder).padStart(2, '0')}`
}