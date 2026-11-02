'use client'

import { useState, type ReactNode } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

// One client per signed-in layout. It lives in memory only, so leaving the layout (sign out) drops it.
export function QueryProvider({ children }: { children: ReactNode }) {
  const [client] = useState(
    () => new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, retry: false, refetchOnWindowFocus: false } } }),
  )
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}