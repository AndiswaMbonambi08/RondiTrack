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


