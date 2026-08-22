# API Conventions

## Base URL and Versioning

- Default listen address: `http://0.0.0.0:5000`.
- Routes are grouped under `/api/`.
- No URL version segment; Swagger document is `v1`.

## Response Envelope

All controller responses use `ApiResult` or `ApiResult<T>` from
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

Failure responses set `isSuccess` to `false` and populate `error` with `code`
and `message`. HTTP status codes align with the error (400, 401, 403, 404,
409, 500).

## JSON Serialization

- Property names: camelCase.
- Enums: serialized as strings (`JsonStringEnumConverter`).
- Reference cycles: ignored (`ReferenceHandler.IgnoreCycles`).

## Date and Time Contract

One product timezone for user wall-clock; storage is always UTC.

| Layer | Rule |
| ----- | ---- |
| Database / server | UTC (`DateTimeKind.Utc`) |
| User wall-clock (UI, schedules, seed) | `Asia/Ho_Chi_Minh` (Windows: `SE Asia Standard Time`) |
| Preferred request wire format | ISO 8601 with offset or `Z` (e.g. `2026-08-22T09:00:00+07:00`) |
| Legacy request format | `dd/MM/yyyy`, `dd/MM/yyyy HH:mm`, `dd/MM/yyyy HH:mm:ss` — interpreted as Vietnam local time, then converted to UTC |

`FlexibleDateTimeConverter` / `AppDateTime.TryParseFlexible` enforce this on inbound JSON. Do not treat naive legacy strings as UTC. Clients should send ISO with timezone; do not compensate by subtracting hours on the client while still sending naive strings.

## Authentication

- JWT Bearer tokens on protected endpoints.
- Refresh tokens stored on the `User` entity.
- `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap` is cleared so role claims
  map directly to `[Authorize(Roles = "...")]`.

Public auth endpoints live under `/api/auth/*` (register, login, OTP, password
reset, refresh). Account management under `/api/account/*` requires auth.

## Error Handling

`GlobalExceptionMiddleware` catches unhandled exceptions and returns JSON
`ApiResult` failures. Business rule violations use `AppException` (4xx) or
`ErrorHelper` helpers in services.

## Upload Limits

- Multipart body and Kestrel max request size: 3 GB.
- Large uploads support media and assignment evidence workflows.

## CORS

- Development: allow any origin.
- Production: exact origins from `CORS_ALLOWED_ORIGINS` /
  `CORS_ALLOWED_ORIGIN` (default includes `https://oboxsteam.website` and
  `http://localhost:3000`), plus the apex `https://oboxsteam.website` and
  one-label portfolio hosts `https://<subdomain>.oboxsteam.website`
  (e.g. `https://ch1mpleo.oboxsteam.website`). Deeper hosts like
  `a.b.oboxsteam.website` are not allowed.

## Documentation

Swagger UI is enabled in Development and Production with Dracula theme and
persisted authorization. Controllers use `SwaggerOperation` and
`ProducesResponseType` attributes.

## Parse-First Rule

Request DTOs are defined in `OboxSteam.Application/DTOs/`. Controllers accept
typed bodies and route parameters; services validate business rules before
domain mutations. See `docs/ARCHITECTURE.md`.
