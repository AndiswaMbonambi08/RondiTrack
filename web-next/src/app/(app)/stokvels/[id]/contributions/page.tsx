import { Suspense } from 'react'
import { PageSkeleton } from '@/components/PageSkeleton'
import { ContributionHistory } from './ContributionHistory'

// Returns a <Suspense> at once. params, searchParams, the session and the API calls happen inside it.
export default function ContributionsPage({ params, searchParams }: PageProps<'/stokvels/[id]/contributions'>) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <ContributionHistory params={params} searchParams={searchParams} />
    </Suspense>
  )
}