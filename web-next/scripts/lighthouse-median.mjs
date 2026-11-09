// Lighthouse, mobile profile (simulated slow 4G, 4x CPU slowdown), against `npm run start`, signed in as Sipho.
// Three runs per page, prints and saves the median.
//   node scripts/lighthouse-median.mjs before
//   node scripts/lighthouse-median.mjs after
// Needs: the stack from compose.e2e.yaml, `npm run start`, and a fresh `npx playwright test --project=setup`.
import { spawnSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'

const label = process.argv[2] ?? 'before'
const base = process.env.LH_BASE_URL ?? 'http://localhost:3000'
const api = process.env.E2E_API_URL ?? 'http://localhost:5051'
const RUNS = 3
const outDir = path.resolve('.lighthouse')
fs.mkdirSync(outDir, { recursive: true })

const state = JSON.parse(fs.readFileSync('e2e/.auth/sipho.json', 'utf8'))
const headersFile = path.join(outDir, 'headers.json')
fs.writeFileSync(headersFile, JSON.stringify({ Cookie: state.cookies.map((c) => `${c.name}=${c.value}`).join('; ') }))

function findByName(node, name) {
  if (Array.isArray(node)) {
    for (const n of node) {
      const f = findByName(n, name)
      if (f) return f
    }
    return null
  }
  if (node && typeof node === 'object') {
    if (node.name === name || node.stokvelName === name) return node
    for (const v of Object.values(node)) {
      const f = findByName(v, name)
      if (f) return f
    }
  }
  return null
}

const { sipho } = JSON.parse(fs.readFileSync('e2e/.auth/api.json', 'utf8'))
const home = await (await fetch(`${api}/api/v1/me/home`, { headers: { Authorization: `Bearer ${sipho.token}` } })).json()
const found = findByName(home, 'Load Test Stokvel')
const stokvelId = found?.id ?? found?.stokvelId
if (!stokvelId) throw new Error('Load Test Stokvel not found. Start the API with compose.e2e.yaml (Seed__LoadDemo=true).')

const pages = { home: `${base}/`, stokvel: `${base}/stokvels/${stokvelId}` }
const cli = path.resolve('node_modules/lighthouse/cli/index.js')
const median = (xs) => [...xs].sort((a, b) => a - b)[Math.floor(xs.length / 2)]

const summary = {}
for (const [pageName, url] of Object.entries(pages)) {
  const runs = []
  for (let i = 1; i <= RUNS; i++) {
    const file = path.join(outDir, `${label}-${pageName}-${i}.json`)
    const r = spawnSync(process.execPath, [
      cli, url, '--output=json', `--output-path=${file}`, '--quiet', '--only-categories=performance',
      `--extra-headers=${headersFile}`, '--chrome-flags=--headless=new --disable-extensions',
    ], { stdio: 'inherit' })
    if (r.status !== 0) throw new Error(`Lighthouse failed for ${url}`)
    const lhr = JSON.parse(fs.readFileSync(file, 'utf8'))
    runs.push({
      score: Math.round((lhr.categories.performance.score ?? 0) * 100),
      lcpMs: Math.round(lhr.audits['largest-contentful-paint'].numericValue),
      cls: Number(lhr.audits['cumulative-layout-shift'].numericValue.toFixed(3)),
      tbtMs: Math.round(lhr.audits['total-blocking-time'].numericValue),
      fcpMs: Math.round(lhr.audits['first-contentful-paint'].numericValue),
    })
  }
  summary[pageName] = {
    url,
    runs,
    median: Object.fromEntries(Object.keys(runs[0]).map((k) => [k, median(runs.map((r) => r[k]))])),
  }
}

fs.writeFileSync(path.join(outDir, `summary-${label}.json`), JSON.stringify(summary, null, 2))
console.log(`\nMedian of ${RUNS} runs (${label}):`)
console.table(Object.fromEntries(Object.entries(summary).map(([k, v]) => [k, v.median])))