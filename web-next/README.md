# RondiTrack web-next

**Assignment 8.1: RondiTrack routes**

This is the RondiTrack web app moved to Next.js 16.4.1 (App Router) with React, Tailwind and the React Compiler. Four routes fetch their data in Server Components: `/stokvels`, `/stokvels/[id]`, `/dashboard/member` and `/dashboard/treasurer`. The session lives in an encrypted, HttpOnly cookie on this site. The browser never calls the API. The Next.js server does, with a token the browser cannot see.

It builds on Assignments 7.1 to 7.4 (the components, generated types and error messages). The Vite app in `web/` is unchanged. Parallel fetching, streaming and Server Actions for writes come on Day 2. Registration, silent refresh and route protection come on Day 3.

**Branch:** `assignment-8.1`
**Pull request:** Assignment 8.1: RondiTrack routes

## What is in this assignment

- The `web-next/` app with every dependency pinned exactly and a clean `npm audit`.
- A server-side session in `src/lib/session.ts` (encrypted with `jose`, `dir` and `A256GCM`) and a typed `openapi-fetch` client in `src/lib/api.ts`. Both start with `import 'server-only'`.
- Sign in and sign out as Server Actions, with a login page that shows the 7.3 message and the reference.
- The four routes, one signed-in layout, `loading.tsx`, `error.tsx` and `not-found.tsx`.
- The treasurer filter `stokvelsWhereTreasurer` with Vitest tests.
- On the API side: `GET /api/v1/stokvels/{id}` for members, with one 404 for everyone else, and integration tests for it.

## Contents

1. Running it
2. API check
3. Routes
4. The treasurer dashboard
5. The client boundary
6. Server-only proof
7. Known gaps

## Running it

Needs Node 24 and the RondiTrack API running on `http://localhost:5051`.

```powershell
cd web-next
copy .env.example .env.local   # then fill in both values
npm install
npm run dev
```

| Variable | What it is |
| --- | --- |
| `API_BASE_URL` | Address of the RondiTrack API, for example `http://localhost:5051` |
| `SESSION_SECRET` | 32-byte key that encrypts the session cookie. The command that generates one is in `.env.example` |

Neither name starts with `NEXT_PUBLIC_`. Next.js copies every `NEXT_PUBLIC_` variable into the JavaScript sent to the browser, and both values must only ever exist on the server.

Scripts: `npm run lint`, `npm run build`, `npm test` and `npm run typecheck`. The dev server uses port 3000, or the next free port if the Vite app in `web/` is running.

## API check

Answered against the running API. No API fixes were needed.

| # | Question | Answer |
| --- | --- | --- |
| 1 | Does the stokvel list return the caller's role? What for a user in no stokvels? | Yes. `GET /api/v1/me/home` returns the role for each stokvel (`Treasurer`, `Member` or `Admin`). A user in no stokvels gets 200 with `"stokvels":[]`, checked with a freshly registered user. |
| 2 | `GET /api/v1/stokvels/{id}` as a non-member, for a missing id and for `not-a-guid` | All three return 404. A malformed id returns 404, not 400, so the page needs no special case. |
| 3 | Which header carries the refresh token after login? | `Set-Cookie: refresh_token=...; path=/api/v1/auth; secure; samesite=strict; httponly`, valid for 7 days. The sign-in Server Action reads it with `response.headers.getSetCookie()`. |
| 4 | How long does the access token live? What happens after? | 900 seconds (15 minutes). An expired token gets a 401 problem+json answer. `readOrFail` sends the person to `/login?expired=1`. There is no refresh until Day 3. |
| 5 | Which IP does the API see when ten people sign in? | The IP of the Next.js server, for everyone. See Known gaps. |
| 6 | Does the detail endpoint return the member count and a cycle summary? | Yes. `GET /api/v1/stokvels/{id}` returns `memberCount` and a `currentCycle` with `label`, `status`, `dueDate`, `contributorCount` and `collectedCents`. |

## Routes

| Route | Shows | Nothing or not theirs |
| --- | --- | --- |
| `/` | Redirects to `/dashboard/member` when signed in and to `/login` when not | None |
| `/login` | Sign-in form (Server Action) | A wrong password shows the 7.3 message and the reference |
| `/dashboard/member` | Every stokvel the person is in, as `StokvelCard`s with paid, still owed and next payout | No stokvels: an empty state |
| `/dashboard/treasurer` | Only the stokvels where the person is Treasurer | Treasurer nowhere: an empty state |
| `/stokvels` | Every stokvel the person is in, with their role in each | No stokvels: an empty state |
| `/stokvels/[id]` | Name, contribution, role, member count and current cycle summary | Not a member or no such stokvel: `not-found.tsx` |

