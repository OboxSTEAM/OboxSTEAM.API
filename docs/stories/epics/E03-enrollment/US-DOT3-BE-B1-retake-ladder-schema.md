# US-DOT3-BE-B1 Retake ladder schema (Đợt 3-BE b1)

## Status

implemented

## Lane

high-risk

## Product Contract

Schema for the two-tier retake ladder and program-only pricing:

- `Module` no longer stores retail `Price` or `RetakeFee`.
- `ModuleEnrollment` no longer stores unused `AssignmentFailureCount`.
- `Class` has `Kind` (`Standard` / `Remedial`) and optional `RemedialModuleId`.
- `ClassEnrollment` has `Kind` (`Primary` / `Retake`), default `Primary`.
- `ClassRedeliveryRequest` has `IntensivePaceAcceptedAt` and `ResolutionType`.

Existing classes and enrollments default to `Standard` / `Primary`. Re-delivery checkout amount uses `Program.Price` (minimal compile slice of WS7e). Module DTO price fields remain and return `0` until WS7e removes à la carte checkout.

## Relevant Product Docs

- `docs/product/enrollment.md`

## Acceptance Criteria

- EF migration generated via CLI (not hand-written).
- Solution builds; module/payment/enrollment unit tests that touched dropped columns are updated.
- New columns have string-stored enums (project convention) and nullable FKs where specified.

## Design Notes

- Commands: none (schema only; payment amount source for retake checkout).
- Queries: none new.
- API: module `Price`/`RetakeFee` on responses stay at 0.
- Tables: `Modules`, `ModuleEnrollments`, `Classes`, `ClassEnrollments`, `ClassRedeliveryRequests`.
- Domain rules: Remedial classes may point at one module via `RemedialModuleId`; validators for that land in WS7h.

## Validation

| Layer | Expected proof |
| --- | --- |
| Unit | `dotnet test` filter ModuleService, ModuleEnrollmentService, PaymentService, ValidatorAndUtils |
| Integration | none (no DB in this worktree CI path) |
| E2E | 0 |
| Platform | `dotnet build` |

## Harness Delta

`harness-cli.exe` is not present in this Orca worktree; intake/story CLI rows were not recorded.

## Evidence

- `dotnet build OboxSteam.API/OboxSteam.API.csproj` succeeded (existing warnings only).
- `dotnet ef migrations add DropModulePricingAndAddRetakeLadderSchema` generated `20260824141156_DropModulePricingAndAddRetakeLadderSchema`.
- `dotnet test` filter Module/Payment/Validator/Program/EnrollmentCurriculum/ParentProgression: 180 passed.
- Full `dotnet test OboxSteam.Test`: 1230 passed.
