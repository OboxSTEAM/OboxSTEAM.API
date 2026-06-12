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
- Production: `CORS_ALLOWED_ORIGINS` or `CORS_ALLOWED_ORIGIN` env var; default
  includes `https://oboxsteam.website` and `http://localhost:3000`.

## Documentation

Swagger UI is enabled in Development and Production with Dracula theme and
persisted authorization. Controllers use `SwaggerOperation` and
`ProducesResponseType` attributes.

## Parse-First Rule

Request DTOs are defined in `OboxSteam.Application/DTOs/`. Controllers accept
typed bodies and route parameters; services validate business rules before
domain mutations. See `docs/ARCHITECTURE.md`.
