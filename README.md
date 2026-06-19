# OboxSTEAM API

Backend REST API for **OboxSTEAM** — a Vietnamese STEAM edtech platform that delivers experiential learning programs and automatically builds student digital portfolios for study-abroad applications.

## What this project does

OboxSTEAM combines online learning, live/offline lab sessions, assessments, and AI-powered media processing. Students enroll in programs, join class cohorts, complete activities, submit assignments, and accumulate evidence (certificates, tagged photos, highlight videos) that feed a public portfolio microsite.

This repository is the **API layer only**. The Next.js frontend lives in a separate repository.

## Solution structure

Clean Architecture with four projects:

```
OboxSTEAM.API/
├── OboxSteam.Domain/          # Entities, enums, repository interfaces
├── OboxSteam.Application/     # Services, DTOs, validators, business logic
├── OboxSteam.Infrastructure/  # EF Core, PostgreSQL, AWS, external integrations
└── OboxSteam.API/             # ASP.NET Core controllers, DI, middleware
```

## Domain model at a glance

### Curriculum (what students learn)

```
Program → Module → Course → Activity
```

- **Program** — catalog product with modules, pricing, reviews, expert board
- **Module** — curriculum stage with prerequisites and retake pricing
- **Course** — unit/chapter inside a module (groups theory + activities; not a cohort)
- **Activity** — atomic task (SelfPaced, LiveOnline, Offline)
- **Material**, **Assignment**, **QuestionBank** — attached at module or course level

### Cohort delivery (when / with whom)

```
Program → Class → ClassSession → SessionAttendance
```

- **Class** — running đợt học (mentor, schedule, capacity)
- **ClassEnrollment** — student cohort membership
- **ClassSession** — calendar events linking cohorts to activities or assignments

### Student journey

```
ProgramEnrollment → ModuleEnrollment → ActivityProgress / Submission
                 └→ ClassEnrollment
```

Supporting entities: **StudentProfile**, **Portfolio**, **Certificate**, **Payment**, **MediaAsset**, **HighlightVideo**, **FaceEmbedding**.

For the full domain reference (roles, UI mapping, AI pipeline, entity index), see [`.cursor/rules/context.mdc`](.cursor/rules/context.mdc).

## Tech stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 8 / ASP.NET Core 8 |
| Database | PostgreSQL 15 (EF Core 8) |
| Auth | JWT Bearer |
| File storage | AWS S3 |
| AI / video | AWS Rekognition, MediaConvert, Bedrock |
| Email | Resend |
| Payments | Stripe |
| API docs | Swagger (Swashbuckle) |
| Containers | Docker, docker-compose |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (optional, for PostgreSQL + API container)
- PostgreSQL 15 (if not using Docker)

## Getting started

### 1. Environment variables

Create a `.env` file at the solution root (loaded by `EnvFileLoader` at startup). Minimum variables for local development:

```env
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=oboxsteam;Username=postgres;Password=your_password;Maximum Pool Size=15;Timeout=15;Command Timeout=30
JWT__SecretKey=your-secret-key-at-least-32-chars
JWT__Issuer=OboxSTEAM
JWT__Audience=OboxSTEAM
APP_BASE_URL=http://localhost:3000
CORS_ALLOWED_ORIGINS=http://localhost:3000
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_password
POSTGRES_DB=oboxsteam
POSTGRES_PORT=5432
API_PORT=5000
```

AWS, Resend, and payment keys are required for full feature testing (media upload, email OTP, payments).

For production (`shared-postgres` with `max_connections=20`), append the same pool limits to
`ConnectionStrings__DefaultConnection` in your Infrastructure `.env`:

```env
ConnectionStrings__DefaultConnection=Host=shared-postgres;Port=5432;Database=OboxSteam_db;Username=admin;Password=...;Maximum Pool Size=15;Timeout=15;Command Timeout=30
```

### 2. Run with Docker Compose

```bash
docker compose up -d
```

This starts PostgreSQL and the API. Migrations run on startup via `MigrationExtensions`.

### 3. Run locally (without Docker)

```bash
# Apply migrations and start API
dotnet run --project OboxSteam.API
```

Swagger UI is available at `/swagger` in Development.

### 4. Seed sample data

```http
POST /api/seed
```

Use the seed endpoint in Development to populate programs, modules, courses, activities, and test users.

## Build and test

```bash
dotnet build
dotnet test
```

## API areas

| Route prefix | Purpose |
|--------------|---------|
| `/api/auth` | Login, registration, OTP |
| `/api/account` | User profile management |
| `/api/parent` | Parent–child linking |
| `/api/programs` | Program catalog |
| `/api/modules` | Module CRUD |
| `/api/courses` | Course CRUD |
| `/api/activities` | Activity CRUD |
| `/api/materials` | Material upload |
| `/api/assignments` | Assignment management |
| `/api/question-banks` | Question bank CRUD |
| `/api/quiz` | Quiz attempts and grading |
| `/api/program-enrollments` | Program enrollment |
| `/api/module-enrollments` | Module enrollment and progress |
| `/api/media` | Media upload and tagging |
| `/api/highlight-videos` | Personal highlight video pipeline |
| `/api/experts` | Expert / program board |
| `/api/program-reviews` | Student program reviews |
| `/api/webhooks/aws` | AWS SNS / processing callbacks |

Class delivery (`Class`, `ClassSession`) domain entities exist; dedicated controllers are planned.

## Architecture notes

- **Soft delete** — most entities use `IsDeleted` with EF Core global query filters.
- **Enums as strings** — stored as text in PostgreSQL for readability and safe reordering.
- **Unit of Work** — `IUnitOfWork` aggregates repository access in the Application layer.
- **ApiResult wrapper** — consistent success/error response shape for the frontend.

## Related documentation

- Domain & product context: [`.cursor/rules/context.mdc`](.cursor/rules/context.mdc)
- C# coding standards: [`.cursor/rules/coding-style-csharp.mdc`](.cursor/rules/coding-style-csharp.mdc)

## License

Private — Semester 9 project.
