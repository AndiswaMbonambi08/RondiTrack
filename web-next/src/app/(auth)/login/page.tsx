import { Suspense } from 'react'
import { safeNext } from '@/lib/safeNext'
import { LoginForm } from './LoginForm'

// searchParams is request data, so it is read inside <Suspense>.
async function LoginContent({
  searchParams,
}: {
  searchParams: Promise<{ expired?: string; next?: string }>
}) {
  const { expired, next } = await searchParams
  return (
    <main className="flex min-h-screen items-center justify-center p-6">
      <LoginForm expired={expired === '1'} next={safeNext(next, '')} />
    </main>
  )
}

export default function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ expired?: string; next?: string }>
}) {
  return (
    <Suspense fallback={<main className="min-h-screen" />}>
      <LoginContent searchParams={searchParams} />
    </Suspense>
  )
}