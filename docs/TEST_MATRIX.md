# Test Matrix

Behavior-to-proof map for OboxSTEAM.API. Current proof status is also
queryable from the durable layer:

```bash
scripts/bin/harness-cli query matrix
```

On Windows:

```powershell
.\scripts\bin\harness-cli.exe query matrix
```

## Status Values

| Status | Meaning |
| --- | --- |
| planned | Accepted as intended behavior, not implemented |
| in_progress | Actively being built |
| implemented | Implemented and proof exists |
| changed | Contract changed after earlier implementation |
| retired | No longer part of the product contract |

## Matrix

| Story | Contract | Unit | Integration | E2E | Platform | Status | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| US-E01 | Auth: register, login, JWT, OTP, refresh | no | no | no | no | implemented | `dotnet build` only |
| US-E02 | Curriculum CRUD: program → activity | no | no | no | no | implemented | `dotnet build` only |
| US-E03 | Enrollment: program, module, class | no | no | no | no | implemented | `dotnet build` only |
| US-E04 | Quiz: start, draft, submit, grade | no | no | no | no | implemented | `dotnet build` only |
| US-E05 | AWS: S3, webhooks, MediaConvert, Rekognition | no | no | no | no | implemented | `dotnet build` only |
| US-E06 | Parent link, experts, reviews, highlights | no | no | no | no | implemented | `dotnet build` only |
| US-E07 | Automated test suite | no | no | no | no | planned | none |

## Evidence Rules

- Unit proof covers pure domain and application rules (validators, quiz grading).
- Integration proof covers EF persistence, auth enforcement, enrollment gates,
  webhook signature handling.
- E2E proof covers multi-step student flows through the HTTP API.
- Platform proof covers migration startup, AWS smoke checks, deployment health.
- A story may be `implemented` with partial proof if the story packet documents
  gaps; current epic rows are build-only until tests land.

## Verification Command

Configured on epic stories in the durable layer:

```bash
dotnet build OboxSteam.API/OboxSteam.API.csproj
```

Do not claim unit, integration, E2E, or platform proof until corresponding
tests exist and pass.
