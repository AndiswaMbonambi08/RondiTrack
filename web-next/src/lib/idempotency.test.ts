import { describe, expect, it } from 'vitest'
import { chooseKey, intentOf } from './idempotency'

const made = () => 'new-key'
const same = intentOf('s', 'c', 'm', 15000)

describe('chooseKey', () => {
  it('a first submit uses the key the server wrote', () => {
    expect(chooseKey('key-1', '', same, made)).toBe('key-1')
  })
  it('a retry of the same payment keeps the key', () => {
    expect(chooseKey('key-1', same, same, made)).toBe('key-1')
  })
  it('a changed amount is a new payment with a new key', () => {
    expect(chooseKey('key-1', same, intentOf('s', 'c', 'm', 20000), made)).toBe('new-key')
  })
  it('a changed member is a new payment with a new key', () => {
    expect(chooseKey('key-1', same, intentOf('s', 'c', 'other', 15000), made)).toBe('new-key')
  })
})