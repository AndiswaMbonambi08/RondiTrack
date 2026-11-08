import { Suspense } from 'react'
import { PageSkeleton } from '@/components/PageSkeleton'
import { JoinContent } from './JoinContent'

export default function JoinPage({ searchParams }: PageProps<'/join'>) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <JoinContent searchParams={searchParams} />
    </Suspense>
  )
}