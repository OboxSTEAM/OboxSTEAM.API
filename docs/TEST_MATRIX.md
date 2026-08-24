# Test Matrix

Markdown snapshot of epic-level contracts and proof. Update this table when an
epic’s proof level changes. Day-to-day proof is `dotnet test` / `dotnet build`,
not a Harness SQLite matrix.

## Status Values

| Status | Meaning |
| --- | --- |
| planned | Accepted as intended behavior, not implemented |
| in_progress | Actively being built |
| implemented | Implemented; see Evidence |
| changed | Contract changed after earlier implementation |
| retired | No longer part of the product contract |

## Matrix

| Story | Contract | Unit | Integration | E2E | Platform | Status | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| US-E01 | Auth: register, login, JWT, OTP, refresh | partial | no | no | no | implemented | unit coverage growing; build |
| US-E02 | Curriculum CRUD: program → activity | partial | no | no | no | implemented | `OboxSteam.Test` service tests |
| US-E03 | Enrollment: program, module, class, schedule | partial | no | no | no | implemented | unit tests; retake ladder in progress |
| US-E04 | Quiz: start, draft, submit, grade | partial | no | no | no | implemented | unit tests |
| US-E05 | AWS: S3, webhooks, MediaConvert, Rekognition | no | no | no | no | implemented | build / manual |
| US-E06 | Parent link, experts, reviews, highlights | partial | no | no | no | implemented | unit tests |
| US-E07 | Automated test suite | yes | no | no | no | implemented | `OboxSteam.Test` (~1230 unit tests) |
| US-E08 | Student skills | partial | no | no | no | implemented | schema + APIs |
| US-E09 | Mentor skills | yes | no | no | no | implemented | unit + build |

## Evidence Rules

- **Unit**: validators, application services, pure domain rules in `OboxSteam.Test`.
- **Integration**: EF persistence, auth enforcement, webhooks (mostly still open).
- **E2E**: multi-step HTTP flows (open).
- **Platform**: migration-on-startup, AWS smoke, deploy health (open).

Do not claim a proof column without a command that failed for the right reason
when broken and passed when fixed.
