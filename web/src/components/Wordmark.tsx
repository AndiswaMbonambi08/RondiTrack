export function Wordmark({ className = 'text-xl' }: { className?: string }) {
  return (
    <span className={`font-extrabold tracking-tight ${className}`}>
      Rondi<span className="text-emerald-800 dark:text-emerald-300">Track</span>
    </span>
  )
}