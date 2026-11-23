// Ten runs in a row. A test that fails even once is flaky and must be fixed.
import { spawnSync } from 'node:child_process'

const runs = Number(process.argv[2] ?? 10)
for (let i = 1; i <= runs; i++) {
  console.log(`\n=== run ${i} of ${runs} ===`)
  const r = spawnSync('npx', ['vitest', 'run'], { stdio: 'inherit', shell: true })
  if (r.status !== 0) {
    console.log(`FAILED on run ${i}`)
    process.exit(1)
  }
}
console.log(`\nAll ${runs} runs passed.`)