'use client'

import { useEffect } from 'react'
import { usePathname } from 'next/navigation'
import { usePinsStoreApi } from '@/components/PinsProvider'

// A pin for a stokvel the member has left opens the not-found page, and removes itself here.
export function ForgetPin() {
  const pathname = usePathname()
  const store = usePinsStoreApi()

  useEffect(() => {
    const id = /^\/stokvels\/([^/]+)/.exec(pathname)?.[1]
    if (!id) return
    const run = () => store.getState().unpin(decodeURIComponent(id))
    if (store.persist.hasHydrated()) run()
    return store.persist.onFinishHydration(run)
  }, [pathname, store])

  return null
}