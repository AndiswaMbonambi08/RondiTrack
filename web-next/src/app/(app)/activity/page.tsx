import { Suspense } from 'react'
import { PageSkeleton } from '@/components/PageSkeleton'
import { ActivityContent } from './ActivityContent'

export default function ActivityPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <ActivityContent />
    </Suspense>
  )
}