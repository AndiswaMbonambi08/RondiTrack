// Money in the browser is integer cents. No floats anywhere in this file.
//
// Accepted: "150", "150.5", "150.50", "150,50", "1 500,50", "1 500.50"
// (spaces or no-break spaces group thousands in groups of exactly 3 digits).
// Refused:  more than 2 decimals ("150.505"), "1,500" (3 digits after the
// separator would need rounding or guessing), negatives, letters, empty text.

const AMOUNT = /^(\d{1,3}(?:[ \u00a0\u202f]\d{3})+|\d+)(?:[.,](\d{1,2}))?$/

/** Typed rands -> integer cents, or null when the text can't be read exactly. */
export function parseRandsToCents(text: string): number | null {
  const match = AMOUNT.exec(text.trim())
  if (!match) return null

  const whole = match[1].replace(/[ \u00a0\u202f]/g, '')
  const fraction = (match[2] ?? '').padEnd(2, '0')

  const cents = BigInt(whole) * 100n + BigInt(fraction)
  if (cents > BigInt(Number.MAX_SAFE_INTEGER)) return null
  return Number(cents)
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
