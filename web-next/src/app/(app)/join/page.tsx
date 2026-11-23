import type { Metadata } from 'next'
import { Suspense } from 'react'
import { PageSkeleton } from '@/components/PageSkeleton'
import { JoinContent } from './JoinContent'

// A link-preview bot is signed out and the preview endpoint needs a signed-in person, so the head
// never depends on the invite. It says only that this is an invite. It does not say whether the
// invite is still valid, and it has no stokvel name, member name or amount.
export const metadata: Metadata = {
  title: 'You have been invited to a stokvel',
  description: 'Open this invite on RondiTrack to see the rules and join.',
  robots: { index: false, follow: false },
  openGraph: {
    title: 'You have been invited to a stokvel on RondiTrack',
    description: 'Open this invite to see the rules and join.',
    siteName: 'RondiTrack',
    type: 'website',
  },
}

export default function JoinPage({ searchParams }: PageProps<'/join'>) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <JoinContent searchParams={searchParams} />
    </Suspense>
  )
}