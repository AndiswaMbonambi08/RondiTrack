# RondiTrack
RondiTrack is the foundation of a backend API for managing stokvels, rotating savings groups widely used in South Africa. Built with ASP.NET Core 10 Minimal APIs, it models the real-world rules of stokvels directly in code, ensuring invalid states are impossible rather than just checked later. The project focuses on domain modeling, HTTP contracts, and resource-oriented routing, laying the groundwork for a production-ready API.

Minimal APIs were chosen over Controllers because they keep the project lightweight and focused. For an assignment centered on domain modeling and HTTP fundamentals, Minimal APIs reduce boilerplate, make routes explicit, and keep the logic close to the domain rules. This clarity helps demonstrate how the API enforces fairness and accountability in stokvels.

## Setup
Prerequisites

Requires the .NET 10 SDK specifically (`dotnet --list-sdks` should show a 10.x.x entry). Older SDKs will fail with error NETSDK1045.

A terminal or IDE (Visual Studio, Rider, VS Code)

## Steps
Clone the repo: git clone https://github.com/AndiswaMbonambi08/RondiTrack.git

cd RondiTrack

Run the app: dotnet run

Open the Scalar UI: http://localhost:5051/scalar/v1

## Usage
Domain Rules

Inactive users cannot join a stokvel. Prevents non-participating members from claiming payouts unfairly.

A user cannot join the same stokvel twice. Stops duplicate membership that would allow double payout claims.

A stokvel cannot be created without a name or with a contribution amount of 0 or less. Ensures clarity and financial meaning; a nameless or zero-value stokvel undermines trust.

These rules reflect how stokvels operate in reality: fairness, accountability, and trust are non-negotiable.

## Example API Calls

### Create a user
POST /api/users
Content-Type: application/json

{
  "fullName": "Andiswa Mbonambi",
  "email": "andiswa@example.com"
}

### Create a stokvel
POST /api/stokvels
Content-Type: application/json

{
  "name": "Holiday Savings",
  "contributionAmount": 500
}

### Join a stokvel
POST /api/stokvels/{id}/members
Content-Type: application/json

{
  "userId": "<a real user id from GET /api/users>"
}

### Create a contribution cycle
POST /api/contribution-cycles
Content-Type: application/json

{
  "stokvelId": "<a real stokvel id>",
  "label": "2026-09",
  "targetAmount": 500
}

### Record a contribution (idempotent)
POST /api/stokvels/{id}/contributions
Content-Type: application/json
Idempotency-Key: key-1

{
  "userId": "<a real member id>",
  "contributionCycleId": "<a real cycle id from POST /api/contribution-cycles>",
  "amount": 500
}

Sending this exact request again with the same Idempotency-Key returns the same response with no new contribution recorded. Sending it again with the same key but a different amount or contributionCycleId returns 422 Unprocessable Entity.

## Error cases
All errors follow RFC 9457 Problem Details (application/problem+json), produced by a single centralized exception handler:

Inactive user joining:
{
  "title": "Conflict",
  "status": 409,
  "detail": "'Lindiwe Zulu' is inactive and cannot join a stokvel."
}

Duplicate membership:
{
  "title": "Conflict",
  "status": 409,
  "detail": "'Sipho Dlamini' is already a member of this stokvel."
}

Invalid stokvel creation:
{
  "title": "Bad Request",
  "status": 400,
  "detail": "Contribution amount must be greater than zero."
}

Idempotency key reused with different data:
{
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "This idempotency key was already used with a different request."
}

## DTOs and mapping
Every endpoint binds to a request DTO and returns a response DTO. No User or Stokvel entity crosses the HTTP boundary in either direction. Mapping is done by hand with small extension methods (Mapping/), one file per entity. Manual mapping is the right call here regardless of the "no external libraries" constraint: this project moves money, and a reflection-based mapper could silently expose or bind a field nobody intended (like IsActive or the internal member list) the moment a new property is added to an entity. Hand-written mapping fails loudly at compile time instead.

## Service layer
IStokvelService holds the two operations that involve a decision spanning more than one entity: adding a member (checks both the stokvel and the user) and recording a contribution (checks the stokvel, the user, the contribution cycle, membership, duplicate-cycle, and the idempotency key). Everything else, plain CRUD, stays in the endpoint layer, since it's a straightforward lookup with no cross-entity decision to make.

