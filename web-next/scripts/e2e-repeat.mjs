// Three Playwright runs in a row against the production build. A 429 or a flaky test fails the whole thing.
// One command string (no args array) so Node does not print the DEP0190 shell warning.
import { spawnSync } from 'node:child_process'

const runs = Number(process.argv[2] ?? 3)
for (let i = 1; i <= runs; i++) {
  console.log(`\n=== Playwright run ${i} of ${runs} ===`)
  const r = spawnSync('npx playwright test --grep-invert @perf', { stdio: 'inherit', shell: true })
  if (r.status !== 0) {
    console.log(`FAILED on run ${i}`)
    process.exit(1)
  }
}
console.log(`\nAll ${runs} runs passed.`)