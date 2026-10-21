import { describe, expect, it } from 'vitest';
import { formatCents } from './money';

describe('formatCents (arrears)', () => {
  it('formats an arrears amount as rand with no-break spaces', () => {
    expect(formatCents(15000).replace(/\u00A0/g, ' ')).toBe('R 150,00');
  });
});
