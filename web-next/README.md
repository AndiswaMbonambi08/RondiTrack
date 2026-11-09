# RondiTrack web-next

**Assignment 8.3: RondiTrack state** (the curriculum's Assignment 8.4)

Next.js 16.4.1 with Cache Components. The browser never calls the API: pages, Server Actions and the `/bff/` Route Handlers call it from the server with a token the browser cannot see. This assignment adds a contribution history with filters in the URL, an activity feed that loads page after page, and pinned stokvels in a small client store. It builds on 8.1 and 8.2 (session, `proxy.ts`, the contribution Server Action).

**Branch:** `assignment-8.3`  **Pull request:** Assignment 8.3: RondiTrack state

Versions pinned: nuqs 2.10.2, TanStack Query 5.104.2, Zustand 5.0.15.

## Contents

1. API check
2. Where each piece of state lives
3. Filters
4. Dates
5. Paging
6. Pinned stokvels
7. Known gaps

## API check

Answered against the code and its tests (`RondiTrack.Tests/Integration/HistoryAndActivityTests.cs`). The existing `GET /stokvels/{id}/contributions` only returns the caller's own rows, so I added new endpoints instead of changing it.

| # | Question | Answer |
| --- | --- | --- |
| 1 | Filter by member and time range, and page with `pageSize` and `pageToken`? | Not before. Now `GET /api/v1/stokvels/{id}/contribution-history` takes `member`, `from`, `to`, `sort` (`newest` or `oldest`), `pageSize` and `pageToken`, and returns `{ items, nextPageToken }`. A Member sees only their own rows (asking for someone else is 403). A Treasurer or Admin sees everyone's. |
| 2 | What does the time range take? | Two UTC instants such as `2026-09-30T22:00:00Z`. `from` is included (`RecordedAt >= from`) and `to` is excluded (`RecordedAt < to`). A date with no time, or an instant with no zone, is a 400, so a date can never be read as midnight UTC. The conversion from South African dates happens on the Next.js side only, in `southAfricanDaysToUtcRange`. |
| 3 | Is there an activity feed? | Not before. Now `GET /api/v1/me/activity` returns contributions, payouts and members joining across the stokvels the caller belongs to, newest first, paged by `pageToken`. It only contains stokvels the caller belongs to. A Member sees only their own contributions. Other people's names are shown only to Treasurers and Admins. |
| 4 | A `pageToken` from one filter with a different filter? | 400 problem+json saying the token does not match the current filter or sort. The token carries a hash of stokvel, member, range and sort. The feed's token is bound to the caller. Both are tested. |
| 5 | `?member=` for someone not in the stokvel, and a malformed date? | A malformed date, a malformed member id, a bad sort or `to` before `from` are all 400 problem+json. A well-formed member id that is not in this stokvel gives 200 with an empty list, so the endpoint cannot be used to find out who belongs where. A non-member of the stokvel gets 404. No token gets 401. |

```text
dotnet test --filter HistoryAndActivityTests
Passed!  - Failed:     0, Passed:    16, Skipped:     0, Total:    16, Duration: 20 s - RondiTrack.Tests.dll (net10.0)
```

## Where each piece of state lives

| State | Where it lives | Why |
| --- | --- | --- |
| Member, from, to and sort filters | The URL (nuqs) | A link must show the same list to another treasurer. It survives reload and Back. |
| Page one of the history and of the feed | The server (rendered from the URL or the session) | It is data from the API. The server fetches it with the token. |
| Later pages of the history and of the feed | TanStack Query cache in the browser, fetched from `/bff/` | Server data that arrives page by page. The key holds the filters and page one's token. |
| Fetching, error and "end reached" for the lists | TanStack Query | Derived from the requests. Not copied into other state. |
| "The list is updating" after a filter change | `useTransition` in `HistoryFilters` | Only the controls need it, and only while the server renders again. |
| Pinned stokvels (id and name) | Zustand store, saved in `localStorage` | No single page owns it, several components read it, and the API does not store it. |
| Owner of the saved pins | Same store | Lets a different signed-in user clear them. |
| Tokens and the person's name and email | Encrypted HttpOnly session cookie | Never in browser JavaScript. |
| Which stokvel and cycle a payment is for, the Idempotency-Key | The contribution form and its Server Action (8.2) | Unchanged. |

## Filters

| Filter | Parser | Default |
| --- | --- | --- |
| `member` | A custom parser: a UUID (any case, stored lower case). Anything else is ignored. | Empty (everyone) |
| `from` | A custom parser: a real `YYYY-MM-DD` date. `2026-02-30` is ignored. | None |
| `to` | The same parser | None |
| `sort` | `parseAsStringLiteral(['newest', 'oldest'])` | `newest` |

- One file, `src/lib/historyParsers.ts`, imported from `nuqs/server` by the page (`createLoader`), the Route Handler and the controls (`useQueryStates`). A Vitest test shows `?member=banana&from=2026-02-30&to=yesterday&sort=sideways` falls back to the defaults.
- `clearOnDefault` is on, so the unfiltered page is `/stokvels/{id}/contributions` with nothing after it.
- `history: 'push'`: each filter change is a deliberate step the person may want to go Back from.
- The server renders page one inside `<Suspense>`. The controls use `shallow: false` and show "Updating the list..." in a `role="status"` line while the server renders again.
- A filter that matches nothing shows "No contributions match these filters." with a "Show everything" link. A Member who types another member's id gets a plain message and a link back, not an error page.
- Dates stay as `YYYY-MM-DD` text in the URL. `parseAsIsoDate` is not used.

## Dates

- `src/lib/southAfricanDays.ts` holds the one function, `southAfricanDaysToUtcRange(from, to)`. It uses only `Date.UTC` with a fixed 2 hour offset, never the machine's time zone. South Africa has no daylight saving.
- `from` is 00:00 SAST on the first day. `to` is 00:00 SAST on the day after the last day, excluded. No `23:59:59` anywhere.
- It is called in one place, `fetchHistoryPage` in `src/lib/bff.ts`, which both the page and the Route Handler use. The browser sends plain dates to `/bff/`.
- Rules: an end left empty stays open. If `to` is before `from` the two are swapped. A date that does not exist throws a `RangeError`. In the page and the handler the parser has already turned such a date into "no filter", so the person never sees the error.
- Times in the list are shown with `formatDateTime`, which uses `Africa/Johannesburg`. Thandi's 23:30 on 31 October reads as 31 October.
- Tests (`southAfricanDays.test.ts`): 1 to 31 October bounds, 23:30 SAST in and 00:00 SAST out, a single day is 24 hours, one end only, `to` before `from`, a year end, a leap day and rejected dates.

Passing runs in three time zones (`npx vitest run src/lib/southAfricanDays.test.ts`):

```text
TZ=UTC
 Test Files  1 passed (1)
      Tests  14 passed (14)

TZ=Africa/Johannesburg
 Test Files  1 passed (1)
      Tests  14 passed (14)

TZ=America/New_York
 Test Files  1 passed (1)
      Tests  14 passed (14)
```

## Paging

- **Browser to API:** never. More pages come from `/bff/activity` and `/bff/stokvels/{id}/history`. Each checks the session itself and answers JSON. No session is a JSON 401. `proxy.ts` answers `/bff/` paths with a JSON 401 too (no session or a refused refresh), never a redirect. The handlers pass on only a status and a reference, never the API's problem text.
- **useInfiniteQuery:** `initialPageParam` is the token page one ended with. `getNextPageParam` returns `undefined` for an empty token. The query key is the list's filters plus page one's token. The query is disabled, and `fetchNextPage` fetches the first extra page, because page one comes from the server.
- **Scrolling and keyboard:** an `IntersectionObserver` on the end of the list loads the next page. A real "Load more" button does the same. A `role="status"` line says how many items are showing and when the end is reached.
- **Not claiming "all" too early:** until the first extra page has arrived, "more" comes from page one's own token. The button stays (it uses `aria-disabled`, so keyboard focus is kept) and the status line only says the end is reached when the API's token is empty.
- **A filter change starts again from page one:** the list has `key={JSON.stringify(filters)}`, the server renders new page one, and the query key holds the filters and page one's token, so a page from the old filter cannot appear under the new one. The app never sends a token with other filters.
- **A session that ends while scrolling:** a 401 from `/bff/` makes the list send the person to `/login?expired=1` through the router.
- **The 400 proof:** the request that would send the wrong token would get a 400 from the API (covered by HistoryAndActivityTests). The app never makes it, because the token only ever comes from a response to the same filters and the handler builds the request from the same filters.

```text
GET .../contribution-history?pageSize=1                                          -> 200, nextPageToken=eyJRdWVyeUhhc2giOiJDMzlG...
GET .../contribution-history?pageSize=1&pageToken=<that token>                    -> 200
GET .../contribution-history?pageSize=1&to=2030-01-01T00:00:00Z&pageToken=<that token>
  -> 400
  pageToken does not match the current filter or sort. (application/problem+json)
```

- **After recording a contribution:** the Server Action calls `refresh()` (the stokvel page) and `revalidatePath` for `/activity` and the history page, which refreshes the server's page one and the router's cached copy. The form then removes the `history` and `activity` queries from TanStack's cache, so later pages are fetched again. Page one's token is also part of the key, so an old cached page is not reused once page one changes.

## Pinned stokvels

- The store is a factory (`createStore` from `zustand/vanilla`) in `src/stores/pinsStore.ts`. `PinsProvider` makes one store with `useState`, and `usePinsStore` reads it from context. There is no module-level store, and no Server Component imports it.
- A pin holds only the stokvel id and name.
- `persist` with `skipHydration: true`. The provider calls `rehydrate()` in an effect, so the first client render matches the server. What is read back is checked for shape.
- **Sign out:** the sign-out form is a Client Component. As the form is sent it clears the store and removes the saved key. The Server Action cannot touch `localStorage`.
- **Expired or refused session, with no sign-out:** every route to a signed-out state ends on `/login`, and `/login` and `/register` remove the saved pins when they show. As a backstop the store also records the signed-in user's id. If saved pins belong to a different user, they are cleared before they are shown.
- **A pin for a stokvel the member has left:** it opens the not-found page and then removes itself. This needs no extra API call on every page, and the not-found page already never confirms whether a stokvel exists.

## Known gaps

- `src/lib/schema.d.ts` was not regenerated, so the two new endpoints use the hand-written types in `src/lib/paged.ts` and a plain `fetch` in `src/lib/bff.ts`.
- The contribution status is shown as the text the API sends. If the API sends it as a number the badge shows that number.
- Opening `/login` while signed in removes the saved pins.
- Pins are not limited in number.
- A Member who types another member's id sees a plain message (the API's 403), not the list.
- A platform admin who is not a member of a stokvel gets the not-found page there.
- Refresh sharing is per process (from 8.2).

