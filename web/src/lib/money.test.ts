import { describe, expect, it } from 'vitest';
import { formatCents } from './money';

const NBSP = '\u00A0';

describe('formatCents', () => {
  it('formats 10 cents', () => {
    expect(formatCents(10)).toBe(`R${NBSP}0,10`);
  });
  it('formats zero', () => {
    expect(formatCents(0)).toBe(`R${NBSP}0,00`);
  });
  it('formats 15050 cents', () => {
    expect(formatCents(15050)).toBe(`R${NBSP}150,50`);
  });
  it('formats 1234567 cents with a thousands separator', () => {
    expect(formatCents(1234567)).toBe(`R${NBSP}12${NBSP}345,67`);
  });
  it('formats a large amount (edge case)', () => {
    expect(formatCents(99999999)).toBe(`R${NBSP}999${NBSP}999,99`);
  });
  it('throws on a fractional number of cents', () => {
    expect(() => formatCents(10.5)).toThrow('Not a whole number of cents');
  });
});