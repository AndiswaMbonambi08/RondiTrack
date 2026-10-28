export default function Loading() {
  return (
    <div className="space-y-4" aria-busy="true" aria-label="Loading">
      <div className="h-8 w-48 animate-pulse rounded-full bg-muted" />
      <div className="grid gap-4 lg:grid-cols-2">
        {[0, 1, 2].map((i) => (
          <div key={i} className="h-48 animate-pulse rounded-3xl bg-muted" />
        ))}
      </div>
    </div>
  )
}