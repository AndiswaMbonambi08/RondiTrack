# RondiTrack
RondiTrack is the foundation of a backend API for managing stokvels rotating savings groups widely used in South Africa. Built with ASP.NET Core 10 Minimal APIs, it models the real-world rules of stokvels directly in code, ensuring invalid states are impossible rather than just checked later. The project focuses on domain modeling, HTTP contracts, and resource-oriented routing, laying the groundwork for a production-ready API.
Minimal APIs were chosen over Controllers because they keep the project lightweight and focused. For an assignment centered on domain modeling and HTTP fundamentals, Minimal APIs reduce boilerplate, make routes explicit, and keep the logic close to the domain rules. This clarity helps demonstrate how the API enforces fairness and accountability in stokvels.

## Setup
Prerequisites

.NET 10 SDK installed and on your PATH

A terminal or IDE (Visual Studio, Rider, VS Code)

## Steps
Clone the repo: git clone https://github.com/AndiswaMbonambi08/RondiTrack.git

cd RondiTrack
                
Run the app:    dotnet run

Open the Scalar UI: http://localhost:5051/scalar/v1

## Usage
Domain Rules
Inactive users cannot join a stokvel  
→ Prevents non-participating members from claiming payouts unfairly.

A user cannot join the same stokvel twice  
→ Stops duplicate membership that would allow double payout claims.

A stokvel cannot be created without a name or with a contribution ≤ 0  
→ Ensures clarity and financial meaning; a nameless or zero-value stokvel undermines trust.

These rules reflect how stokvels operate in reality: fairness, accountability, and trust are non-negotiable.

## Example API Calls
## Create a user
POST /users
Content-Type: application/json

{
  "name": "Andiswa",
  "isActive": true
}

## Create a stokvel
POST /stokvels
Content-Type: application/json

{
  "name": "Holiday Savings",
  "contributionAmount": 500
}
## Join a stokvel
POST /stokvels/{id}/members
Content-Type: application/json

{
  "userId": "123"
}
## Error cases
Inactive user joining:
{ "error": "Inactive users cannot join a stokvel." }

Duplicate membership:
{ "error": "User already belongs to this stokvel." }

Invalid stokvel creation:
{ "error": "Stokvel must have a name and contribution > 0." }

## DTOs and mapping (Assignment 4.2)
Every endpoint now binds to a request DTO and returns a response DTO — no `User` or
`Stokvel` entity crosses the HTTP boundary in either direction. Mapping is done by hand
with small extension methods (`Mapping/`), one file per entity. Manual mapping is the
right call here regardless of the "no external libraries" constraint: this project moves
money, and a reflection-based mapper could silently expose or bind a field nobody
intended (like `IsActive` or the internal member list) the moment a new property is
added to an entity. Hand-written mapping fails loudly at compile time instead.

## Service layer
`IStokvelService` holds the two operations that involve a decision spanning more than
one entity: adding a member (checks both the stokvel and the user) and recording a
contribution (checks the stokvel, the user, membership, duplicate-cycle, and the
idempotency key). Everything else — plain CRUD — stays in the endpoint layer, since it's
a straightforward lookup with no cross-entity decision to make.

## Idempotency design
The client sends an `Idempotency-Key` header with each contribution request. The service
checks the in-memory idempotency store first:
- No record for that key → the contribution is recorded normally, and the key is saved
  alongside the request and the response.
- A record exists and the incoming request matches the stored one → the original response
  is returned as-is, with no new contribution recorded.
- A record exists but the incoming request differs → the request is rejected with
  422 Unprocessable Entity, since reusing a key for a different payload is a contradiction,
  not a retry.

## 400 vs 422
400 Bad Request is used for input that's wrong on its own, regardless of state — a
negative contribution amount, a missing name, an invalid email. 422 Unprocessable Entity
is used for input that's well-formed but conflicts with something outside the payload
itself — specifically, reusing an idempotency key with a different request body. 409
Conflict is reserved for state conflicts on the resource itself — a duplicate member, or
a duplicate contribution for a cycle that's already been paid.
