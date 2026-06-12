# Spec Intake

Date: 2026-06-11

## Source

- User prompt: Fill Harness docs by scanning the existing OboxSTEAM.API codebase.
- Attached file: none.
- External reference: Harness v0 templates in `docs/`.

## Project Summary

OboxSTEAM is a STEAM education platform backend built with .NET 8. It exposes a
REST API for curriculum management (programs, modules, courses, activities),
cohort-based classes, student enrollment, quizzes and assignments, parent
linking, media processing, and face recognition. Clients are browser and mobile
frontends consuming `/api/*`.

## Candidate Product Docs

| File | Purpose | Source sections |
| --- | --- | --- |
| `docs/product/overview.md` | Platform summary and hierarchy | Domain entities, controllers |
| `docs/product/api-conventions.md` | API contract | `ApiResult`, middleware, Program.cs |
| `docs/product/permissions.md` | Authorization | `RoleType`, `[Authorize]` usage |
| `docs/product/curriculum.md` | Learning structure | Program, Module, Course, Activity, Class |
| `docs/product/enrollment.md` | Enrollment flows | Enrollment controllers and entities |
| `docs/product/assessment.md` | Quizzes and assignments | QuizController, Assignment entity |
| `docs/product/integrations.md` | External services | IocContainer, AWS, Resend |

## Candidate Epics

| Epic | Description | Status |
| --- | --- | --- |
| E01-auth-accounts | Registration, JWT, OTP, account management | implemented (weak proof) |
| E02-curriculum | Programs, modules, courses, activities, materials | implemented (weak proof) |
| E03-enrollment | Program, module, class enrollment and progress | implemented (weak proof) |
| E04-assessment | Assignments, question banks, quiz attempts | implemented (weak proof) |
| E05-media-aws | S3 uploads, MediaConvert, Rekognition, webhooks | implemented (weak proof) |
| E06-parent-experts | Parent linking, experts, reviews, highlight videos | implemented (weak proof) |
| E07-test-harness | Automated test project and CI proof ladder | unsliced |

## Architecture Questions

- Runtime stack: .NET 8, ASP.NET Core, EF Core, PostgreSQL — **decided** (see
  `docs/decisions/0006-dotnet-clean-architecture-stack.md`).
- Product surfaces: REST API, Swagger, SignalR hub.
- Storage: PostgreSQL (relational), AWS S3 (blobs).
- External providers: AWS (S3, Rekognition, MediaConvert, Bedrock), Resend email.
- Deployment target: Kestrel on `0.0.0.0:5000` (hosting not defined in repo).
- Security model: JWT Bearer roles, global exception middleware.

## Validation Shape

| Layer | Expected proof |
| --- | --- |
| Unit | Domain rules, validators, quiz grading logic |
| Integration | EF repositories, auth, enrollment gates, webhook handling |
| E2E | Student enrollment → quiz submit; manager curriculum CRUD |
| Platform | Migration startup, S3/Rekognition smoke with env credentials |
| Release | `dotnet test` full suite, `story verify-all` before merge |

Current state: `dotnet build` passes; no test project exists yet.

## Open Decisions

- Payment gateway public API surface (entities exist; endpoints need audit).
- Production hardening for `/api/seed`.
- Redis activation vs removal of dead code.

## First Story Candidates

- US-001: Add `OboxSteam.Tests` with integration test harness.
- US-002: Document and test quiz auto-grading edge cases.
- US-003: Class session and attendance API documentation and proof.

## Harness Delta

- Populated `docs/product/*` from codebase scan.
- Updated `docs/ARCHITECTURE.md`, `docs/TEST_MATRIX.md`, `docs/stories/backlog.md`.
- Added decision `0006-dotnet-clean-architecture-stack.md`.
- Seeded Harness durable layer with epics and build verification command.
