// South Africa has been UTC+2 all year with no DST since 1944,
// so "Johannesburg midnight" is always 22:00 UTC.
const SA_OFFSET_MS = 2 * 60 * 60 * 1000;
const DAY_MS = 24 * 60 * 60 * 1000;

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;

/** Date-only "2026-10-31" -> end of that day in Johannesburg (22:00Z). Instants pass through. */
export function dueDateToDeadline(due: string): Date {
  if (DATE_ONLY.test(due)) {
    const [y, m, d] = due.split('-').map(Number);
    return new Date(Date.UTC(y, m - 1, d + 1) - SA_OFFSET_MS);
  }
  return new Date(due);
}

/** Whole SA calendar days since the epoch for this instant. */
function saDayNumber(instant: Date): number {
  return Math.floor((instant.getTime() + SA_OFFSET_MS) / DAY_MS);
}

/** Milliseconds until the next Johannesburg midnight. Exactly midnight returns 24h, never 0. */
export function msUntilNextSaMidnight(now: Date): number {
  const sinceMidnight = (((now.getTime() + SA_OFFSET_MS) % DAY_MS) + DAY_MS) % DAY_MS;
  return DAY_MS - sinceMidnight;
}

export type DueInfo = { kind: 'today' | 'tomorrow' | 'future' | 'overdue'; text: string };

const days = (n: number) => `${n} ${n === 1 ? 'day' : 'days'}`;

export function describeDue(due: string, cycleStatus: string, now: Date): DueInfo | null {
  if (cycleStatus !== 'Open') return null; // API wins: Closed/PaidOut show no countdown
  const deadline = dueDateToDeadline(due);
  const dueDay = saDayNumber(new Date(deadline.getTime() - 1)); // last SA day the money may arrive
  const diff = dueDay - saDayNumber(now);
  if (diff < 0) return { kind: 'overdue', text: `Overdue by ${days(-diff)}` };
  if (diff === 0) return { kind: 'today', text: 'Due today' };
  if (diff === 1) return { kind: 'tomorrow', text: 'Due tomorrow' };
  return { kind: 'future', text: `Due in ${days(diff)}` };
}

const dueFormat = new Intl.DateTimeFormat('en-ZA', {
  day: 'numeric', month: 'short', year: 'numeric', timeZone: 'Africa/Johannesburg',
});
export function formatDueDate(due: string): string {
  return dueFormat.format(new Date(dueDateToDeadline(due).getTime() - 1));
}