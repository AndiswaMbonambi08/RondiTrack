// The browser's check is a courtesy. The API judges the file's bytes and is the rule.
export const PROOF_MAX_BYTES = 5 * 1024 * 1024
export const PROOF_TYPES = ['image/jpeg', 'image/png', 'application/pdf'] as const

/** A message for the person, or null when the file looks fine. */
export function checkProofFile(file: { size: number; type: string }): string | null {
  if (file.size === 0) return 'That file is empty.'
  if (!(PROOF_TYPES as readonly string[]).includes(file.type)) return 'Only JPEG, PNG or PDF files can be attached.'
  if (file.size > PROOF_MAX_BYTES) {
    return `That file is ${(file.size / (1024 * 1024)).toFixed(1)} MB. The most you can attach is 5 MB.`
  }
  return null
}