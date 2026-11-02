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