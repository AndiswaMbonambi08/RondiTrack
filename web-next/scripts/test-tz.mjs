// The same suite in the two time zones that matter: CI (UTC) and the members (South Africa).
import { spawnSync } from 'node:child_process'

let failed = false
for (const tz of ['UTC', 'Africa/Johannesburg']) {
  console.log(`\n=== TZ=${tz} ===`)
  const r = spawnSync('npx', ['vitest', 'run'], { stdio: 'inherit', shell: true, env: { ...process.env, TZ: tz } })
  if (r.status !== 0) failed = true
}
process.exit(failed ? 1 : 0)