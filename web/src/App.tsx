import { useEffect, useState } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { queryClient } from './api/queryClient'
import { ApiError } from './api/apiError'
import { useMe } from './api/queries'
import { onSignedOut } from './auth/session'
import { LoginForm } from './components/LoginForm'
import { ErrorView } from './components/ErrorView'
import { StokvelWorkspace } from './features/StokvelWorkspace'

function AuthGate() {
  const me = useMe()
  if (me.isPending) return <p className="min-h-screen bg-slate-100 p-6 text-slate-700 dark:bg-slate-950 dark:text-slate-300">Loading…</p>
  if (me.error instanceof ApiError && me.error.status === 401) return <LoginForm /> // no toast on top
  if (me.isError) {
    return (
      <div className="min-h-screen bg-slate-100 p-6 dark:bg-slate-950">
        <ErrorView error={me.error} onRetry={() => me.refetch()} />
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