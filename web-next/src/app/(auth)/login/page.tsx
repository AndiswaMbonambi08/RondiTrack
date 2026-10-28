import { LoginForm } from './LoginForm'

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ expired?: string }>
}) {
  const { expired } = await searchParams
  return (
    <main className="flex min-h-screen items-center justify-center p-6">
      <LoginForm expired={expired === '1'} />
    </main>
  )
}