## Idempotency design
The client sends an Idempotency-Key header with each contribution request. The service checks the in-memory idempotency store first.

No record for that key: the contribution is recorded normally, and the key is saved alongside the request and the response.

A record exists and the incoming request matches the stored one: the original response is returned as is, with no new contribution recorded.

A record exists but the incoming request differs: the request is rejected with 422 Unprocessable Entity, since reusing a key for a different payload is a contradiction, not a retry.

## 400 vs 422
This distinction is applied in the contribution-recording endpoint (POST /api/stokvels/{id}/contributions). 400 Bad Request is used for input that's wrong on its own, regardless of state, such as a negative contribution amount, a missing name, or an invalid email. 422 Unprocessable Entity is used for input that's well-formed but conflicts with something outside the payload itself, specifically reusing an idempotency key with a different request body. 409 Conflict is reserved for state conflicts on the resource itself, such as a duplicate member or a duplicate contribution for a cycle that's already been paid.

## Exception hierarchy
RequestValidationException (400), NotFoundException (404), ConflictException (409), and IdempotencyConflictException (422) each carry their own status code and title, so the centralized handler needs no long mapping table. A duplicate contribution and an idempotency-key conflict are classified as two different kinds of failure. A duplicate contribution conflicts with the current state of the stokvel (someone already paid this cycle), while an idempotency-key conflict violates the retry contract itself (the same key was already used for a different request). That distinction is why one is 409 and the other is 422.

## Validation vs exceptions
FluentValidation checks shape only: is a field present, is a Guid non-empty, is an amount positive. It never touches a repository. Anything that requires checking whether another record exists or already happened, such as whether a user is already a member or has already contributed for a cycle, is expressed as a thrown exception from the entity or service layer instead.

## ContributionCycle: no service needed
Creating or updating a ContributionCycle involves no decision spanning more than one entity beyond confirming the parent stokvel exists, so it's handled straight from the endpoint through its repository, protected by validation and NotFoundException. A service method would just be a pass-through with nothing to decide.

## Demonstrated error cases

### 400 Bad Request, malformed request (empty FullName)
Request: POST /api/users
{
  "fullName": "",
  "email": "test@example.com"
}

Response body:
{
  "title": "Bad Request",
  "status": 400,
  "detail": "'Full Name' must not be empty.",
  "correlationId": "0HNOPRRV56DRQ:00000004"
}

Screenshot:
![400 Bad Request response in Scalar](screenshots/400-bad-request.jpeg)

### 404 Not Found, nonexistent user
Request: GET /api/users/00000000-0000-0000-0000-000000000000

Response body:
{
  "title": "Not Found",
  "status": 404,
  "detail": "User not found.",
  "correlationId": "0HNOPRRV56DRS:00000001"
}

Screenshot:
![404 Not Found response in Scalar](screenshots/404-not-found.jpeg)

### 409 Conflict, duplicate stokvel membership
Request: POST /api/stokvels/0fd66196-0142-4d3c-bc40-afda684aa252/members
{
  "userId": "c31e9dff-7a92-4dd9-b469-b88eef15d677"
}

Response body:
{
  "title": "Conflict",
  "status": 409,
  "detail": "'Sipho Dlamini' is already a member of this stokvel.",
  "correlationId": "0HNOPS7L7BLPL:00000008"
}

