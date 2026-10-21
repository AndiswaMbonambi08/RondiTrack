import { useEffect, useState } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { queryClient } from './api/queryClient'
import { ApiError } from './api/apiError'
import { useHome } from './api/queries'
import { onSignedOut } from './auth/session'
import { LoginForm } from './components/LoginForm'
import { ErrorView } from './components/ErrorView'
import { StokvelWorkspace } from './features/StokvelWorkspace'

// /me/home is the signed-in probe: it answers 401 when there is no session.
function AuthGate() {
  const home = useHome()
  if (home.isPending) return <p className="min-h-screen bg-slate-100 p-6 text-slate-700 dark:bg-slate-950 dark:text-slate-300">Loading…</p>
  if (home.error instanceof ApiError && home.error.status === 401) return <LoginForm /> // no toast on top
  if (home.isError) {
    return (
      <div className="min-h-screen bg-slate-100 p-6 dark:bg-slate-950">
        <ErrorView error={home.error} onRetry={() => home.refetch()} />
      </div>
    )
  }
  return <StokvelWorkspace />
}

export default function App() {
  const [epoch, setEpoch] = useState(0)

  useEffect(
    () =>
      onSignedOut(() => {
        // Button or failed refresh: wipe every cached query, then remount the tree so the
        // selected stokvel id (component state) goes too. Nothing is kept in web storage.
        void queryClient.cancelQueries()
        queryClient.clear()
        setEpoch((e) => e + 1)
      }),
    [],
  )

  return (
    <QueryClientProvider client={queryClient}>
      <AuthGate key={epoch} />
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  )
}