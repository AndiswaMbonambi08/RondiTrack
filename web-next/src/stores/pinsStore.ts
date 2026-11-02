import { createJSONStorage, persist } from 'zustand/middleware'
import { createStore } from 'zustand/vanilla'
import { PINS_STORAGE_KEY } from '@/lib/pinsKey'

// Only what a pin needs. Never a balance, a role or a member list: those belong to the API.
export type Pin = { id: string; name: string }

export type PinsState = {
  /** The signed-in user these pins belong to. A different user clears them. */
  ownerId: string | null
  pins: Pin[]
  pin: (pin: Pin) => void
  unpin: (id: string) => void
  claim: (userId: string) => void
  reset: () => void
}

type Saved = { ownerId: string | null; pins: Pin[] }

// What comes back from localStorage is untrusted text.
function readSaved(value: unknown): Saved {
  const v = (typeof value === 'object' && value !== null ? value : {}) as Partial<Saved>
  const pins = Array.isArray(v.pins)
    ? v.pins.filter((p): p is Pin => !!p && typeof p.id === 'string' && typeof p.name === 'string')
    : []
  return { ownerId: typeof v.ownerId === 'string' ? v.ownerId : null, pins }
}

// A factory, not a module-level store: the provider makes one store per provider.
export const createPinsStore = () =>
  createStore<PinsState>()(
    persist(
      (set) => ({
        ownerId: null,
        pins: [],
        pin: (pin) =>
          set((s) => ({
            pins: [...s.pins.filter((p) => p.id !== pin.id), { id: pin.id, name: pin.name }],
          })),
        unpin: (id) => set((s) => ({ pins: s.pins.filter((p) => p.id !== id) })),
        claim: (userId) =>
          set((s) => (s.ownerId === userId ? s : { ownerId: userId, pins: [] })),
        reset: () => set({ ownerId: null, pins: [] }),
      }),
      {
        name: PINS_STORAGE_KEY,
        storage: createJSONStorage(() => localStorage),
        // Loaded after the first render by the provider, so the server and the first client render match.
        skipHydration: true,
        partialize: (s) => ({ ownerId: s.ownerId, pins: s.pins }),
        merge: (saved, current) => ({ ...current, ...readSaved(saved) }),
      },
    ),
  )

export type PinsStore = ReturnType<typeof createPinsStore>