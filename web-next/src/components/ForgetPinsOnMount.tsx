'use client'

import { useEffect } from 'react'
import { PINS_STORAGE_KEY } from '@/lib/pinsKey'

// Shown on /login and /register, which only appear when nobody is signed in: after a sign out,
// an expired session or a refused refresh. The pins of whoever was there before are removed.
export function ForgetPinsOnMount() {
  useEffect(() => {
    try {
      localStorage.removeItem(PINS_STORAGE_KEY)
    } catch {
      // Storage can be blocked. The owner check in PinsOwner is the backstop.
    }
  }, [])
  return null
}