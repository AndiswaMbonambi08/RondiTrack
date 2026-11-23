import type { MetadataRoute } from 'next'

// Deliberately no Disallow lines. Signed-in pages are kept out of search results with noindex,
// and a crawler blocked here would never see that tag. /bff/ uses an X-Robots-Tag header.
export default function robots(): MetadataRoute.Robots {
  return { rules: { userAgent: '*', allow: '/' } }
}