'use client'

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { useStore } from 'zustand'
import { createPinsStore, type PinsState, type PinsStore } from '@/stores/pinsStore'

const PinsContext = createContext<PinsStore | null>(null)

export function PinsProvider({ children }: { children: ReactNode }) {
  // One store per provider.
  const [store] = useState(() => createPinsStore())

  // skipHydration: nothing is read from localStorage until here, after the first render.
  useEffect(() => {
    void store.persist.rehydrate()
  }, [store])

  return <PinsContext value={store}>{children}</PinsContext>
}

export function usePinsStoreApi(): PinsStore {
  const store = useContext(PinsContext)
  if (!store) throw new Error('usePinsStore must be used inside <PinsProvider>')
  return store
}

export function usePinsStore<T>(selector: (state: PinsState) => T): T {
  return useStore(usePinsStoreApi(), selector)
}