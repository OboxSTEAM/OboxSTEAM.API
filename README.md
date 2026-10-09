# OboxSTEAM API

Backend REST API for **OboxSTEAM**, a Vietnamese STEAM education platform. It
runs experiential learning programs end to end: curriculum design with expert
review, cohort classes with live and offline sessions, assessments, payments,
and a student portfolio built from certificates, skills, and AI-processed media.

This repository is the **.NET backend only**. The Next.js frontend lives in a
separate repository.

## Roles

| Role | What they do |
| --- | --- |
| Admin | Platform administration |
| Manager | Programs, classes, question banks, assignments, mentor assignment approval |
| Expert | Program frameworks and rubrics, advisory chat, curriculum approval, offline co-teaching |
| Mentor | Teaches courses and classes, grades work, manages skill profile |
| Student | Enrolls and pays, attends sessions, completes activities and assessments, owns a portfolio |
| Parent | Linked to students; sees progress, approves and pays |

Roles are enforced with JWT role claims. Details: [`docs/product/permissions.md`](docs/product/permissions.md).

## Features

**Curriculum and expert review**
- `Program -> Module (Theory | Experiential | Research) -> Course -> Activity (SelfPaced | LiveOnline | Offline)`, with materials, assignments, and research milestones.
- Expert program frameworks (blueprints and rubric criteria), advisory discussion with attachments and mentions, curriculum versioning with a change log, and approval before publishing.
- Skill catalog linked to programs; student and mentor skill snapshots backed by evidence.

**Classes and sessions**
- Classes (cohorts) with mentors, capacity, schedules, and automatic start of open classes.
- Class sessions for live online (embedded 8x8 JaaS meetings) and offline work, with QR/code check-in, attendance, session evidence, and expert co-teach invitations.
- Per-class assignment windows, session reminders, and automatic session start/completion.
- Mentors request to teach a class and managers approve or reject.
- Class continuity (redelivery): a student picks another eligible class and pays 50% of the program price.

**Enrollment and payments**
- Program and module enrollment with prerequisites, seat holds during checkout, and Stripe payments.
- Fail and drop close a purchase; continuing is a windowed rebuy into another eligible cohort.
- An invoice per payment (also emailed); parent payment requests with checkout links.

**Assessment**
- Bank-drawn and direct quizzes (with per-class question sets), file-upload assignments, retrospectives, and research milestone submissions.
- Attempt limits by module type; assessment recovery requests let mentors grant extra attempts.

**Portfolio, certificates, and media**
- Automatic PDF certificates when a program is completed.
- Student portfolios with sections and galleries, published on per-student subdomains.
- Media upload with AWS Rekognition face tagging; class highlight videos and personal highlight reels (AWS MediaConvert), optionally filtered to a student's strength using AWS Bedrock.

**Notifications and dashboards**
- Inbox notifications with real-time delivery over SignalR (`/hubs/notifications`) and email for priority events.
- Role dashboards and a personal schedule.

Full product contract: [`docs/product/overview.md`](docs/product/overview.md).

## Tech stack

| Concern | Technology |
| --- | --- |
| Runtime | .NET 8, ASP.NET Core 8 |
| Database | PostgreSQL (EF Core 8 + Npgsql) |
| Auth | JWT Bearer with refresh tokens |
| Real-time | SignalR |
| Storage and media | AWS S3, Rekognition, MediaConvert (SNS webhooks) |
| AI | AWS Bedrock (Mantle, OpenAI-compatible client) |
| Payments | Stripe |
| Email | Resend (Vietnamese templates) |
| Meetings | 8x8 JaaS (Jitsi as a Service) |
| PDF | QuestPDF (certificates) |
| Observability | OpenTelemetry exported to Traceway Cloud |
| API docs | Swagger (Swashbuckle) |
| Tests | xUnit, Moq, EF Core InMemory, Coverlet |
| Delivery | Docker, Docker Hub, GitHub Actions |

## Solution structure

Clean architecture: inner layers never depend on outer ones.

```text
OboxSTEAM.API/
├── OboxSteam.Domain/          # Entities, enums, repository interfaces
├── OboxSteam.Application/     # Services, DTOs, validators, notifications, business rules
├── OboxSteam.Infrastructure/  # EF Core, migrations, repositories, AWS/Stripe/email, background jobs
├── OboxSteam.API/             # Controllers, SignalR hub, DI, middleware, observability
├── OboxSteam.Test/            # Unit tests
├── docs/                      # Product contract, architecture, decisions, plans
└── scripts/                   # Coverage and tooling scripts
```

More detail: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

### Background jobs

| Job | Interval | Purpose |
| --- | --- | --- |
| `ClassSeatHoldCleanupService` | 1 min | Release expired checkout seat holds |
| `SessionReminderService` | 5 min | Send "session starting soon" notifications |
| `SessionLifecycleService` | 5 min | Start due sessions, complete elapsed ones |
| `AssignmentWindowCloseService` | 5 min | Grade timed-out quiz attempts, close elapsed assignment windows |
| `OpenClassAutoStartService` | Adaptive (up to 30 min) | Move eligible open classes to in progress |
| `PendingEnrollmentCleanupService` | 1 h | Remove unpaid enrollments older than one day |
| `DiscussionAttachmentPurgeService` | 1 h | Purge unsent advisory discussion attachments |
| `PersonalVideoGenerationWorker` | Queue | Build personal highlight reels |

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for PostgreSQL, or to run the whole stack)

