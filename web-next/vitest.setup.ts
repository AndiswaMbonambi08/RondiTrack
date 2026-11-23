import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'

// Only in jsdom tests: unmount what a test rendered, so tests never see each other's pages.
if (typeof document !== 'undefined') {
  const { cleanup } = await import('@testing-library/react')
  afterEach(cleanup)
}