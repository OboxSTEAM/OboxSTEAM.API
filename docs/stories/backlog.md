# Story Backlog

Epic-level backlog derived from the implemented OboxSTEAM.API codebase. Create
individual story packets under `docs/stories/epics/` when work is selected.

Durable status: `scripts/bin/harness-cli query matrix`.

## Candidate Epics

| Epic | Description | Status | Proof |
| --- | --- | --- | --- |
| E01-auth-accounts | JWT auth, OTP, account endpoints | implemented | build only |
| E02-curriculum | Programs, modules, courses, activities, materials | implemented | build only |
| E03-enrollment | Program/module/class enrollment, progress | implemented | build only |
| E04-assessment | Assignments, question banks, quiz lifecycle | implemented | build only |
| E05-media-aws | S3, Rekognition, MediaConvert, SNS webhooks | implemented | build only |
| E06-parent-experts | Parent linking, experts, reviews, highlight video | implemented | build only |
| E07-test-harness | Test project, CI, proof ladder | unsliced | none |
| E08-student-skills | Skill catalog + student skill snapshots | schema done | build |
| E09-mentor-skills | Transparent mentor skill profiles | implemented | unit + build |

## Suggested Next Stories

| ID | Title | Lane | Notes |
| --- | --- | --- | --- |
| US-E09-001 | Expand mentor skill schema | high-risk | Done (schema + docs) |
| US-E09-002 | Mentor skill CRUD and visibility APIs | high-risk | Done (APIs + tests) |
| US-001 | Add integration test project | normal | Foundation for all proof columns |
| US-002 | Quiz grading integration tests | normal | Covers US-E04 contract |
| US-003 | Auth and role enforcement tests | high-risk | Touches auth hard gate |
| US-004 | Class session and attendance API docs + tests | normal | Domain exists; API surface needs audit |
| US-005 | Payment flow contract | high-risk | Entities exist; public API unclear |

## Story Paths

```text
docs/stories/epics/E01-auth-accounts/US-003-auth-role-tests.md
docs/stories/epics/E04-assessment/US-002-quiz-grading-tests.md
```

High-risk stories use `docs/templates/high-risk-story/` folders.
