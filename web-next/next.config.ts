import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  reactCompiler: true,
  // Cache Components: pages are prerendered into a static shell, and anything that reads the
  // request (cookies, params, API calls) must sit inside <Suspense> and streams in afterwards.
  cacheComponents: true,
  // Partial Prefetching: a link prefetches only the static shell of a route, not its personal data.
  partialPrefetching: true,
  agentRules: false,
}

export default nextConfig