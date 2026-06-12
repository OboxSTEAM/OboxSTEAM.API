# 0006 .NET Clean Architecture Stack

Date: 2026-06-11

## Status

Accepted

## Context

OboxSTEAM.API is an existing backend with four projects, PostgreSQL persistence,
JWT authentication, and multiple AWS integrations. Harness docs were empty and
agents had no durable record of the chosen stack or layering rules.

## Decision

Adopt and document the current four-layer layout as the stable architecture:

| Project | Responsibility |
| --- | --- |
| `OboxSteam.Domain` | Entities, enums, repository interfaces |
| `OboxSteam.Application` | DTOs, service interfaces, application services, validators |
| `OboxSteam.Infrastructure` | EF Core, AWS/email implementations, `GenericRepository` |
| `OboxSteam.API` | Controllers, middleware, DI composition, startup |

Additional rules inherited from project conventions:

- Manual DTO mapping (no AutoMapper).
- `IGenericRepository` / `GenericRepository` for data access.
- `ErrorHelper` and `AppException` for business errors.
- Controllers return `ApiResult<T>`; action methods use `async Task<IActionResult>`
  without `Async` suffix.
- EF Core migrations run at application startup.

## Alternatives Considered

1. Minimal API single-project layout — rejected; would discard existing
   structure and blur boundaries.
2. CQRS with MediatR — not present; would add ceremony without current need.
3. Monorepo with frontend — out of scope; API is standalone.

## Consequences

Positive:

- Clear dependency direction: API → Application → Domain ← Infrastructure.
- Agents can locate behavior by layer without rescans.

Tradeoffs:

- No automated architecture tests enforce layer boundaries yet.
- Some authorization logic spans controllers and services.

## Follow-Up

- Add integration test project referencing API and Infrastructure.
- Consider architecture test or analyzer rules if cross-layer leaks appear.
