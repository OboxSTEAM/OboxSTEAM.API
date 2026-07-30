# OboxSTEAM — Product Overview

## Summary

OboxSTEAM is a STEAM education platform backend API. It supports structured
learning programs, cohort-based classes, student progress, assessments, parent
visibility, and media-rich learning experiences. The API serves browser and
mobile clients; this repository is the .NET backend only.

## Users and Roles

| Role | Primary responsibility |
| --- | --- |
| SuperAdmin | Full platform administration |
| Manager | Curriculum, classes, question banks, assignments |
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
  └── ClassSession (scheduled sessions tied to activities)
```

Programs can be sold as bundles. Modules can have prerequisites, individual
prices, and retake fees. Classes group students moving through a program on a
shared schedule.

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
- Automated test projects (not present yet; see `docs/TEST_MATRIX.md`).

## Living Contract

Product truth lives in `docs/product/*` plus executable proof recorded in the
Harness durable layer (`scripts/bin/harness-cli query matrix`). When behavior
changes, update the affected product doc and story proof status together.
