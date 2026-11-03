'use client'

import { useEffect, useRef, type ReactNode } from 'react'

// Focus follows the step: the heading takes focus when the step appears.
export function StepHeading({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    ref.current?.focus()
  }, [])
  return (
    <h1 ref={ref} tabIndex={-1} className="text-2xl font-bold tracking-tight outline-none">
      {children}
    </h1>
  )
}