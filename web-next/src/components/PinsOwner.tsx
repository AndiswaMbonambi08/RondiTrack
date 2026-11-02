'use client'

import { useEffect } from 'react'
import { usePinsStoreApi } from '@/components/PinsProvider'

// Backstop: if the saved pins belong to someone else, they are cleared before anyone sees them.
// Runs after the saved pins are loaded, never before.
export function PinsOwner({ userId }: { userId: string }) {
  const store = usePinsStoreApi()
  useEffect(() => {
    const claim = () => store.getState().claim(userId)
    if (store.persist.hasHydrated()) claim()
    return store.persist.onFinishHydration(claim)
  }, [store, userId])
  return null
}