---

# Assignment 9.2: Tests, performance and accessibility

## Plan

| # | Question | Answer |
| --- | --- | --- |
| 1 | Which rules are pure functions, and which were buried? | Pure: `parseRandsToCents`, `centsToInput`, `MAX_CENTS` (`src/money/money.ts`), `formatCents`, the South African date helpers (`dates`, `southAfricanDays`), `historyParsers`, `joinMachine`, `proofRules`, `safeNext`, `contributionErrors`. **Buried, now moved out:** the Idempotency-Key rule was inside the `recordContribution` Server Action and is now `lib/idempotency.ts` (`intentOf`, `chooseKey`). The treasurer-only block was inside `StokvelContent.tsx` and is now `components/TreasurerActions.tsx`, so a test can render it. |
| 2 | Which tool for each part? | **Vitest** for pure rules (cheapest, no browser). **Vitest + React Testing Library + userEvent** for client components with a fake Server Action: the contribution form and the role views. Vitest cannot render async Server Components, so nothing here tries. **Playwright** for anything that needs a page, a cookie, a redirect, a header or the real API: the journey, the 403, the signed-out redirect, axe, noindex and the invite head. **Manual** for what only a person can judge: keyboard order, what a screen reader says, 400% zoom, reduced motion. |
| 3 | Edges of the contribution form, and what stands in for each | The Server Action `recordContribution` is replaced with `vi.fn()` at the module boundary. The router (`next/navigation`) and the other actions (`createInvite`, `closeCycle`) are replaced the same way in the role test. Nothing inside the component (state, hooks) is mocked, so the tests exercise the real component. The URL plays no part in this form. |
| 4 | Login limit, and how tests sign in | `appsettings.json` allows 5 logins per 60 s (`RateLimit:Login`). The `setup` project signs in **once**: Sipho in the browser, saved to `e2e/.auth/sipho.json`. It also makes two API logins (Sipho, Thandiwe) so tests can create their own data. That is 3 logins per run, and the API login waits and retries once on a 429. Every test saves the session back afterwards, because the refresh token rotates. CI starts Compose with a fresh database. The seeded users get a password generated in that run and masked in the logs, so no real secret is in the workflow. `e2e/.auth` is git-ignored. |
| 5 | Routes | See "Metadata and indexing". |
| 6 | LCP element on a phone, and what it waits for | The LCP element on the home screen is ``. See the Performance section for what it waits for. |
| 7 | Dragging, and typing from memory | Nothing in `web-next` is reordered by dragging (there is no payout-rotation screen in this app), so SC 2.5.7 has nothing to fix here. Typing from memory: the sign-in form uses `autocomplete="username"` and `"current-password"` (checked in `LoginForm.tsx`) and has no paste blocker. The invite code can be pasted and stays in the URL, so Back never asks for it again (SC 3.3.7, 3.3.8). |

