// A pattern for an amount as the app formats it: "R", a space or no-break space, groups of three
// separated by spaces or no-break spaces, a comma, two decimals. Works for any cents value.
export function amountPattern(cents: number): RegExp {
  const rands = String(Math.floor(cents / 100)).replace(/\B(?=(\d{3})+(?!\d))/g, '\\s')
  const decimals = String(cents % 100).padStart(2, '0')
  return new RegExp(`R\\s*${rands},${decimals}`)
}