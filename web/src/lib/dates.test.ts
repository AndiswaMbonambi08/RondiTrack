import { describe, expect, it } from 'vitest';
import { formatCalendarDate, formatDate, formatDateTime } from './dates';

describe('dates in Africa/Johannesburg', () => {
  it('shows the midnight contribution on 1 November, not 31 October', () => {
    expect(formatDateTime('2026-10-31T22:30:00Z')).toBe('01 Nov 2026, 00:30');
  });
  it('formats a UTC instant as a South African day', () => {
    expect(formatDate('2026-10-31T21:59:59Z')).toBe('31 Oct 2026');
    expect(formatDate('2026-10-31T22:00:00Z')).toBe('01 Nov 2026');
  });
  it('formats a calendar date without shifting the day', () => {
    expect(formatCalendarDate('2026-10-31')).toBe('31 Oct 2026');
  });
  it('rejects a value that is not a calendar date', () => {
    expect(() => formatCalendarDate('2026-10-31T22:30:00Z')).toThrow();
  });
});