## Running the tests

```text
Unit and component tests (no server needed):
  cd web-next
  npm test                 # Vitest once
  npm run test:tz          # Vitest with TZ=UTC and TZ=Africa/Johannesburg
  npm run test:repeat      # ten runs in a row

End-to-end tests (production build, never next dev). Must be running first:
  docker compose -f compose.yaml -f compose.e2e.yaml up -d --build     # API on localhost:5051, database, seed incl. the 40-member stokvel
  $env:SEED_DEMO_PASSWORD = '<the demo password in .env>'
  $env:API_BASE_URL = 'http://localhost:5051'                           # web-next/.env.local also needs SESSION_SECRET
  cd web-next
  npm run build
  npx playwright install chromium                                       # once
  npm run test:e2e                # starts `npm run start` itself and runs setup first
  npm run test:e2e:repeat         # three runs in a row
  npm run test:e2e:perf           # INP (web-vitals, 4x CPU), excluded from CI

Lighthouse (mobile profile, median of 3, `npm run start` running):
  npx playwright test --project=setup
  npm run lh -- before            # and, after the fixes: npm run lh -- after
```

## What's checked where

| Behaviour | Test file or manual check | Tool |
| --- | --- | --- |
| Money parsing: South African formats, refusals, maximum, no float, round trip | `src/money/money.edge.test.ts`, `src/money/money.test.ts` | Vitest |
| Dates in both time zones (23:30 on the last day) | `src/lib/dates.test.ts`, `src/lib/southAfricanDays.test.ts`, `npm run test:tz` and both CI steps | Vitest, TZ=UTC and TZ=Africa/Johannesburg |
| One click, one request. Same key on a retry, a different key for the next contribution | `src/components/ContributionForm.test.tsx` | Vitest + RTL + userEvent, fake Server Action |
| Every answer the form shows (400 under the field, 409 with reference, success, typed value kept) | `src/components/ContributionForm.test.tsx`, `src/lib/contributionErrors.test.ts` | Vitest + RTL |
| Key rule | `src/lib/idempotency.test.ts` | Vitest |
| Roles belong to a stokvel (treasurer in one, member in the other) | `src/components/roles.test.tsx` (fixtures typed from the generated API types, `queryByRole`) | Vitest + RTL |
| The UI is not the rule: a member gets the API's 403 and stays signed in | `e2e/roles.spec.ts` | Playwright |
| Signed-out redirect with `?next=`, JSON 401 from `/bff/` | `e2e/signed-out.spec.ts` | Playwright |
| The journey: record R 150,50, total up by exactly 15 050 cents, no reload | `e2e/journey.spec.ts` | Playwright |
| WCAG 2.2 AA axe on the home screen, a stokvel, the form with errors, the Close Cycle dialog | `e2e/a11y.spec.ts` | Playwright + axe |
| noindex on signed-in pages, `X-Robots-Tag` on `/bff/`, no Disallow in robots.txt, invite head has no name or amount | `e2e/metadata.spec.ts` | Playwright |
| INP under 200 ms | `e2e/inp.spec.ts` (`@perf`, not in CI) | Playwright + web-vitals |
| LCP, CLS, TBT | `scripts/lighthouse-median.mjs` | Lighthouse |
| Keyboard order, screen reader, 400% zoom, reduced motion | Manual, see "Accessibility" | Keyboard, NVDA or VoiceOver |