Every page is a Server Component that awaits its data. `params` is a Promise and the detail page is typed with `PageProps<'/stokvels/[id]'>`.

The list pages call `GET /api/v1/me/home` rather than `GET /api/v1/stokvels`. The second endpoint is for platform admins and returns 403 for an ordinary user. `/me/home` already carries the role for each stokvel. Sign-in also calls `GET /api/v1/me` once for the name and email.

## The treasurer dashboard

RondiTrack has no treasurer users. It has users who are Treasurer of some stokvels, so everyone may open `/dashboard/treasurer`. The page fetches `/me/home` and keeps the entries where `role === 'Treasurer'`. For most people that list is empty and the page says they do not run any stokvels yet, with a link to the member dashboard. It is not an error and not a 403.

- The filter is a pure function, `stokvelsWhereTreasurer`, in `src/lib/treasurer.ts`, with Vitest tests for a mixed list, a list with no Treasurer role and an empty list. Admin is not Treasurer.
- The role always comes from the API's answer for each stokvel. The session stores the tokens, the user id, the email and the full name, and no role. It cannot hold one: a role belongs to a person in one stokvel, so the same login can be Treasurer in one and Member in another. Membership can also change mid-session, and a stored role would keep saying Treasurer after the person was removed.
- The sidebar links are shown to everyone. Hiding the Treasurer link would only be a convenience. The page is safe either way because the API decides what each person may see.

## The client boundary

Files with `'use client'`:

| File | Why it needs it |
| --- | --- |
| `src/app/(app)/error.tsx` | Next.js requires error boundaries to be Client Components. It passes `retry` to the Try again button. |
| `src/app/(auth)/login/LoginForm.tsx` | `useActionState` drives the form and its error message. |
| `src/components/ErrorView.tsx` | `useState` and `useEffect` run the rate-limit countdown, and the Try again button has an `onClick`. |
| `src/components/DueCountdown.tsx` | Its clock hook uses state and an effect. |

`StokvelCard`, `ContributionCard`, `StatusBadge` and the pages stay Server Components. `ErrorView` receives a plain `ErrorDescription` object built on the server by `describeError`, never an `ApiError` instance. `LoginForm` receives only an `expired` boolean.

`LoginForm` imports the sign-in Server Action, and that file imports `session.ts` and `api.ts`. A `'use server'` module is not bundled into the browser: the client gets a reference that calls the server. `session.ts` and `api.ts` both start with `import 'server-only'`, so any direct import from a Client Component fails the build.

## Server-only proof

I added `import { getSession } from '@/lib/session'` to `LoginForm.tsx` and referenced it, then ran `npm run build`. The first attempt, which only imported it and never used it, still passed because the unused import is dropped. With the import in use, the build failed:

```text
./src/lib/session.ts:1:1
Error: You're importing a module that depends on "server-only".
Import traces:
  Client Component SSR:
    ./src/lib/session.ts [Client Component SSR]
    ./src/app/(auth)/login/LoginForm.tsx [Client Component SSR]
```

The message mentions the Pages Router. That is Next.js wording for any Client Component import of a server-only module. After `git checkout` restored the file, the build passed. Full outputs: `docs/proofs/server-only-fail.txt` and `docs/proofs/server-only-pass.txt`.

Without the guard, the module that reads and decrypts the session cookie and the client that attaches the Bearer token could be bundled into the browser's JavaScript. Token-handling code would then run in the browser, where this design says it must never run, and page code could call the API directly.

## Known gaps

- **Login rate limit.** The API limits login to 5 per 60 seconds per client IP. Every sign-in now comes from the Next.js server, so all users share one partition and the sixth sign-in in a minute is refused for everyone. The fix is to forward the person's IP and have the API trust that header only from the Next.js server. Not done this week.
- **No silent refresh.** The session ends when the access token expires after 15 minutes and the person is sent to `/login?expired=1`. Refresh is Day 3.
- **No registration page.** Accounts are created through the API. Registration is Day 3.
- **Disabled treasurer buttons.** `StokvelCard` shows Close cycle and Record payout as disabled placeholders from 7.1.
- **Due countdown not seen.** `DueCountdown` only renders when a cycle has a due date. None of the seeded open cycles has one, so it was not seen rendering.