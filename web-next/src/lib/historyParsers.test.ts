import { describe, expect, it } from 'vitest'
import { historyLoader } from './historyParsers'

describe('historyLoader', () => {
  it('falls back to the defaults for nonsense', () => {
    expect(historyLoader('?member=banana&from=2026-02-30&to=yesterday&sort=sideways')).toEqual({
      member: '', from: null, to: null, sort: 'newest',
    })
  })

  it('keeps valid values', () => {
    const id = '33333333-3333-4333-8333-333333333333'
    expect(historyLoader(`?member=${id}&from=2026-10-01&to=2026-10-31&sort=oldest`)).toEqual({
      member: id, from: '2026-10-01', to: '2026-10-31', sort: 'oldest',
    })
  })
})