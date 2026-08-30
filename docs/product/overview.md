# OboxSTEAM — Product Overview

## Summary

OboxSTEAM is a STEAM education platform backend API. It supports structured
learning programs, cohort-based classes, student progress, assessments, parent
visibility, and media-rich learning experiences. The API serves browser and
mobile clients; this repository is the .NET backend only.

## Users and Roles

| Role | Primary responsibility |
| --- | --- |
| Admin | Full platform administration |
| Manager | Curriculum, classes, question banks, assignments |
| Expert | Framework blueprints, curriculum review, Offline co-teach |
| Mentor | Course delivery, class mentoring, student guidance |
| Student | Enrollment, activities, assignments, quizzes, submissions |
| Parent | Linked student visibility and approvals |

Roles are enforced with JWT role claims on controller actions. See
`docs/product/permissions.md`.

## Core Product Surfaces

- REST API at `/api/*` with Swagger UI at `/` in Development and Production.
- SignalR hub for real-time features (configured; consumers are client-side).
- AWS webhooks at `/api/webhooks/aws` for async media processing callbacks.

## Curriculum Hierarchy

```text
Program
  └── Module (Theory | Experiential | Research)
        ├── Course (mentor-owned)
        │     └── Activity (SelfPaced | LiveOnline | Offline)
        ├── Assignment (Quiz | FileUpload | Retrospective)
        └── Material
Class (cohort / đợt học)
  └── ClassSession (live/offline sessions and per-class AssignmentWindow)
```

Programs can be sold as bundles. Modules can have prerequisites; tuition and
the optional chuyen-ca fee (`Program.RetakeFee`) are program-level. Classes
group students moving through a program on a shared schedule. Failed or
withdrawn purchases close (`Failed`/`Dropped`) and continuing is a windowed
rebuy into another eligible cohort (chuyen ca), not a module retake — see
`docs/product/enrollment.md`.

## Major Capability Areas

| Area | Product doc |
| --- | --- |
| API response shape and errors | `api-conventions.md` |
| Roles and authorization | `permissions.md` |
| Programs, modules, courses, activities | `curriculum.md` |
| Program/module/class enrollment | `enrollment.md` |
| Assignments, quizzes, question banks | `assessment.md` |
| Student skills and evidence | `student-skills.md` |
| Mentor skills and evidence | `mentor-skills.md` |
| AWS, email, face recognition, media | `integrations.md` |

## Out of Scope (This Repo)

- Frontend application code.
- Infrastructure-as-code and deployment manifests (not present in repo).

## Living Contract

Product truth lives in `docs/product/*` plus executable proof (`dotnet test` /
`dotnet build`; see `docs/TEST_MATRIX.md`). When behavior changes, update the
affected product doc and keep any active plan or story packet current.
