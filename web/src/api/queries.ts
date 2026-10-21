import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { api, unwrap } from './client'

// Every key for stokvel data starts with that stokvel's id (after 'stokvels'), so
// invalidateQueries({ queryKey: queryKeys.stokvel(id) }) touches one stokvel and no other.
export const queryKeys = {
  me: ['me'] as const,
  home: ['me', 'home'] as const,
  stokvels: ['stokvels'] as const,
  stokvel: (id: string) => ['stokvels', id] as const,
  cycles: (id: string) => ['stokvels', id, 'cycles'] as const,
  contributions: (id: string) => ['stokvels', id, 'contributions'] as const,
}

export const useMe = () =>
  useQuery({ queryKey: queryKeys.me, queryFn: () => unwrap(api.GET('/api/v1/me')) })

export const useHome = () =>
  useQuery({ queryKey: queryKeys.home, queryFn: () => unwrap(api.GET('/api/v1/me/home')) })

export const useStokvels = () =>
  useQuery({ queryKey: queryKeys.stokvels, queryFn: () => unwrap(api.GET('/api/v1/stokvels')) })

export const useStokvel = (id: string) =>
  useQuery({
    queryKey: queryKeys.stokvel(id),
    queryFn: () => unwrap(api.GET('/api/v1/stokvels/{stokvelId}', { params: { path: { stokvelId: id } } })),
  })

export const useCycles = (id: string) =>
  useQuery({
    queryKey: queryKeys.cycles(id),
    queryFn: () =>
      unwrap(api.GET('/api/v1/stokvels/{stokvelId}/cycles', { params: { path: { stokvelId: id } } })),
  })

// No placeholderData / keepPreviousData here: the key includes the stokvel id, and keeping the
// previous result would show the old stokvel's rows under the new one while it loads.
export const useContributions = (id: string) =>
  useInfiniteQuery({
    queryKey: queryKeys.contributions(id),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      unwrap(
        api.GET('/api/v1/stokvels/{stokvelId}/contributions', {
          params: { path: { stokvelId: id }, query: { pageToken: pageParam } },
        }),
      ),
    // check 4: "no next page" may be "" or null; both mean stop
    getNextPageParam: (last) => last.nextPageToken || undefined,
  })