## Accessibility

axe covers part of the problem. It cannot tell whether focus went to the right place or whether an announcement made sense, so the last column says who found each row.

| Criterion | Flow | What failed | Fix | How checked | Found by |
| --- | --- | --- | --- | --- | --- |
| 1.3.1 / 4.1.2 field error tied to its field | Record a contribution (member picker) | The member field's error was not tied to the field | `aria-describedby` and `id` on the member error in `ContributionForm.tsx` | Same pattern as the amount field, which `ContributionForm.test.tsx` checks with `toHaveAccessibleDescription` | Me, reading the form while writing its tests |
| 2.4.11 Focus not obscured | Long lists, toasts at the bottom | A sticky header or a toast could cover the focused element | `scroll-padding-top` and `scroll-padding-bottom` in `globals.css` | Tab through the contribution list at phone width | Manual |
| 2.5.7 Dragging | Payout rotation | Nothing drags in this app | None needed | The app has no rotation screen | Manual |
| FILL | FILL | FILL | FILL | FILL | FILL |

## Performance

Measured with Lighthouse mobile (simulated slow 4G, 4x CPU) on `npm run start`, signed in, against the seeded "Load Test Stokvel" (40 members, 12 cycles). Median of 3. The raw reports are in `web-next/.lighthouse/` (git-ignored).

