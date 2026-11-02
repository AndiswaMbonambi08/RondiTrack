// Answers API check question 1 against the running API. Run from web-next:
//   $env:CHECK_EMAIL="you@example.com"; $env:CHECK_PASSWORD="your-password"; node scripts/api-check-refresh.mjs
// It prints statuses and yes/no answers only, never a token.
const base = process.env.API_BASE_URL ?? 'http://localhost:5051'
const email = process.env.CHECK_EMAIL
const password = process.env.CHECK_PASSWORD
if (!email || !password) throw new Error('Set CHECK_EMAIL and CHECK_PASSWORD first.')

const tokenFrom = (res) => {
  const line = res.headers.getSetCookie().find((c) => c.startsWith('refresh_token='))
  return line ? line.split(';')[0].slice('refresh_token='.length) : null
}
const refresh = (token) =>
  fetch(`${base}/api/v1/auth/refresh`, { method: 'POST', headers: { Cookie: `refresh_token=${token}` } })

const login = await fetch(`${base}/api/v1/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password }),
})
const first = tokenFrom(login)
console.log('login:', login.status, '| refresh token in Set-Cookie:', first !== null)

const r1 = await refresh(first)
const second = tokenFrom(r1)
const body = await r1.json().catch(() => ({}))
console.log('refresh with the first token:', r1.status, '| new token in Set-Cookie:', second !== null, '| body has accessToken:', 'accessToken' in body, '| expiresInSeconds:', body.expiresInSeconds)

const replay = await refresh(first)
console.log('replay of the first (spent) token:', replay.status)

const newer = await refresh(second)
console.log('the NEW token after the replay:', newer.status, '(401 means the whole family was revoked)')