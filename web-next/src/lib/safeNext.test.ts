import { describe, expect, it } from 'vitest'
import { safeNext } from './safeNext'

describe('safeNext', () => {
  it.each(['/stokvels/abc', '/stokvels/abc?x=1', '/dashboard/treasurer'])('follows %s', (path) => {
    expect(safeNext(path, '/fallback')).toBe(path)
  })

  it.each([
    '//evil.example',
    '/\\evil.example',
    '/ok\\evil',
    'https://evil.example',
    'javascript:alert(1)',
    'stokvels/abc',
    '/login',
    '/register?x=1',
    '/a\nb',
    '',
  ])('refuses %j', (path) => {
    expect(safeNext(path, '/fallback')).toBe('/fallback')
  })

  it('uses the fallback for null and undefined', () => {
    expect(safeNext(null, '/fallback')).toBe('/fallback')
    expect(safeNext(undefined, '/fallback')).toBe('/fallback')
  })
})