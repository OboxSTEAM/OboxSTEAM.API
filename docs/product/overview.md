# OboxSTEAM — Product Overview

## Summary

OboxSTEAM is a STEAM education platform backend API. It supports structured
learning programs, cohort-based classes, student progress, assessments, parent
visibility, Stripe payments, student portfolios, live online sessions, and
media-rich learning experiences (face-tagged media, personal highlight videos).
The API serves browser and mobile clients; this repository is the .NET backend
only.

## Users and Roles

Roles come from `RoleType`: `Admin`, `Manager`, `Mentor`, `Parent`, `Student`,
`Expert`.

| Role | Primary responsibility |
| --- | --- |
| Admin | Platform administration; shares most Manager routes (some are Manager-only, e.g. `POST /api/skills`) |
| Manager | Curriculum, classes, mentor staffing, question banks, assignments, skill catalog |
| Expert | Framework blueprints, advisory chat and curriculum approval, Offline co-teach |
| Mentor | Class delivery (one mentor per `Class`), sessions, student guidance, self-managed skill profile |
| Student | Enrollment, activities, assignments, quizzes, submissions, portfolio |
| Parent | Linked student visibility, approvals, payments |

Roles are enforced with JWT role claims on controller actions. See
`docs/product/permissions.md`.

## Core Product Surfaces

- REST API at `/api/*` with Swagger UI at `/` in Development and Production.
- SignalR hub `/hubs/notifications` for notifications and realtime sync (see
  `notifications.md`).
- AWS SNS webhook `/api/webhooks/aws` for MediaConvert and Rekognition job
  callbacks.
- Stripe webhook `POST /api/payments/stripe-webhook`.
- JaaS (8x8) meeting tokens for LiveOnline sessions
  (`POST /api/class-sessions/{id}/join`).
- Hosted background services: `PendingEnrollmentCleanupService`,
  `ClassSeatHoldCleanupService`, `OpenClassAutoStartService`,
  `PersonalVideoGenerationWorker`, `SessionReminderService`,
  `SessionLifecycleService`, `AssignmentWindowCloseService`,
  `DiscussionAttachmentPurgeService`.
- Data seeding `POST /api/seed/all` and reset `DELETE /api/seed/clear`
  (`SeedController` has no `[Authorize]` attribute).

## Curriculum Hierarchy

```text
Program
  ├── ProgramSkill[] (catalog skills taught)
  └── Module (Theory | Experiential | Research)
        ├── Course (ordered by CourseOrder)
        │     └── Activity (SelfPaced | LiveOnline | Offline)
        │           └── Material (SelfPaced only, at most one)
        ├── Assignment (Quiz | FileUpload | Retrospective; module- or course-scoped)
        └── ResearchMilestone (Research modules; owns one Assignment)
Class (cohort / đợt học; Standard | Remedial; one optional Mentor)
  └── ClassSession (LiveOnline | Offline | AssignmentWindow)
```

Modules can have prerequisites (`PrerequisiteModuleId`). Tuition is
program-level (`Program.Price`); modules carry no price. Classes group students
moving through a program on a shared schedule. Failed or withdrawn purchases
close (`Failed`/`Dropped`) and continuing is a windowed rebuy into another
eligible cohort (chuyen ca), not a module retake. Continuity and in-window
rebuy checkout charge 50% of `Program.Price` (`ProgramPurchaseLifecycle`);
`Program.RetakeFee` exists on the schema but is not used for checkout amounts.
See `docs/product/enrollment.md`.

## Major Capability Areas

| Area | Product doc |
| --- | --- |
| API response shape and errors | `api-conventions.md` |
| Roles and authorization | `permissions.md` |
| Programs, modules, courses, activities, classes | `curriculum.md` |
| Program/module/class enrollment | `enrollment.md` |
| Assignments, quizzes, question banks | `assessment.md` |
| Notifications and realtime sync | `notifications.md` |
| Skill catalog, program skills, student skills | `student-skills.md` |
| Mentor skills and evidence | `mentor-skills.md` |
| Database, AWS, AI, email, payments, meetings, telemetry | `integrations.md` |

## Deployment (In Repo)

- `OboxSteam.API/Dockerfile`: multi-stage .NET 8 build and publish.
- `docker-compose.yml`: `obox-backend` (image
  `${DOCKERHUB_USERNAME}/oboxsteam-api:${IMAGE_TAG}`, host `${API_PORT}` to
  container port 5000) and `oboxsteam.database` (`postgres:15` with health
  check). A Redis service is present but commented out.
- `.github/workflows/deploy.yml`: on push to `main`, builds and pushes the
  image to Docker Hub (tags `latest` and the commit SHA), then over SSH on the
  VPS pins `IMAGE_TAG` in `.env` and runs `docker compose pull` and
  `docker compose up -d --force-recreate obox-backend`.

## Out of Scope (This Repo)

- Frontend application code.
- Infrastructure-as-code for AWS resources (SNS topics, IAM roles,
  MediaConvert, EventBridge rules); these are provisioned outside the repo.

## Living Contract

Product truth lives in `docs/product/*` plus executable proof (`dotnet test` /
`dotnet build`; see `docs/TEST_MATRIX.md`). When behavior changes, update the
affected product doc and keep any active plan or story packet current.