Screenshot (captured via PowerShell against the live running app, since Scalar's path-parameter field would not commit this particular request; the response is identical either way, same running API, same centralized handler):
![409 Conflict response via PowerShell](screenshots/409-conflict.png)

All three responses come from the same centralized RondiTrackExceptionHandler, producing the identical problem+json shape (title, status, detail, correlationId) regardless of which exception type triggered them.

## Correlation ID walkthrough
Every error response includes a correlationId matching the log entry that recorded it. Example, from the 409 case above.

Response body:
{
  "title": "Conflict",
  "status": 409,
  "detail": "'Sipho Dlamini' is already a member of this stokvel.",
  "correlationId": "0HNOPS7L7BLPL:00000008"
}

Matching log line (from the terminal running dotnet run):
fail: RondiTrack.ErrorHandling.RondiTrackExceptionHandler[0]
      Request failed. CorrelationId: 0HNOPS7L7BLPL:00000008, Method: POST,
      Path: /api/stokvels/0fd66196-0142-4d3c-bc40-afda684aa252/members, StatusCode: 409
      RondiTrack.Domain.Exceptions.ConflictException: 'Sipho Dlamini' is already a member of this stokvel.ed. CorrelationId: 0HNOPS7L7BLPL:00000008, Method: POST,
      Path: /api/stokvels/0fd66196-0142-4d3c-bc40-afda684aa252/members, StatusCode: 409
      RondiTrack.Domain.Exceptions.ConflictException: 'Sipho Dlamini' is already a member of this stokvel.

      ## Definition of Done

| Endpoint | Documented | Validated | Unit-tested | Integration-tested | Status codes reviewed |
|---|---|---|---|---|---|
| GET /api/users | Yes | N/A (no body) | N/A (no rule) | Yes | Yes |
| GET /api/users/{id} | Yes | N/A | N/A | Yes (happy path + 404) | Yes |
| POST /api/users | Yes | Yes | N/A (validation only) | Yes (happy path + 400) | Yes |
| PUT /api/users/{id} | Yes | Yes | N/A | No (see gaps) | Yes |
| DELETE /api/users/{id} | Yes | N/A | N/A | No (see gaps) | Yes |
| GET /api/stokvels | Yes | N/A | N/A | Yes | Yes |
| GET /api/stokvels/{id} | Yes | N/A | N/A | Yes | Yes |
| POST /api/stokvels | Yes | Yes | N/A | Yes (happy path + 400 + boundary) | Yes |
| PUT /api/stokvels/{id} | Yes | Yes | N/A | No (see gaps) | Yes |
| DELETE /api/stokvels/{id} | Yes | N/A | N/A | No (see gaps) | Yes |
| GET /api/stokvels/{id}/members | Yes | N/A | N/A | Yes (happy path + empty-collection edge case) | Yes |
| POST /api/stokvels/{id}/members | Yes | Yes | Yes (inactive user, duplicate member) | Yes (happy path + 404 + 409 x2) | Yes |
| DELETE /api/stokvels/{id}/members/{userId} | Yes | N/A | No (see gaps) | No (see gaps) | Yes |
| POST /api/stokvels/{id}/contributions | Yes | Yes | Yes (duplicate contribution, idempotency comparison) | Yes (happy path + 400 x2 + 404 + 409 + 422 + cross stokvel edge case) | Yes |
| GET /api/contribution-cycles | Yes | N/A | N/A | No (see gaps) | Yes |
| GET /api/contribution-cycles/{id} | Yes | N/A | N/A | No (see gaps) | Yes |
| POST /api/contribution-cycles | Yes | Yes | N/A | Yes (happy path + 400 + 404) | Yes |
| PUT /api/contribution-cycles/{id} | Yes | Yes | N/A | No (see gaps) | Yes |
| DELETE /api/contribution-cycles/{id} | Yes | N/A | N/A | No (see gaps) | Yes |

## Edge cases

1. Empty collection: GET /api/stokvels/{id}/members on a brand new stokvel. Found by asking what an endpoint returns before its typical use case has happened yet. Asserts 200 with an empty array, not an error.
2. Boundary value: contributionAmount set to 0.01. Found by reading each FluentValidation rule and checking the exact boundary it enforces. GreaterThan(0) means 0 itself must fail, already covered by the 400 test, while the smallest representable positive amount must succeed. Asserts 201.
3. Cross resource validity: recording a contribution against a real ContributionCycle that belongs to a different, also real stokvel than the one in the URL. Found by reading StokvelService.RecordContributionAsync and noticing the explicit cycle.StokvelId check. Both the stokvel and the cycle are independently valid, only their combination is wrong. Asserts 404.

## Test run and the rule I deliberately broke

All tests pass:
Test summary: total: 29, failed: 0, succeeded: 29, skipped: 0, duration: 35.9s
Build succeeded with 8 warning(s) in 118.0s

To confirm the suite would actually catch a regression, I commented out the duplicate contribution check in Stokvel.RecordContribution (Domain/Stokvel.cs) and reran the suite.

PS C:\Users\Andiswa Mbonambi\RondiTrack> dotnet test
Restore succeeded with 2 warning(s) in 8.9s
    C:\Users\Andiswa Mbonambi\RondiTrack\RondiTrack.Tests\RondiTrack.Tests.csproj : warning NU1900: Error occurred while getting package vulnerability data: The download of 'https://api.nuget.org/v3-vulnerabilities/2026.09.26.05.43.06/vulnerability.base.json' timed out because no data was received for 60000ms.
    C:\Users\Andiswa Mbonambi\RondiTrack\RondiTrack.csproj : warning NU1900: Error occurred while getting package vulnerability data: The download of 'https://api.nuget.org/v3-vulnerabilities/2026.09.26.05.43.06/vulnerability.base.json' timed out because no data was received for 60000ms.
  RondiTrack net10.0 failed with 1 error(s) and 5 warning(s) (24.9s)
    C:\Users\Andiswa Mbonambi\RondiTrack\RondiTrack.csproj : warning NU1900: Error occurred while getting package vulnerability data: The download of 'https://api.nuget.org/v3-vulnerabilities/2026.09.26.05.43.06/vulnerability.base.json' timed out because no data was received for 60000ms.
    C:\Users\Andiswa Mbonambi\RondiTrack\Domain\ContributionCycle.cs(14,12): warning CS8618: Non-nullable property 'Label' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
    C:\Users\Andiswa Mbonambi\RondiTrack\Domain\User.cs(12,12): warning CS8618: Non-nullable property 'FullName' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
    C:\Users\Andiswa Mbonambi\RondiTrack\Domain\User.cs(12,12): warning CS8618: Non-nullable property 'Email' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
    C:\Users\Andiswa Mbonambi\RondiTrack\Domain\Stokvel.cs(18,12): warning CS8618: Non-nullable property 'Name' must contain a non-null valuewhen exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
    C:\Users\Andiswa Mbonambi\RondiTrack\Domain\Stokvel.cs(54,25): error CS0161: 'Stokvel.RecordContribution(User, Guid, decimal)': not all code paths return a value
  RondiTrack.Tests net10.0 failed with 1 warning(s) (7.9s)
    C:\Users\Andiswa Mbonambi\RondiTrack\RondiTrack.Tests\RondiTrack.Tests.csproj : warning NU1900: Error occurred while getting package vulnerability data: The download of 'https://api.nuget.org/v3-vulnerabilities/2026.09.26.05.43.06/vulnerability.base.json' timed out because no data was received for 60000ms.

Build failed with 1 error(s) and 8 warning(s) in 50.7s
PS C:\Users\Andiswa Mbonambi\RondiTrack> git checkout Domain/Stokvel.cs

The check was restored immediately afterward, and the suite returned to all green.

## Known gaps

PUT and DELETE across all three resources, and the plain GET-all and GET-by-id for contribution cycles, are documented and status code reviewed but not integration tested. They follow the exact same fetch, mutate, respond pattern already proven by the corresponding tests on Users and Stokvels, so testing them again would exercise plumbing rather than new behavior.

DELETE /api/stokvels/{id}/members/{userId}'s not-a-member rule is the one gap I would close first given more time, since it is a real business rule rather than routine CRUD, and it is the only untested case in that category.

Three behavioral gaps are also documented directly in the OpenAPI descriptions: deleting a User does not remove their memberships, deleting a Stokvel does not delete its ContributionCycles, and deleting a ContributionCycle does not check for contributions already recorded against it.

## Secret management
The PostgreSQL connection string is stored via .NET User Secrets, never in
appsettings.json or any tracked file. A teammate cloning this repo runs:

dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:RondiTrack" "Host=localhost;Port=5432;Database=ronditrack;Username=ronditrack_user;Password=<their-own-password>"

then their own local secrets.json (outside the repo) holds it. Program.cs reads
it through normal configuration, which merges User Secrets automatically in
the Development environment.

## Migration review
Ran `dotnet ef migrations add InitialCreate`, then read the generated file
before applying it. Checked specifically that: each of the six tables was
created (Users, Stokvels, StokvelMembers, ContributionCycles, Contributions,
Payouts), StokvelMembers has a composite key on (StokvelId, UserId) rather
than its own surrogate id, and no column was silently dropped or renamed
compared to what the entity classes declare — a rename can look identical to
a drop-and-add in the generated migration, so I compared property names in
each entity file against the CreateTable columns line by line rather than
trusting the diff visually.

## Retry configuration
EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10)).
5 retries capped at 10 seconds each caps total wait around 50 seconds, short
enough that a caller isn't left hanging indefinitely, long enough to ride out
a brief network blip or a connection pool being temporarily exhausted during
a deploy. This retries transient failures — a dropped connection, a timeout
reaching the database. It deliberately does not retry something like a
constraint violation or a business-rule conflict, since retrying an inherently
wrong request would just fail again identically; that needs to surface
immediately as a 409, not be silently retried.

