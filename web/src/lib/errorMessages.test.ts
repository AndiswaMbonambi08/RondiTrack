import { describe, expect, it } from 'vitest'
import { ApiError } from '../api/apiError'
import { describeError } from './errorMessages'

const problem = (extra: object = {}) => ({ title: 't', correlationId: 'abc123', ...extra }) as never

describe('describeError', () => {
  it('404 says stokvel not found with a reference', () => {
    const d = describeError(new ApiError(404, problem(), null))
    expect(d).toMatchObject({ kind: 'notFound', title: 'Stokvel not found', reference: 'abc123' })
  })
  it('429 uses the number from Retry-After', () => {
    expect(describeError(new ApiError(429, problem(), 23)).message).toBe('Too many requests. Try again in 23 seconds.')
  })
  it('429 without Retry-After still reads sensibly', () => {
    expect(describeError(new ApiError(429, problem(), null)).message).toMatch(/Too many requests/)
  })
  it('wrong password never says which part was wrong', () => {
    expect(describeError(new ApiError(401, problem(), null), 'login').message).toBe('Email or password is incorrect.')
  })
  it('validation carries the field messages', () => {
    const d = describeError(new ApiError(400, problem({ errors: { Email: ['Required'] } }), null))
    expect(d.fieldErrors).toEqual({ Email: ['Required'] })
  })
  it('network failure has no reference', () => {
    const d = describeError(new TypeError('Failed to fetch'))
    expect(d.message).toBe("Can't reach RondiTrack. Check your connection.")
    expect(d.reference).toBeUndefined()
  })
  it('a 500 never leaks detail', () => {
    const d = describeError(new ApiError(500, problem({ detail: 'NpgsqlException: password=secret' }), null))
    expect(JSON.stringify(d)).not.toContain('Npgsql')
    expect(d.reference).toBe('abc123')
  })
})