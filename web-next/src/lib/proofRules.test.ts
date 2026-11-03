import { describe, expect, it } from 'vitest'
import { checkProofFile, PROOF_MAX_BYTES } from './proofRules'

describe('checkProofFile', () => {
  it('accepts a JPEG, a PNG and a PDF under the limit', () => {
    expect(checkProofFile({ size: 1000, type: 'image/jpeg' })).toBeNull()
    expect(checkProofFile({ size: 1000, type: 'image/png' })).toBeNull()
    expect(checkProofFile({ size: PROOF_MAX_BYTES, type: 'application/pdf' })).toBeNull()
  })
  it('refuses a file over 5 MB and says how big it is', () => {
    expect(checkProofFile({ size: 6 * 1024 * 1024, type: 'application/pdf' })).toContain('6.0 MB')
  })
  it('refuses other types and empty files', () => {
    expect(checkProofFile({ size: 10, type: 'text/plain' })).toContain('JPEG, PNG or PDF')
    expect(checkProofFile({ size: 0, type: 'image/png' })).toContain('empty')
  })
})