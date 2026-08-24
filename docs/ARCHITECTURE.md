# Architecture

OboxSTEAM.API is a .NET 8 ASP.NET Core backend using clean architecture with
PostgreSQL and AWS integrations.

## Stack

| Concern | Choice |
| --- | --- |
| Runtime | .NET 8 |
| Web framework | ASP.NET Core (Kestrel) |
| ORM | Entity Framework Core + Npgsql |
| Database | PostgreSQL |
| Auth | JWT Bearer + refresh tokens |
| Object storage | AWS S3 |
| Face recognition | AWS Rekognition |
| Video transcoding | AWS MediaConvert + SNS webhooks |
| AI runtime | AWS Bedrock (registered) |
| Email | Resend |
| Real-time | SignalR (hub configured) |
| API docs | Swagger / Swashbuckle |

Record: `docs/decisions/0006-dotnet-clean-architecture-stack.md`.

## Solution Layout

```text
OboxSteam.Domain/
  Entities/          # 40+ domain entities
  Enums/             # RoleType, ActivityType, AssignmentType, ...
  Interfaces/        # IUnitOfWork, IGenericRepository

OboxSteam.Application/
  DTOs/              # Request/response shapes per feature area
  Interfaces/        # IAuthService, IProgramService, ...
  Services/          # Application logic
  Validation/        # Fluent validators
  Utils/             # ApiResult, ErrorHelper, HashHelper

OboxSteam.Infrastructure/
  Persistence/       # OboxSteamDbContext, migrations, factory
  Repositories/      # GenericRepository, UnitOfWork
  Services/          # AWS, email, CSV parser implementations

OboxSteam.API/
  Controllers/       # 19 REST controllers
  Architecture/      # IocContainer, migrations, env loading
  Middlewares/       # GlobalExceptionMiddleware
```

Build entrypoint:

```bash
dotnet build OboxSteam.API/OboxSteam.API.csproj
```

## Layering

```text
domain
  <- application
      <- infrastructure
          <- interface (OboxSteam.API)
```

| Layer | May depend on | Must not depend on |
| --- | --- | --- |
| domain | pure utilities | framework, database, HTTP, AWS SDK |
| application | domain | EF Core, controllers, AWS concrete types |
| infrastructure | domain, application interfaces | controllers |
| API | all registered services | direct DbContext usage in controllers |

## Dependency Rule

Inner layers must not depend on outer layers. Controllers delegate to application
services; services use `IUnitOfWork` and domain types.

## Parse-First Boundary Rule

HTTP request DTOs are parsed at the controller boundary. Services validate
business rules and map to domain entities before persistence.

Boundaries include:

- HTTP bodies and route parameters (`OboxSteam.Application/DTOs/`).
- JWT claims via `IClaimsService`.
- Environment variables and `appsettings` (loaded in `IocContainer`).
- EF entities returned from `GenericRepository`.
- AWS SNS webhook payloads (`AwsWebhookController`).
- CSV rows for question bank import.

Target flow:

```text
HTTP request
  -> controller + DTO
  -> application service
  -> domain entity / business rule
  -> infrastructure persistence or provider call
  -> ApiResult<T> response
```

## Command/Query Separation

Reads and writes are separated at the service level (`IProgramService`,
`IQuizAttemptService`, etc.) even though CQRS mediators are not used. Mutating
operations own validation and side effects; queries return DTOs.

## API Surface Map

| Controller | Route prefix | Domain |
| --- | --- | --- |
| AuthController | `/api/auth` | Registration, login, OTP, tokens |
| AccountController | `/api/account` | Profile and account settings |
| ProgramController | `/api/programs` | Program catalog and admin |
| ModuleController | `/api/modules` | Module CRUD |
| CourseController | `/api/courses` | Course CRUD |
| ActivityController | `/api/activities` | Activity CRUD |
| MaterialController | `/api/materials` | Learning materials |
| AssignmentController | `/api/assignments` | Assignments |
| QuestionBankController | `/api/question-banks` | Question banks |
| QuizController | `/api` | Quiz attempt lifecycle |
| RetrospectiveController | `/api` | Retrospective draft and submit |
| ProgramEnrollmentController | `/api/program-enrollments` | Program enrollment |
| ModuleEnrollmentController | `/api/module-enrollments` | Module enrollment |
| ParentController | `/api/parent` | Parent–student linking |
| ExpertController | `/api/experts` | Expert profiles |
| ProgramReviewController | `/api/programs/{id}/reviews` | Student reviews |
| MediaController | `/api/media` | Media assets |
| HighlightVideoController | `/api/highlight-video` | Class-scoped highlight stacks |
| AwsWebhookController | `/api/webhooks/aws` | SNS callbacks |
| SeedController | `/api/seed` | Dev seed data |

## Startup Sequence

1. Load `.env` from solution root.
2. Configure CORS, JSON, JWT, Swagger, SignalR, DI.
3. Apply EF migrations (fail fast on error).
4. Ensure Rekognition collection and S3 bucket exist.
5. Map controllers and run on port 5000.

## Observability Contract

`GlobalExceptionMiddleware` logs warnings for 4xx business errors and errors
with stack traces for 5xx. Structured request logging per
`docs/ARCHITECTURE.md` observability template is partially met via middleware
logging; a full canonical JSON request log line is not yet implemented.

Audit product records (submissions, attendance, enrollment changes) live in
PostgreSQL entities; distinguish them from operational application logs.

## Validation Ladder (Current)

| Command | Status |
| --- | --- |
| `dotnet build OboxSteam.API/OboxSteam.API.csproj` | Available |
| `dotnet test OboxSteam.Test/OboxSteam.Test.csproj` | Available (unit) |
| Integration / E2E / platform scripts | Not defined |

Epic-level proof snapshot: `docs/TEST_MATRIX.md`.