| Page | Metric | Before | After | The change that made the difference |
| --- | --- | --- | --- | --- |
| home | LCP (target under 2.5 s) |  ms |  ms | FILL (the change that made the difference) |
| home | CLS (target under 0.1) |  |  | FILL (the change that made the difference) |
| home | TBT |  ms |  ms | FILL (the change that made the difference) |
| stokvel | LCP (target under 2.5 s) |  ms |  ms | FILL (the change that made the difference) |
| stokvel | CLS (target under 0.1) |  |  | FILL (the change that made the difference) |
| stokvel | TBT |  ms |  ms | FILL (the change that made the difference) |
| home | INP (target under 200 ms) | n/a (needs interactions) | FILL (the "INP:" line in proof-inp-9-2.txt) | measured by `e2e/inp.spec.ts` |

`next/image` is not used anywhere in the app, so nothing needed `sizes`. Proofs of payment never go near the image optimizer: they are served as attachments through `/bff/stokvels/{id}/contributions/{contributionId}/proof` with `Cache-Control: private, no-store`.

## Metadata and indexing

| Route | Public? | Indexed? | What a link preview shows |
| --- | --- | --- | --- |
| `/` (landing), `/login`, `/register` | Yes | Yes (root layout metadata: title, description, Open Graph) | What RondiTrack is |
| `/join?code=...` | Yes (works signed out) | No (`robots` noindex, the code is in the URL) | "You have been invited to a stokvel": generic, no names, no amounts, and not whether the invite is valid |
| `/stokvels`, `/stokvels/[id]`, `/stokvels/[id]/contributions`, `/activity`, `/dashboard/*` | No (session) | No: noindex set once in `src/app/(app)/layout.tsx` | Nothing beyond the product name |
| `/bff/*`, downloads | No | No: `X-Robots-Tag: noindex, nofollow` from `next.config.ts` | Never fetched by a bot with no session |
| `/robots.txt` | Yes | n/a | Allows everything. Deliberately no `Disallow`: a crawler that cannot fetch a page never sees its noindex |

**How the invite page treats a signed-out bot.** The preview endpoint needs a signed-in person and the bot has no session, so the head cannot depend on the invite. The metadata is static and generic. A signed-out request for step 2 or 3 gets a 200 page with a "Sign in to continue" prompt and does not redirect, because a bot that followed a redirect would show the login page's card instead of an invite card. The page never calls the API when signed out, so no stokvel data can reach the HTML. `e2e/metadata.spec.ts` proves this with a `WhatsApp/2` user agent: the title, description and every `og:` tag are checked, and the stokvel name is checked in the whole page. The curl `<head>` is in the PR description.

## CI

- Red run (money parser broken on purpose): 
- Green run after the fix: 

Jobs: `build-and-test` (backend) and `frontend` (npm ci, lint, `next typegen` + tsc, Vitest in UTC and in Africa/Johannesburg) run side by side. `e2e` runs only after both pass: Compose with throwaway secrets generated in the run and a fresh database, Chromium, a production build, Playwright (axe and metadata included). The HTML report is kept as an artifact when the run fails.

## A test that found a bug

The old `money.test.ts` expected `R150` to be refused, which the 9.2 brief reverses, so the test failed in every Vitest run until it was updated. That was a stale test, not an app bug. While writing the form tests I also found a real accessibility bug: the member field's error was not tied to its field (see the first row of the accessibility table, fixed with `aria-describedby`).

## Known gaps

- The 403 test calls the `/bff/` close-preview route. A member never sees the Close Cycle button, so the dialog's "Only a treasurer of this stokvel can close a cycle" message cannot be reached through the UI, and a Server Action cannot be called directly from Playwright without its generated id.
- INP is measured by an `@perf` test that CI skips, because CPU throttling makes it timing-sensitive on shared runners.
- Vitest is 4.1.12 (the brief names 4.1.12).
- The stretch challenges (Lighthouse CI budgets, MSW for "Load more") are not done.