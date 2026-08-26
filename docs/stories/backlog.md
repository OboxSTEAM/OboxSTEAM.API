# Story Backlog

Epic-level backlog derived from the implemented OboxSTEAM.API codebase. Create
story packets under `docs/stories/epics/` or plans under `docs/plans/active/`
when work is selected. Proof snapshot: `docs/TEST_MATRIX.md`.

## Candidate Epics

| Epic | Description | Status | Proof |
| --- | --- | --- | --- |
| E01-auth-accounts | JWT auth, OTP, account endpoints | implemented | unit partial + build |
| E02-curriculum | Programs, modules, courses, activities, materials | implemented | unit + build |
| E03-enrollment | Program/module/class enrollment, progress, retake | implemented | unit + build; ladder WIP |
| E04-assessment | Assignments, question banks, quiz lifecycle | implemented | unit + build |
| E05-media-aws | S3, Rekognition, MediaConvert, SNS webhooks | implemented | build / manual |
| E06-parent-experts | Parent linking, experts, reviews, highlight video | implemented | unit + build |
| E07-test-harness | Automated unit suite | implemented | `OboxSteam.Test` |
| E08-student-skills | Skill catalog + student skill snapshots | implemented | schema + APIs |
| E09-mentor-skills | Transparent mentor skill profiles | implemented | unit + build |

## Suggested Next Stories

| ID | Title | Lane | Notes |
| --- | --- | --- | --- |
| US-DOT3-BE-B1 | Retake ladder schema | high-risk | Done (migration + compile slice) |
| US-DOT3-BE-B2+ | WS7e–WS7h retake ladder behavior | high-risk | After schema |
| US-001 | Integration test project | normal | Persistence / auth gates |
| US-002 | Quiz grading integration tests | normal | Covers US-E04 |
| US-003 | Auth and role enforcement tests | high-risk | Auth hard gate |

## Story Paths

```text
docs/stories/epics/E03-enrollment/US-DOT3-BE-B1-retake-ladder-schema.md
docs/stories/epics/E03-enrollment/US-E03-001-student-weekly-schedule.md
docs/stories/epics/E06-parent-experts/US-E06-002-notification-template-engine.md
docs/plans/active/   # prefer for multi-session work
```
