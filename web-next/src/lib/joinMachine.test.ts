import { describe, expect, it } from 'vitest'
import { joinTransition, normaliseCode, resolveStep, stepUrl } from './joinMachine'

describe('joinTransition', () => {
  it('moves forward and back, and stops at the ends', () => {
    expect(joinTransition('code', 'next')).toBe('rules')
    expect(joinTransition('rules', 'next')).toBe('confirm')
    expect(joinTransition('confirm', 'next')).toBe('confirm')
    expect(joinTransition('confirm', 'back')).toBe('rules')
    expect(joinTransition('code', 'back')).toBe('code')
  })
})

describe('resolveStep', () => {
  it('sends a step without a valid code back to the code step', () => {
    expect(resolveStep('confirm', null)).toBe('code')
    expect(resolveStep('rules', null)).toBe('code')
    expect(resolveStep(null, null)).toBe('code')
  })
  it('keeps the requested step when there is a code', () => {
    expect(resolveStep('code', 'ABC123')).toBe('code')
    expect(resolveStep('confirm', 'ABC123')).toBe('confirm')
  })
  it('lands an invite link (code only) on the rules', () => {
    expect(resolveStep(null, 'ABC123')).toBe('rules')
    expect(resolveStep('banana', 'ABC123')).toBe('rules')
  })
})

describe('normaliseCode', () => {
  it('accepts letters and digits in any case', () => expect(normaliseCode(' abc123 ')).toBe('ABC123'))
  it.each(['', 'ab', '../x', 'ABC 123', 'A'.repeat(21)])('refuses %j', (v) => expect(normaliseCode(v)).toBeNull())
})

describe('stepUrl', () => {
  it('keeps the code in the URL', () => expect(stepUrl('rules', 'ABC123')).toBe('/join?code=ABC123&step=rules'))
  it('has no code on the first step when there is none', () => expect(stepUrl('code', null)).toBe('/join?step=code'))
})