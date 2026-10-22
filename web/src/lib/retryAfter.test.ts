import { describe, expect, it } from 'vitest'
import { parseRetryAfter } from './retryAfter'

const now = Date.parse('Wed, 21 Oct 2026 07:28:00 GMT')

describe('parseRetryAfter', () => {
  it('reads a number of seconds', () => expect(parseRetryAfter('23', now)).toBe(23))
  it('reads an HTTP date in the future', () =>
    expect(parseRetryAfter('Wed, 21 Oct 2026 07:28:30 GMT', now)).toBe(30))
  it('returns 0 for a date in the past', () =>
    expect(parseRetryAfter('Wed, 21 Oct 2026 07:00:00 GMT', now)).toBe(0))
  it.each(['soon', '-5', '12abc', '', '1.5', 'Wed, 99 Foo 2026 99:99:99 GMT'])(
    'returns null for garbage %j', (input) => expect(parseRetryAfter(input, now)).toBeNull())
  it('returns null when the header is missing', () => expect(parseRetryAfter(null, now)).toBeNull())
})