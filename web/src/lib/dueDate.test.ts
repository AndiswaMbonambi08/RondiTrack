import { describe, expect, it } from 'vitest';
import { describeDue, dueDateToDeadline, msUntilNextSaMidnight } from './dueDate';

const MIN = 60_000;
const HOUR = 60 * MIN;

describe('dueDateToDeadline', () => {
  it('turns a date-only value into the end of that day in Johannesburg', () => {
    expect(dueDateToDeadline('2026-10-31').toISOString()).toBe('2026-10-31T22:00:00.000Z');
  });
  it('passes an instant through unchanged', () => {
    expect(dueDateToDeadline('2026-10-31T10:00:00Z').toISOString()).toBe('2026-10-31T10:00:00.000Z');
  });
});

describe('describeDue', () => {
  it('is "Due today" at 23:30 SAST on the due date', () => {
    expect(describeDue('2026-10-31', 'Open', new Date('2026-10-31T21:30:00Z'))?.text).toBe('Due today');
  });
  it('is "Due today" at 00:30 SAST on the due date', () => {
    expect(describeDue('2026-10-31', 'Open', new Date('2026-10-30T22:30:00Z'))?.text).toBe('Due today');
  });
  it('is "Overdue by 1 day" just after midnight SAST', () => {
    expect(describeDue('2026-10-31', 'Open', new Date('2026-10-31T22:00:01Z'))?.text).toBe('Overdue by 1 day');
  });
  it('says tomorrow and N days', () => {
    expect(describeDue('2026-10-31', 'Open', new Date('2026-10-30T10:00:00Z'))?.text).toBe('Due tomorrow');
    expect(describeDue('2026-10-31', 'Open', new Date('2026-10-26T10:00:00Z'))?.text).toBe('Due in 5 days');
  });
  it('shows no countdown unless the cycle is Open', () => {
    expect(describeDue('2026-10-31', 'Closed', new Date('2026-10-20T10:00:00Z'))).toBeNull();
    expect(describeDue('2026-10-31', 'PaidOut', new Date('2026-10-20T10:00:00Z'))).toBeNull();
  });
});

describe('msUntilNextSaMidnight', () => {
  it('returns 30 minutes at 23:30 SAST', () => {
    expect(msUntilNextSaMidnight(new Date('2026-10-31T21:30:00Z'))).toBe(30 * MIN);
  });
  it('returns 24 hours (not 0) at exactly SA midnight', () => {
    expect(msUntilNextSaMidnight(new Date('2026-10-31T22:00:00Z'))).toBe(24 * HOUR);
  });
});