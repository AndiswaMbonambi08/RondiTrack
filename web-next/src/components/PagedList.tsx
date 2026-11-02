'use client'

import { useEffect, useRef, type ReactNode } from 'react'
import { useRouter } from 'next/navigation'
import { useInfiniteQuery } from '@tanstack/react-query'
import type { Page } from '@/lib/paged'

type Props<T> = {
  /** Everything the list is filtered by. The page-one token is added to it below. */
  queryKey: readonly unknown[]
  urlFor: (pageToken: string) => string
  /** Page one, rendered by the server. */
  initialPage: Page<T>
  getKey: (item: T) => string
  renderItem: (item: T) => ReactNode
  nouns: [one: string, many: string]
  empty: ReactNode
}

// The browser only ever calls this site (/bff/...), never the API.
class SessionEnded extends Error {}

async function fetchPage<T>(url: string): Promise<Page<T>> {
  const res = await fetch(url, { headers: { Accept: 'application/json' } })
  if (res.status === 401) throw new SessionEnded()
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
  return (await res.json()) as Page<T>
}

export function PagedList<T>({ queryKey, urlFor, initialPage, getKey, renderItem, nouns, empty }: Props<T>) {
  const sentinel = useRef<HTMLDivElement>(null)
  const router = useRouter()

  // Page one came from the server, so this query holds the pages AFTER it. Disabled on purpose:
  // fetchNextPage() fetches the first extra page with initialPageParam (page one's token).
  // The token is part of the key, so pages cached for an older page one are never reused.
  const query = useInfiniteQuery({
    queryKey: [...queryKey, initialPage.nextPageToken],
    queryFn: ({ pageParam }) => fetchPage<T>(urlFor(pageParam)),
    initialPageParam: initialPage.nextPageToken,
    getNextPageParam: (last) => last.nextPageToken || undefined,
    enabled: false,
  })
  const { fetchNextPage, isFetching, isFetchingNextPage, isError, error } = query

  // A 401 from /bff/ means the session is over: go to the login page.
  useEffect(() => {
    if (error instanceof SessionEnded) router.push('/login?expired=1')
  }, [error, router])

  const items = [...initialPage.items, ...(query.data?.pages.flatMap((p) => p.items) ?? [])]
  // Before the first extra page has arrived, page one's own token says whether there is more.
  // So the list never claims to be complete while that page is still loading.
  const more = query.data ? query.hasNextPage : initialPage.nextPageToken !== ''

  const loadMore = () => {
    if (!isFetching) void fetchNextPage({ cancelRefetch: false })
  }

  useEffect(() => {
    const node = sentinel.current
    if (!node || !more || isFetching || isError) return
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting)) void fetchNextPage({ cancelRefetch: false })
    })
    observer.observe(node)
    return () => observer.disconnect()
  }, [more, isFetching, isError, fetchNextPage, items.length])

  if (items.length === 0 && !more) return <>{empty}</>

  const count = items.length
  const status = `Showing ${count} ${count === 1 ? nouns[0] : nouns[1]}.${
    !more ? ' You have reached the end.' : isFetchingNextPage ? ' Loading more...' : ''
  }`

  return (
    <div className="space-y-3">
      <ul className="space-y-2">
        {items.map((item) => (
          <li key={getKey(item)}>{renderItem(item)}</li>
        ))}
      </ul>

      <div ref={sentinel} aria-hidden="true" className="h-1" />

      {isError && (
        <p role="alert" className="text-sm text-red-700 dark:text-red-300">
          We could not load more. Try again.
        </p>
      )}

      {more && (
        <button
          type="button"
          onClick={loadMore}
          aria-disabled={isFetching}
          className="min-h-11 rounded-full bg-secondary px-5 py-2 text-sm font-semibold text-secondary-foreground hover:bg-accent aria-disabled:opacity-60"
        >
          {isFetchingNextPage ? 'Loading...' : isError ? 'Try again' : 'Load more'}
        </button>
      )}

      <p role="status" className="text-sm text-muted-foreground">
        {status}
      </p>
    </div>
  )
}