// A plain helper (not a component), so the React Compiler's purity rule does not flag the clock.
// Used only for the timing numbers in the README.
export function nowMs(): number {
  return performance.now()
}