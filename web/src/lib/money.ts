// Money is integer cents end to end. We split into rands and cents with
// integer arithmetic, build an exact decimal string, and let Intl format
// that string, so no fractional float is ever created.
const zar = new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' });

export function formatCents(cents: number): string {
  if (!Number.isInteger(cents)) {
    throw new Error('Not a whole number of cents');
  }
  const sign = cents < 0 ? '-' : '';
  const abs = Math.abs(cents);
  const rands = Math.trunc(abs / 100); // integer division
  const remainder = abs % 100;
  const decimal = `${sign}${rands}.${String(remainder).padStart(2, '0')}`;
  // Intl.NumberFormat accepts an exact decimal string (needs lib ES2023 for the type).
  return zar.format(decimal as `${number}`);
}