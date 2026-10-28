import { describe, expect, it } from 'vitest'
import { stokvelsWhereTreasurer } from './treasurer'

const stokvel = (name: string, role: string) => ({ stokvelId: name, name, role })

describe('stokvelsWhereTreasurer', () => {
  it('keeps only the stokvels where the person is Treasurer, from a mixed list', () => {
    const list = [
      stokvel('Family', 'Treasurer'),
      stokvel('Work', 'Member'),
      stokvel('Teachers', 'Admin'),
      stokvel('Street', 'Treasurer'),
    ]
    expect(stokvelsWhereTreasurer(list).map((s) => s.name)).toEqual(['Family', 'Street'])
  })

  it('returns an empty list when the person is Treasurer nowhere', () => {
    const list = [stokvel('Work', 'Member'), stokvel('Teachers', 'Admin')]
    expect(stokvelsWhereTreasurer(list)).toEqual([])
  })

  it('returns an empty list for an empty list', () => {
    expect(stokvelsWhereTreasurer([])).toEqual([])
  })

  it('does not change the list it was given', () => {
    const list = [stokvel('Family', 'Treasurer'), stokvel('Work', 'Member')]
    stokvelsWhereTreasurer(list)
    expect(list).toHaveLength(2)
  })
})
