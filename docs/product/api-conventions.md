# API Conventions

## Base URL and Versioning

- Listen address: `http://0.0.0.0:5000` (`UseUrls` in `Program.cs`).
- Routes are grouped under `/api/`; the SignalR hub is `/hubs/notifications`.
- No URL version segment; Swagger document is `v1`.

## Response Envelope

Controller responses use `ApiResult` or `ApiResult<T>` from
`OboxSteam.Application.Utils`:

```json
{
  "isSuccess": true,
  "value": {
    "code": "200",
    "message": "Operation successful.",
    "data": { }
  },
  "error": null
}
```

Failure responses set `isSuccess` to `false`, `value` to `null`, and populate
`error` with `code` and `message`. `error.code` is the HTTP status string
unless the service supplied a machine code (e.g. `ADVISOR_REQUIRED`). Some
409 conflicts also return structured detail in `value.data`.

Responses outside the envelope:

- Model binding and validation failures (including unparseable dates) return
  ASP.NET Core's default `ProblemDetails` 400; no custom
  `InvalidModelStateResponseFactory` is configured.
- `[Authorize]` 401/403 challenges from the JWT middleware have no body.
- Webhooks (`/api/webhooks/aws`, `/api/payments/stripe-webhook`) return plain
  status codes or text.

## JSON Serialization

- Property names: camelCase.
- Enums: serialized as strings (`JsonStringEnumConverter`).
- Reference cycles: ignored (`ReferenceHandler.IgnoreCycles`).
- `DateTime` / `DateTime?`: `FlexibleDateTimeConverter` /
  `FlexibleDateTimeNullableConverter` (see below).
- SignalR payloads also serialize enums as strings.

## Date and Time Contract

One product timezone for user wall-clock; storage is always UTC.

| Layer | Rule |
| ----- | ---- |
| Database / server | UTC (`DateTimeKind.Utc`) |
| User wall-clock (UI, schedules, seed) | `Asia/Ho_Chi_Minh` (Windows: `SE Asia Standard Time`) |
| Preferred request wire format | ISO 8601 with offset or `Z` (e.g. `2026-08-22T09:00:00+07:00`) |
| ISO without offset | Interpreted as Vietnam local time, then converted to UTC |
| Legacy request format | `dd/MM/yyyy`, `dd/MM/yyyy HH:mm`, `dd/MM/yyyy HH:mm:ss` — interpreted as Vietnam local time, then converted to UTC |

`FlexibleDateTimeConverter` / `AppDateTime.TryParseFlexible` enforce this on
inbound JSON; empty strings are rejected. Responses write `DateTime` values in
ISO 8601. Do not treat naive strings as UTC. Clients should send ISO with
timezone; do not compensate by subtracting hours on the client while still
sending naive strings. Swagger shows the legacy `dd/MM/yyyy HH:mm:ss` form as
the `DateTime` example.

## Authentication

- JWT Bearer (HS256) on protected endpoints. Issuer, audience, lifetime, and
  signing key are validated with zero clock skew. Settings: `JWT:SecretKey`,
  `JWT:Issuer`, `JWT:Audience`; token issuance requires a secret of at least
  32 characters.
- Claims (`JwtUtils`): user id (`sub` and name identifier), email, role,
  `jti`, `iat`. `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap` is cleared
  at startup; endpoints authorize with `[Authorize(Roles = "...")]`. Policies `AdminPolicy`,
  `ManagerPolicy`, `MentorPolicy`, `ParentPolicy`, `StudentPolicy` are also
  registered.
- Access tokens last 30 minutes from login and 1 hour from refresh. One
  refresh token per user is stored on `User` (`RefreshToken`,
  `RefreshTokenExpiryTime`, 7 days) and rotated on refresh; logout clears it.
- SignalR clients may pass the token as `?access_token=` on
  `/hubs/notifications`.

Auth endpoints under `/api/auth/*`: `register`, `login`, `send-resetlink`,
`forgot-password`, `refresh-token`, `resend-otp`, `verify-otp` (anonymous) and
`logout` (requires auth). Account endpoints under `/api/account/*` (`me`,
`{userId}`, `me/avatar`) require auth.

## Error Handling

`GlobalExceptionMiddleware` catches unhandled exceptions and writes an
`ApiResult<object>` failure. Services throw typed `AppException` subclasses
through `ErrorHelper`:

| Source | HTTP status |
| --- | --- |
| `ErrorHelper.BadRequest` / `BadRequestException`, `ArgumentException` | 400 |
| `ErrorHelper.Unauthorized` / `UnauthorizedException`, `UnauthorizedAccessException` | 401 |
| `ErrorHelper.Forbidden` / `ForbiddenException` | 403 |
| `ErrorHelper.NotFound` / `NotFoundException`, `KeyNotFoundException` | 404 |
| `ErrorHelper.Conflict` / `ConflictException` (optional payload in `value.data`) | 409 |
| `ErrorHelper.Gone` / `GoneException` | 410 |
| `ErrorHelper.Internal` / `InternalException`, any other exception | 500 |

For 500 responses the message is replaced with "An unexpected error occurred."
Client errors (4xx) are logged as warnings; 5xx are logged as errors and
recorded on the request trace.

## Upload Limits

- Multipart body and Kestrel max request size: 3 GB.
- Kestrel request headers timeout: 10 minutes; keep-alive timeout: 30 minutes.
- Large uploads support media and assignment evidence workflows.

## CORS

Policy `AllowFrontend`:

- Development: allow any origin, header, and method (no credentials).
- Other environments: exact origins from `CORS_ALLOWED_ORIGINS` /
  `CORS_ALLOWED_ORIGIN` (comma-separated; default
  `https://oboxsteam.website,http://localhost:3000`), plus the apex
  `https://oboxsteam.website` and one-label portfolio hosts
  `https://<subdomain>.oboxsteam.website` on the default port
  (`FrontendCorsOriginValidator`). Deeper hosts like `a.b.oboxsteam.website`,
  non-HTTPS, and non-default ports are rejected. Credentials are allowed.

## Documentation

Swagger UI is served at `/` in Development and Production with the Dracula
theme, persisted authorization, a Bearer security scheme, and
`wwwroot/custom-swagger.js` (search and filter). Enums are inlined.
Controllers use `SwaggerOperation` and `ProducesResponseType` attributes.

## Parse-First Rule

Request DTOs are defined in `OboxSteam.Application/DTOs/`. Controllers accept
typed bodies and route parameters; services validate business rules before
domain mutations. See `docs/ARCHITECTURE.md`.
