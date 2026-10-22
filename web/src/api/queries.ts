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

// GET /stokvels lists every stokvel and is for platform admins only. A member's own stokvels,
// with their role in each, come from /me/home. One request and one cache entry serve all three hooks.
const homeQuery = {
  queryKey: queryKeys.home,
  queryFn: () => unwrap(api.GET('/api/v1/me/home')),
}

export const useStokvels = () =>
  useQuery({
    ...homeQuery,
    select: (home) => home.stokvels.map((s) => ({ id: s.stokvelId, name: s.name, role: s.role })),
  })

export const useStokvel = (id: string) =>
  useQuery({
    ...homeQuery,
    select: (home) => {
      const s = home.stokvels.find((x) => x.stokvelId === id)
      return s ? { id: s.stokvelId, name: s.name, role: s.role } : undefined
    },
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
          params: { path: { stokvelId: id }, query: { PageToken: pageParam } },
        }),
      ),
    // check 4: the API sends "" for no next page; null or undefined would also mean stop
    getNextPageParam: (last) => last.nextPageToken || undefined,
  })