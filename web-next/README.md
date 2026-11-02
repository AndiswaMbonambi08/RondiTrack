# RondiTrack web-next

**Assignment 8.2: RondiTrack data and sign-in** (covers the curriculum's 8.2 and 8.3 together)

Next.js 16.4.1 (App Router) with Cache Components. The browser never calls the API and never sees a token: the Next.js server does, with a session kept in an encrypted HttpOnly cookie. Builds on 8.1 (session, routes, treasurer filter) and 7.4 (the contribution form's money parser, Zod schema, error placement and Idempotency-Key rules).

**Branch:** `assignment-8.2`  **Pull request:** Assignment 8.2: RondiTrack data and sign-in

## Contents

1. Running it
2. API check
3. Cache Components
4. Parallel and streamed
5. Contributions
6. What we cache
7. Sessions and refresh
8. The race
9. Treasurer actions
10. Known gaps

## Running it

Needs Node 24 and the API on `http://localhost:5051`.

```powershell
cd web-next
copy .env.example .env.local   # fill in API_BASE_URL and SESSION_SECRET
npm install
npm run dev
```

| Variable | What it is |
| --- | --- |
| `API_BASE_URL` | Address of the API |
| `SESSION_SECRET` | 32-byte key that encrypts the session cookie |
| `REFRESH_MARGIN_SECONDS` | Optional. Refresh when the access token has fewer seconds left (default 120). Only for experiments |
| `FETCH_MODE` | Optional. `sequential` awaits the detail page's requests one per line, only to measure them |

Neither required name starts with `NEXT_PUBLIC_`, so neither reaches the browser. Scripts: `npm run lint`, `npm run build`, `npm test`, `npm run typecheck`. Production runs: `npm run build` then `npm start`.

## API check

Answered by reading the endpoints and their existing tests, and question 1 by `scripts/api-check-refresh.mjs` (Node, because the Next.js server is the API's caller now).

| # | Question | Answer |
| --- | --- | --- |
| 1 | `POST /auth/refresh` from Node with the cookie. Where is the new token? Then the old one again? | TODO(paste the script's output). 200 with `{accessToken, expiresInSeconds}` in the body; the new refresh token is only in `Set-Cookie: refresh_token=...`. Replaying the spent token returns 401 and revokes the whole family, so the new token stops working too. |
| 2 | Lifetimes | Access token 15 minutes (`Jwt:AccessTokenMinutes`), refresh token 7 days (`Jwt:RefreshTokenDays`, and every rotation issues a fresh 7 days). The session cookie is 7 days, rewritten at each refresh. The refresh token decides when a person must sign in again; the cookie cannot outlive it. On a shared phone, the next person has up to 7 days unless the first person signs out, which revokes the token family. |
| 3 | `POST /auth/register` | Exists. An existing email returns 409 "An account with this email already exists", which would tell a stranger who uses RondiTrack. The API is unchanged; the form shows a neutral message instead (see Known gaps). |
| 4 | Latest contributions, newest first, a few at a time | `GET /stokvels/{id}/contributions?PageSize=5` exists, but it returns only the caller's own contributions. "Recent contributions" is therefore the person's own newest five. |
| 5 | Member records for another member / non-member records | A Member recording for someone else gets 403. A non-member gets 404 (the route policy hides the stokvel). The page shows the 403 with its reference and stays signed in. |
| 6 | Idempotency-Key contract | Unchanged: replay returns the original with `Idempotency-Replayed: true`; same key with a different body is 422; a missing key is 400. |

No API changes were needed, so there are no new API tests.

## Cache Components

`next.config.ts` has `cacheComponents: true` and `partialPrefetching: true`, each with a one-line comment.

Routes `npx next build` named, and the fix for each:

TODO(fill in after running the build: route, what it was, what you did)

Every signed-in page returns a `<Suspense>` straight away, and the session read, `params` and the API calls happen inside it: `/stokvels`, `/stokvels/[id]`, `/dashboard/member`, `/dashboard/treasurer`. `/` only redirects, so its session read sits inside a `<Suspense>` too. `/login` reads `searchParams` inside one. TODO(confirm: no Insight in `npm run dev` while clicking between signed-in pages).

## Parallel and streamed

The detail endpoint already returns the member count and the current cycle (8.1, question 6), so those are not separate requests. The really independent requests are the stokvel (`/stokvels/{id}`), the caller's own figures (`/me/home`) and the recent contributions. The first two start together and are awaited together with `Promise.all`; the third starts at the same moment and is only awaited inside its own `<Suspense>` in `RecentContributions`, with skeleton rows shaped like the real ones. Members are fetched only for a Treasurer or Admin, inside the form's own boundary, because the role is only known after the detail answer.

A non-member or unknown id still gets `not-found.tsx`, now from inside the boundary. Client Components receive ids, names and cents, never whole API responses.

Measured from the server log (production build, `FETCH_MODE=sequential` vs default):

| Version | Top of the page | Recent contributions |
| --- | --- | --- |
| One await per line | TODO ms | included in the top |
| Parallel and streamed | TODO ms | arrives TODO ms after the request started |

## Contributions

The form is a Client Component on `useActionState`; the work is in the Server Action `recordContribution`. The action checks for itself, in order: the session, the Zod schema (the 7.4 schema and cents parser, unchanged), then the API. Expected errors are returned, not thrown: 400 next to its fields, 409 and 422 with their detail, 429 with the wait, 403 and 404 with their messages, each with the reference. A 401 signs the person out and goes to `/login?expired=1`. The form shows again what the person typed after any failure (the inputs take their `defaultValue` from the action's state, so React's reset after an action keeps them). The button is disabled while pending.

**The Idempotency-Key** is a hidden field. The server writes the first one when it renders the form; after that, every answer from the action carries the key for the next submit, and a hidden `keyIntent` field says which payment (stokvel, cycle, member, cents) the key was last used for. The 7.4 table, row by row:

| 7.4 row | What the Server Action version does |
| --- | --- |
| First submit | Uses the server-written key from the hidden field. |
| Retry after an unknown outcome (network error, 5xx) | The action returns the same key and the same `keyIntent`, and the retry sends the same key. |
| 429 | Same key kept; the form shows the wait and "Try again". |
| 400, 409, 422 | Same key kept (nothing was recorded for it). |
| Success | The action returns a brand new key and an empty `keyIntent`: the next contribution is a new intent. |
| Changed amount or member after a failure | `keyIntent` no longer matches the new payment, so the action makes a new key. |
| Failed Zod check | Nothing is sent; key and intent stay as they were. |
| Double click | The button is disabled while pending, and even a second request would carry the same key and be replayed by the API. |

**The new total with no reload:** the action calls `refresh()` after a success, not `updateTag`, because nothing is cached under a tag (see What we cache): the total is uncached data on the page, so `refresh()` re-renders it and the new total arrives in the action's own response.

**Without `refresh()`:** I commented the call out and recorded a contribution. TODO(confirm what you saw). The success message appeared but the total stayed old, because the action's answer only travels back through `useActionState`; the page's Server Components are not re-run unless the action calls `refresh()` or `updateTag()`.

## What we cache

Nothing from the API is cached: there is no `"use cache"`, no `"use cache: private"` and no `cacheTag` in the code. Cache Components only makes the static shell (frame, headings, skeletons) prerendered.

| Thing | Cached? | Why |
| --- | --- | --- |
| A member's balance (`owedCents`, `paidCents`, arrears from `/me/home`) | No | It belongs to one person, and a cache key built from the access token or user id would put an identity in a plain-text key. Always fetched per request. |
| Contributions and the cycle total | No | Money that changes on every payment, and the person must see it update at once. |
| Access token, refresh token, email | No | Keys and tags are stored in plain text, and a cached function would need the token as an argument. That settles it. |
| Public holidays or other data identical for everyone | Not used | Nothing in this assignment needs it. |

## Sessions and refresh

- `proxy.ts` is the only place a refresh happens. It reads the session cookie, and when the access token has less than 120 seconds left it calls the API once. 120 s is more than the API's 30 s clock skew plus a slow page that makes several API calls, while still using most of each 15-minute token.
- It writes the new session twice: into the request (`request.cookies.set` then `NextResponse.next({ request: { headers } })`), so the page about to render sees it, and onto the response (`response.cookies.set`), so the browser keeps it.
- A refused refresh (401) deletes the session and goes to `/login?expired=1`. A refresh that could not reach the API, or got a 5xx, keeps the session: it says nothing about the session.
- No session on a signed-in page goes to `/login?next=<path>`; signing in returns there. `safeNext` follows only a plain path on this site and refuses `//evil.example`, `/\evil.example`, absolute URLs and the login/register pages (Vitest tests). Pages and actions still check the session themselves.
- Register and sign in are Server Actions with `useActionState` in `(auth)`. A successful registration signs the person in and goes to `/dashboard/member`. Fields have real labels and `autoComplete` (`username`, `current-password`, `new-password`), and paste is never blocked.
- Sign out is a POST (a form action). It revokes the refresh token at the API and deletes the session even if that call fails.

## The race

**Provoked first:** `REFRESH_MARGIN_SECONDS=100000` in `.env.local`, a production build, sign in, then in the Console: `await Promise.all([1, 2, 3].map(() => fetch('/stokvels')))`, with the sharing in `refresh.ts` temporarily removed. The API's reuse warning:

```text
TODO(paste the "Refresh token reuse detected; session family revoked" log line)
```

**Fix chosen:** one shared refresh inside the Next.js server (`refreshOnce` in `src/lib/refresh.ts`). Everyone who arrives with the same refresh token shares one call to the API, and for 20 seconds after it finished a late request gets the same result instead of replaying a spent token. The API's reuse detection is untouched.

- *Handles:* three tabs, or three requests of one tab, hitting one Next.js server at once; a request that left the browser just before the new cookie arrived, if it reaches the server within 20 seconds.
- *Does not handle:* several Next.js servers (each has its own memory, so two servers can still replay one token; that needs a shared store or a grace window in the API); a stale request arriving after the 20 seconds; a server restart in the middle of a refresh.
- *Stolen tokens:* unchanged. A stolen refresh token still works until it is used or revoked, up to 7 days; the 20-second memory only briefly keeps the new tokens in this server's memory (never logged).

**Proof with the fix:** TODO(three tabs opened at once and the Console line again, with no reuse warning in the API's log; paste the server's `[refresh]` lines, and optionally the 20-minute session's lines with times).

## Treasurer actions

The member picker (and the Treasurer buttons on the cards) appears only when this person's role in THIS stokvel, from the API's answer for this stokvel, is Treasurer. Not from the session, not from the token, not from "Treasurer anywhere". The detail page passes `stokvel.role` to `ContributionSection`, which fetches the member list (an endpoint that is itself Treasurer/Admin only) and only then shows the picker. The API allows Admins to record for others as well, so `canRecordForOthers` accepts Treasurer or Admin.

A 403 never signs anyone out and never looks like a 401; a 404 for a stokvel you are not in still looks like not found.

**Forced once, on purpose:** the picker was shown to everyone, a contribution was recorded for someone else as a Member, and the page showed the 403 message with its reference, signed in and with the form still usable. TODO(screenshot here). The condition was then put back.

## Known gaps

- **Register can reveal an email.** The API still answers 409 for an existing email. The form hides that behind a neutral sentence, but someone could compare it with sign-in. A real fix is a "check your email" flow, which needs email sending.
- **Recent contributions are the person's own** (the API only returns those), not the whole stokvel's.
- **Refresh sharing is per process** (see The race).
- **Login rate limit** is per IP and the API sees the Next.js server's IP for everyone (from 8.1).
- **`partialPrefetching`** is set at the top level of the config as the assignment says; if the installed Next.js expects it elsewhere, the build tells us.
- TODO(add anything the build, lint or testing turns up).