### 1. Environment variables

Copy `.env.example` to `.env` at the solution root and fill in values. The API
loads it at startup. `.env` is gitignored; never commit it.

- **Required to boot:** connection string, `JWT__*`, `APP_BASE_URL`, `CORS_ALLOWED_ORIGINS`, `POSTGRES_*`, `API_PORT`.
- **Needed for full features:** AWS (S3, Rekognition, MediaConvert, SNS), Bedrock, Stripe, Resend, and JaaS keys.
- **Telemetry:** `OTEL_EXPORTER_OTLP_ENDPOINT` and `TRACEWAY_BACKEND_TOKEN` are production only. Leave them unset locally and nothing is exported.

The example connection string uses the Docker host `oboxsteam.database`. When
running the API with `dotnet run` against the Docker database, use
`Host=localhost;Port=5433` (the `POSTGRES_PORT` mapping) instead.

### 2. Run with Docker Compose

```bash
docker compose up -d
```

Starts PostgreSQL and the API. EF Core migrations are applied on startup.

### 3. Run locally

```bash
docker compose up -d oboxsteam.database
dotnet run --project OboxSteam.API
```

Swagger UI is served at the root URL (`http://localhost:5000/`).

### 4. Seed sample data

```http
POST /api/seed/all
```

Populates programs, classes, curriculum, and test users for development.
`DELETE /api/seed/clear` wipes all data, so never call it against production.

## Build and test

```bash
dotnet build OboxSteam.API/OboxSteam.API.csproj
dotnet test OboxSteam.Test/OboxSteam.Test.csproj
```

Unit tests with Application-layer coverage:

```powershell
.\scripts\run-coverage.ps1
.\scripts\run-coverage.ps1 -Filter "FullyQualifiedName~ClassServiceTests"
```

### Database migrations

Generate migrations with the EF CLI. Never hand-write migration files or edit
the model snapshot.

```bash
dotnet tool restore
dotnet ef migrations add <MigrationName> --project OboxSteam.Infrastructure/OboxSteam.Infrastructure.csproj --startup-project OboxSteam.API/OboxSteam.API.csproj
```

## API areas

All routes live under `/api`. Every response uses the `ApiResult` envelope
([`docs/product/api-conventions.md`](docs/product/api-conventions.md)).

| Area | Route prefixes |
| --- | --- |
| Auth and account | `/auth`, `/account`, `/me`, `/parent` |
| Curriculum | `/programs`, `/programs/{id}/curriculum/changes`, `/program-frameworks`, `/modules`, `/courses`, `/activities`, `/materials`, `/skills` |
| People | `/experts`, `/mentors` |
| Classes | `/classes`, `/classes/{classId}/sessions`, `.../attendance`, `/class-sessions` (check-in, meetings, evidence), `/class-session-experts`, `/class-enrollments`, `/class-mentor-requests`, `/class-redelivery-requests`, `/schedules` |
| Enrollment and payments | `/program-enrollments`, `/module-enrollments`, `/activity-progresses`, `/payments`, `/invoices` |
| Assessment | `/assignments`, `/assignments/{id}/classes/{classId}/quiz-set`, `/question-banks`, quiz, retrospective, submission, and research milestone routes, `/assessment-recovery-requests` |
| Portfolio and media | `/portfolios`, `/certificates`, `/media`, `/highlight-video`, `/programs/{id}/reviews` |
| Platform | `/notifications`, `/dashboard`, `/webhooks/aws`, `/seed` |

## Conventions

- **Responses:** `ApiResult` / `ApiResult<T>`. Business errors go through `ErrorHelper` and `GlobalExceptionMiddleware`.
- **JSON:** camelCase, enums as strings. Dates are stored in UTC; user wall-clock time is `Asia/Ho_Chi_Minh`.
- **Persistence:** `IUnitOfWork` + `GenericRepository`, soft delete through global query filters, enums stored as text.
- **Code style:** manual DTO mapping, no `Async` suffix on controller actions. See [`.cursor/rules/coding-style-csharp.mdc`](.cursor/rules/coding-style-csharp.mdc).
- **Uploads:** up to 3 GB per request.

## Deployment and monitoring

- **Deploy:** every push to `main` runs `.github/workflows/deploy.yml`. It builds the image from `OboxSteam.API/Dockerfile`, pushes it to Docker Hub, then connects to the VPS over SSH to pull it and recreate the `obox-backend` container. The VPS keeps its own `docker-compose.yml` and `.env`, and the workflow only updates the image tag. New environment variables have to be added on the VPS by hand.
- **Monitoring:** production exports traces, logs, runtime metrics, background-job runs, and Bedrock calls to Traceway Cloud through OpenTelemetry. Exported logs have emails masked, and idle background-job runs are not exported. A Traceway agent on the VPS reports host metrics. Details: the Observability Contract in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Documentation

| Topic | Location |
| --- | --- |
| Product overview and capability map | [`docs/product/overview.md`](docs/product/overview.md) |
| Curriculum, enrollment, assessment, notifications | [`docs/product/`](docs/product/) |
| Architecture and layering | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| Architecture decisions | [`docs/decisions/`](docs/decisions/) |
| Test coverage by epic | [`docs/TEST_MATRIX.md`](docs/TEST_MATRIX.md) |
| Contributor and agent workflow | [`docs/WORKFLOW.md`](docs/WORKFLOW.md), [`AGENTS.md`](AGENTS.md) |

## License

Private — Semester 9 project.
