import { Suspense } from 'react'
import { redirect } from 'next/navigation'
import { getSession } from '@/lib/session'

// This page only redirects. The session read is request data, so it sits inside <Suspense>.
async function GoHome(): Promise<never> {
  const session = await getSession()
  redirect(session ? '/dashboard/member' : '/login')
}

export default function Home() {
  return (
    <Suspense fallback={null}>
      <GoHome />
    </Suspense>
  )
}