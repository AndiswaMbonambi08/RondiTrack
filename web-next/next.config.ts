import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  reactCompiler: true,
  // Cache Components: pages are prerendered into a static shell, and anything that reads the
  // request (cookies, params, API calls) must sit inside <Suspense> and streams in afterwards.
  cacheComponents: true,
  // Partial Prefetching: a link prefetches only the static shell of a route, not its personal data.
  partialPrefetching: true,
  agentRules: false,
  experimental: {
    // A Server Action takes 1 MB by default. A proof of payment may be up to 5 MB (the API's limit),
    // so this hop allows a little more and the API stays the strictest, deciding by the file's bytes.
    serverActions: { bodySizeLimit: '6mb' },
  },
}

export default nextConfig