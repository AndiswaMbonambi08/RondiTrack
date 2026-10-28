import Link from 'next/link'

// Same page for "no such stokvel" and "not yours", so it never confirms that a stokvel exists.
export default function StokvelNotFound() {
  return (
    <section className="space-y-3">
      <h1 className="text-2xl font-semibold">We couldn&rsquo;t find that stokvel</h1>
      <p>Check the link, or head back to your stokvels.</p>
      <Link href="/stokvels" className="inline-block rounded bg-emerald-700 px-4 py-2 text-white">
        Back to my stokvels
      </Link>
    </section>
  )
}