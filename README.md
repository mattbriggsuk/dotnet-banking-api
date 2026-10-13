# Banking API

ASP.NET Core 8 implementation of the Java banking API.
It preserves the routes, JSON response shapes, status codes, account types,
PIN login, and 30-minute opaque bearer sessions.

## Run and test

Requires the .NET 8 SDK or a newer SDK supporting `net8.0`.

```powershell
dotnet build Banking.sln
dotnet test Banking.sln
dotnet run --project Api/Banking.Api.csproj --launch-profile http
```

The HTTP profile uses `http://localhost:8080`, matching Java. The HTTPS profile
also provides `https://localhost:7260`. In Development, HTTP requests stay on
HTTP in either profile so the React application's Vite proxy can reach the API
at port 8080. HTTPS redirection remains enabled outside Development. Neither
profile opens a browser. Use [Api/BankingService.http](Api/BankingService.http) to run
the complete registration, login, account, money movement, and logout sequence.
Its named response references work in HTTP clients supporting that syntax.
Otherwise, copy returned numbers, PINs, and tokens into subsequent requests.

The React app in `Z:\GIT\react-banking-app` proxies `/api` to this HTTP address
with `API_PROXY_TARGET=http://localhost:8080` and an empty `VITE_API_BASE_URL`.
Start the API, run `npm run dev` in that app, and open the URL Vite prints. No
CORS policy is needed for this setup because requests use the frontend origin.

The application uses only the ASP.NET Core shared framework; test packages are
the only NuGet dependencies. Tests include service checks and HTTP integration
checks against Kestrel on an automatically assigned loopback port.

## Layers

| Project | Responsibility |
| --- | --- |
| Api | Controllers, HTTP request/response contracts, model validation, authentication handler, dependency registration, and Problem Details. |
| Business | Registration, PIN login, session expiry/revocation, ownership, account lifecycle, and balance rules. |
| Data | Thread-safe in-memory customer, credential, account, and session repositories. |
| Domain | Framework-independent models, enums, exceptions, and service/repository interfaces. |
| Utils | Cryptographic PIN generation/hashing, Luhn numbers, and shared validation. |

Dependencies point toward Domain: Business depends on Domain and Utils; Data
depends on Domain; Utils depends on Domain. Api composes the implementations
through constructor injection. Controllers use service interfaces and never
access repositories. HTTP contracts live in Api so Domain does not depend on
ASP.NET Core, JSON serialization, or HTTP validation attributes.

Accounts and customers are immutable snapshots. The singleton account service
coordinates account mutations with one lock; the account repository stores both
transfer snapshots under its own lock. This prevents lost updates and avoids
exposing balances changed through shared object references. Keep the account
service singleton with these repositories. A database implementation would need
transactions and concurrency controls spanning validation and persistence.

Session storage lives in Data behind `ISessionRepository`; Business decides when
sessions expire. `TimeProvider` makes expiry and transaction timestamps testable.

## Authentication

Protected controllers use Microsoft's `Microsoft.AspNetCore.Authorization.AuthorizeAttribute`
(`[Authorize]` in C#). Registration and login use `[AllowAnonymous]`.
The fallback authorization policy also requires authentication for endpoints
that have no explicit authorization metadata.

`BearerSessionHandler` is an ASP.NET Core `AuthenticationHandler` registered with
`AddAuthentication().AddScheme()`. It parses the Authorization header, asks the
business service to validate the opaque token, and publishes a claims principal
plus the trusted domain session. Controllers receive the session through
`ICurrentSession`; business services enforce ownership. No credentials or
customer identities are trusted from request headers beyond the validated token.

This follows Microsoft's [authentication scheme documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-8.0)
and [Authorize attribute documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/simple?view=aspnetcore-8.0).
Tokens are opaque server-side sessions, matching Java. They are not JWTs or
Microsoft Entra ID tokens.

Registration returns a generated six-digit PIN as a string, the customer, and a
session token. The PIN is returned only on registration; stored credentials are
salted PBKDF2-HMAC-SHA256 hashes using 600,000 iterations. PIN verification uses
a constant-time comparison and a dummy hash for unknown customers. Emails are
trimmed and normalized to lowercase, with case-insensitive uniqueness.

```http
POST /api/access/login
Content-Type: application/json

{"identifier":"taylor.reed@example.com","pin":"012345"}
```

Login also accepts the customer number. Send the returned session token on each
protected request:

```http
Authorization: Bearer opaque-token
```

Missing, malformed, unknown, expired, and revoked tokens return `401` with a
`WWW-Authenticate: Bearer` header. A supplied invalid token is rejected on public
routes too, matching the Java filter. Logout revokes only the current token.

## Endpoints

| Method | Route | Success | Access |
| --- | --- | --- | --- |
| POST | `/api/customer/register` | 201 | Public |
| POST | `/api/access/login` | 200 | Public |
| POST | `/api/access/logout` | 204 | Authenticated |
| GET | `/api/customer/get?customerNumber=...` | 200 | Own customer |
| GET | `/api/customer/accounts?customerNumber=...` | 200 | Own customer |
| POST | `/api/account/create` | 201 | Signed-in customer |
| POST | `/api/account/close` | 202 | Own account |
| POST | `/api/account/deposit` | 200 | Own target account |
| POST | `/api/account/withdraw` | 200 | Own target account |
| POST | `/api/account/transfer` | 200 | Own source account |

Account creation accepts `CURRENT`, `SAVINGS`, or `CREDIT`; omitting `accountType`
defaults to `CURRENT`. Null, undefined, and numeric enum values are rejected.
All types share the same balance rules, as in Java. The authenticated session
determines the account owner.

Deposits and withdrawals accept a `transaction` wrapper with `transactionType`,
`amount`, and `targetAccountNumber`. Transfers additionally require
`sourceAccountNumber` and can credit another customer's account. Source and
target must differ. All involved accounts must be open, withdrawals and transfers
need sufficient funds, and an account must have zero balance before closing.
Closed accounts remain in the customer's account list.

Customer and account numbers use a 16-digit Luhn checksum. Monetary amounts use
`decimal`, must be positive, and may have at most two decimal places and 16
integer digits. The server assigns UTC transaction timestamps, ignoring client
timestamps. Unlike Java's arbitrary-precision `BigDecimal`, C# `decimal` has a
finite balance range; balance updates that overflow or lose precision return
`409` without writing.

## Errors and storage

Errors use `application/problem+json`: malformed requests and invalid business
arguments return `400`, authentication failures `401`, ownership failures `403`,
missing records `404`, and duplicate emails or conflicting account states `409`.
ASP.NET Core supplies validation errors; the global exception handler maps
domain exceptions. Unexpected failures return a generic `500` without internal
exception details. Every response includes `Cache-Control: no-store`.

All storage is in memory and disappears when the API stops, matching the Java
learning project. This implementation has no durable database, ledger,
idempotency, rate limiting, audit trail, recovery, or multi-instance coordination.