## Definition of Done (extended for Assignment 5.1)

| Endpoint | Documented | Validated | Unit-tested | Integration-tested | Status codes reviewed | Persisted via EF Core | Explicit transaction tested |
|---|---|---|---|---|---|---|---|
| GET /api/users | Yes | N/A (no body) | N/A (no rule) | Yes | Yes | No | N/A |
| GET /api/users/{id} | Yes | N/A | N/A | Yes (happy path + 404) | Yes | No | N/A |
| POST /api/users | Yes | Yes | N/A (validation only) | Yes (happy path + 400) | Yes | No | N/A |
| PUT /api/users/{id} | Yes | Yes | N/A | No (see gaps) | Yes | No | N/A |
| DELETE /api/users/{id} | Yes | N/A | N/A | No (see gaps) | Yes | No | N/A |
| GET /api/stokvels | Yes | N/A | N/A | Yes | Yes | Yes | N/A |
| GET /api/stokvels/{id} | Yes | N/A | N/A | Yes | Yes | Yes | N/A |
| POST /api/stokvels | Yes | Yes | N/A | Yes (happy path + 400 + boundary) | Yes | Yes | N/A |
| PUT /api/stokvels/{id} | Yes | Yes | N/A | No (see gaps) | Yes | Yes | N/A |
| DELETE /api/stokvels/{id} | Yes | N/A | N/A | No (see gaps) | Yes | Yes | N/A |
| GET /api/stokvels/{id}/members | Yes | N/A | N/A | Yes (happy path + empty-collection edge case) | Yes | Yes | N/A |
| POST /api/stokvels/{id}/members | Yes | Yes | Yes (inactive user, duplicate member) | Yes (happy path + 404 + 409 x2) | Yes | Yes | N/A |
| DELETE /api/stokvels/{id}/members/{userId} | Yes | N/A | No (see gaps) | No (see gaps) | Yes | Yes | N/A |
| POST /api/stokvels/{id}/contributions | Yes | Yes | Yes (duplicate contribution, idempotency comparison) | Yes (happy path + 400 x2 + 404 + 409 + 422 + cross stokvel edge case) | Yes | No (see gaps) | N/A |
| GET /api/contribution-cycles | Yes | N/A | N/A | No (see gaps) | Yes | Yes | N/A |
| GET /api/contribution-cycles/{id} | Yes | N/A | N/A | No (see gaps) | Yes | Yes | N/A |
| POST /api/contribution-cycles | Yes | Yes | N/A | Yes (happy path + 400 + 404) | Yes | Yes | N/A |
| PUT /api/contribution-cycles/{id} | Yes | Yes | N/A | No (see gaps) | Yes | Yes | N/A |
| DELETE /api/contribution-cycles/{id} | Yes | N/A | N/A | No (see gaps) | Yes | Yes | N/A |
| POST /api/stokvels/{stokvelId}/cycles/{cycleId}/payout | Yes | N/A (no user-supplied body) | No (see gaps) | Yes (rollback test) | Yes | Yes | Yes |

## Note on Contribution persistence

Contribution itself is not yet backed by a dedicated EF table today. It's created
and read as part of Stokvel's in-memory contribution list, the same as before
this assignment. Persisting it properly means either its own DbSet and
repository, or folding it into the same reconciliation approach used for
StokvelMember in SaveChangesMiddleware. Both are reasonable, but doing either
well needs more room than today's scope allows without the payout
transaction work suffering for it. This is a stated gap, not an oversight:
Contribution stays in-memory for now, and Users stays in-memory as a
deliberate choice since it carries no cross-entity decision worth protecting
with a database swap yet.