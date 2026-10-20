const TIME_ZONE = 'Africa/Johannesburg';

const dateTimeFormat = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: TIME_ZONE,
});

const dateFormat = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeZone: TIME_ZONE,
});

/** For a UTC instant such as "2026-10-31T22:30:00Z": date and time in South African time. */
export function formatDateTime(utcInstant: string): string {
  return dateTimeFormat.format(new Date(utcInstant));
}

/** For a UTC instant: the South African calendar day only. */
export function formatDate(utcInstant: string): string {
  return dateFormat.format(new Date(utcInstant));
}

/**
 * For a plain calendar date such as "2026-10-31" (no time, no zone).
 * Parsed at noon UTC so no time zone can push it onto a different day.
 */
export function formatCalendarDate(ymd: string): string {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(ymd)) {
    throw new Error('Expected a YYYY-MM-DD date');
  }
  return dateFormat.format(new Date(`${ymd}T12:00:00Z`));
}