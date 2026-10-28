'use client'

import { ErrorView } from '@/components/ErrorView'

// Catches what nobody expected. Expected API errors never reach here: readOrFail has already
// turned them into a redirect, a not-found page or an ErrorView with a reference.
export default function AppError({
  error,
  retry,
  reset,
}: {
  error: Error & { digest?: string }
  retry?: () => void
  reset?: () => void
}) {
  return (
    <ErrorView
      description={{
        kind: 'generic',
        title: 'Something went wrong',
        message: 'Something went wrong on this page. Try again, and quote the reference if it keeps happening.',
        reference: error.digest,
      }}
      onRetry={retry ?? reset}
    />
  )
}