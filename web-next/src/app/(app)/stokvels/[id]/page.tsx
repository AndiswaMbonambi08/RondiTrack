import { Suspense } from 'react'
import { PageSkeleton } from '@/components/PageSkeleton'
import { StokvelContent } from './StokvelContent'

// Returns a <Suspense> at once. params, the session and every API call happen inside it.
export default function StokvelPage({ params }: PageProps<'/stokvels/[id]'>) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <StokvelContent params={params} />
    </Suspense>
  )
}