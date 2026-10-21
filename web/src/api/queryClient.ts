import { QueryClient } from '@tanstack/react-query'
import { ApiError } from './apiError'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // 404, 403, 429 and friends will not fix themselves; retrying a 429 makes it worse.
      retry: (failureCount, error) =>
        !(error instanceof ApiError && error.status >= 400 && error.status < 500) && failureCount < 3,
    },
  },
})