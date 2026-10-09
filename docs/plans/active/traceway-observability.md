# Execution Plan: Traceway Observability (Cloud)

Date: 2026-10-09

## Status

Active (plan only, awaiting user approval before any code change)

## Outcome

The production API (`obox-backend` on the VPS) exports traces, logs, metrics,
exceptions, background-job runs, and Bedrock AI calls to the Traceway Cloud
project `OboxSTEAM Backend` (framework OpenTelemetry). The VPS host reports
CPU, memory, disk, and network to the same project. Local development and unit
tests export nothing.

## Context

- Traceway setup skill: `.agents/skills/traceway-setup/SKILL.md`,
  `.agents/skills/traceway-setup/data-model.md` (span classification rules).
- Architecture: `docs/ARCHITECTURE.md` (Observability Contract section).
- Startup: `OboxSteam.API/Program.cs`, `OboxSteam.API/Architecture/IocContainer.cs`.
- Errors: `OboxSteam.API/Middlewares/GlobalExceptionMiddleware.cs` catches every
  exception, so tracing never sees 5xx errors unless recorded manually.
- Background work: 8 `BackgroundService` classes in
  `OboxSteam.Infrastructure/Services/`.
- AI: `OboxSteam.Infrastructure/Services/BedrockMantleStrengthMatchService.cs`
  (one-shot `ChatClient.CompleteChatAsync`, OpenAI SDK 2.1.0, not conversational).
- Claims: `ClaimTypes.NameIdentifier` and `ClaimTypes.Role` (`JwtUtils`).
- Deploy: `.github/workflows/deploy.yml` edits the VPS `docker-compose.yml` in
  place; it never copies the repository compose file to the VPS.
- Navigation used CodeGraph MCP plus Grep/Read for non-indexed files.

## Decisions (confirmed with user)

- 2026-10-09: Traceway Cloud (`https://cloud.tracewayapp.com`), organization
  "Working State". Self-hosting rejected: VPS has less than 2 GB RAM.
- 2026-10-09: One project `OboxSTEAM Backend`, framework `opentelemetry`,
  `service.name` = `oboxsteam-api`.
- 2026-10-09: Backend only. `obox-frontend` (separate repo) is out of scope.
- 2026-10-09: Production only. Export is off unless both
  `OTEL_EXPORTER_OTLP_ENDPOINT` and `TRACEWAY_BACKEND_TOKEN` are set.
- 2026-10-09: Request spans carry `user.id` and `user.role` only (no email,
  name, tokens, or request bodies).
- 2026-10-09: AWS SDK instrumentation included.
- 2026-10-09: Bedrock call recorded as an AI Trace with metadata only (model,
  tokens, finish reason, latency); no prompt or completion text.
- 2026-10-09: Traceway OTel Agent installed on the VPS host for host metrics.
- 2026-10-09: `jq` is not installed; the skill's token-writing script is ported
  to PowerShell with the same guarantee (token values never printed).
- 2026-10-09: User tags are set through the ASP.NET Core instrumentation
  `EnrichWithHttpResponse` hook instead of a separate middleware (same result,
  one less file).
- 2026-10-09: Log audit found ~25 log lines with emails (Auth, Parent, Expert,
  Mentor services), the Bedrock strength description, and the SNS webhook body.
  User chose to mask them only in the exported copy:
  `TelemetryLogRedactionProcessor` masks emails (`v***@gmail.com`) and replaces
  `Desc`, `Body`, `Raw` values with `[redacted]`. Console logs unchanged.
- 2026-10-09: `OpenTelemetry.Instrumentation.AWS` pinned to 1.11.3, the last
  version supporting AWSSDK 3.7 (1.12+ requires AWSSDK v4).

## Scope

In scope:

- Traceway project creation via setup plan approval.
- OpenTelemetry wiring in `OboxSteam.API`.
- Exception recording in `GlobalExceptionMiddleware`.
- CONSUMER spans for the 8 hosted services.
- AI Trace span around the Bedrock call.
- User enrichment middleware.
- `docker-compose.yml` environment variables.
- VPS `.env` / compose updates and host agent install (manual, user-run).
- `docs/ARCHITECTURE.md` Observability Contract update.

Out of scope:

- Frontend SDK, session replay, source maps.
- Self-hosted Traceway, nginx, Docker network changes.
- Replacing console logging (it stays for `docker logs`).
- Alert rules and on-call (optional follow-up in the dashboard).

## Approach

### Phase 0: Already done

- Setup token validated (`GET /api/setup/session` returned 200, org "Working
  State", no projects).
- Plan draft submitted (`PUT /api/setup/plan` returned `{"status":"pending"}`).
- Temporary files in repo root (untracked, no secrets):
  `traceway-setup-plan.json`, `traceway-setup-wait.ps1`.

### Phase 1: Create the Traceway project (one terminal command)

1. User presses **Approve Setup** on `https://cloud.tracewayapp.com/setup`.
2. Agent runs the wait script once:
   `powershell -NoProfile -ExecutionPolicy Bypass -File .\traceway-setup-wait.ps1`.
   It writes `TRACEWAY_BACKEND_TOKEN` into the root `.env` (gitignored) and
   prints only the variable name and status.
3. User completes the **Next Steps** panel on the Traceway page: append
   `TRACEWAY_BACKEND_TOKEN=<token>` to `/root/apps/oboxsteam/oboxsteam-be/.env`.
4. Agent runs one wire check (token read from `.env` inside the command, never
   echoed): `POST /api/otel/v1/traces` with `{"resourceSpans":[]}` must return
   `200 {}` with `Content-Type: application/json`.
5. Agent deletes `traceway-setup-plan.json` and `traceway-setup-wait.ps1`.

The setup token expires 6 hours after it was generated. If approval happens
later, generate a new one at `/setup` and resubmit the plan (safe; projects are
matched by name).

### Phase 2: Code changes (repository)

1. Packages in `OboxSteam.API/OboxSteam.API.csproj` (latest stable for
   `net8.0`; AWS package version must support AWSSDK 3.7):
   - `OpenTelemetry.Extensions.Hosting`
   - `OpenTelemetry.Exporter.OpenTelemetryProtocol`
   - `OpenTelemetry.Instrumentation.AspNetCore`
   - `OpenTelemetry.Instrumentation.Http`
   - `OpenTelemetry.Instrumentation.Runtime`
   - `OpenTelemetry.Instrumentation.AWS`
   - No Npgsql package: Npgsql 8 emits spans natively on ActivitySource `Npgsql`.
2. New `OboxSteam.API/Architecture/ObservabilityExtensions.cs`
   (`AddObservability(this WebApplicationBuilder builder)`), called from
   `Program.cs` right after configuration is loaded:
   - Returns early (no exporter, no overhead) when
     `OTEL_EXPORTER_OTLP_ENDPOINT` or `TRACEWAY_BACKEND_TOKEN` is empty.
   - Resource: `service.name` from `OTEL_SERVICE_NAME` (default
     `oboxsteam-api`), `service.version` from `IMAGE_TAG`,
     `deployment.environment` from the ASP.NET Core environment.
   - Traces: ASP.NET Core (filter out `/swagger`, `/hubs`, static files),
     HttpClient, AWS, sources `Npgsql`, `OboxSteam.BackgroundJobs`,
     `OboxSteam.AI`.
   - Metrics: ASP.NET Core, HttpClient, runtime.
   - Logs: `builder.Logging.AddOpenTelemetry` with formatted messages; console
     logging unchanged.
   - Exporter: OTLP `HttpProtobuf` (Traceway has no gRPC), endpoint
     `https://cloud.tracewayapp.com/api/otel` (SDK appends `/v1/*`), header
     `Authorization=Bearer <TRACEWAY_BACKEND_TOKEN>` built in code.
3. `OboxSteam.API/appsettings.json`: add `Logging:OpenTelemetry:LogLevel` with
   `Microsoft.EntityFrameworkCore` = `Warning` so every SQL command is not
   shipped as an INFO log (Npgsql spans already cover SQL).
4. New `OboxSteam.Infrastructure/Observability/TelemetrySources.cs`: shared
   `ActivitySource` instances (`OboxSteam.BackgroundJobs`, `OboxSteam.AI`) and a
   `RecordException(Activity?, Exception)` helper that adds the standard
   `exception` event (type, message, stacktrace) and sets status Error. Uses
   only BCL `System.Diagnostics`; no new Infrastructure package.
5. `GlobalExceptionMiddleware`: for 5xx only, call the helper on
   `Activity.Current`. 4xx business errors stay warnings and do not create
   Issues. The response already returns 500, matching Traceway's rule.
6. New `OboxSteam.API/Middlewares/TelemetryUserEnrichmentMiddleware.cs`,
   registered after `UseAuthentication()`: sets `user.id` and `user.role` on
   `Activity.Current` from `ClaimTypes.NameIdentifier` / `ClaimTypes.Role`.
7. Hosted services: wrap each work iteration (not the `Task.Delay`) in a
   CONSUMER span with a stable task name; record caught exceptions with the
   helper. Task names:
   - `PendingEnrollmentCleanupService` -> `pending-enrollment-cleanup`
   - `ClassSeatHoldCleanupService` -> `class-seat-hold-cleanup`
   - `OpenClassAutoStartService` -> `open-class-auto-start`
   - `PersonalVideoGenerationWorker` -> `personal-video-generation` (one span
     per dequeued job)
   - `SessionReminderService` -> `session-reminder`
   - `SessionLifecycleService` -> `session-lifecycle`
   - `AssignmentWindowCloseService` -> `assignment-window-close`
   - `DiscussionAttachmentPurgeService` -> `discussion-attachment-purge`
   Counts (for example reminders sent) go in span attributes, never in names.
8. `BedrockMantleStrengthMatchService.CompleteMatchAsync`: CLIENT span
   `strength-match` on `OboxSteam.AI` with `gen_ai.system`,
   `gen_ai.operation.name` = `chat`, `gen_ai.request.model`,
   `gen_ai.response.model`, integer `gen_ai.usage.input_tokens` /
   `output_tokens` / `total_tokens`, `gen_ai.response.finish_reason`,
   `trace.name` = `strength-match`. No prompt or completion attributes.
9. `docker-compose.yml` (`obox-backend.environment`):
   - `OTEL_SERVICE_NAME=oboxsteam-api`
   - `OTEL_EXPORTER_OTLP_ENDPOINT=${OTEL_EXPORTER_OTLP_ENDPOINT:-}`
   - `TRACEWAY_BACKEND_TOKEN=${TRACEWAY_BACKEND_TOKEN:-}`
   - `IMAGE_TAG=${IMAGE_TAG:-latest}`
10. Log privacy audit before enabling: search `_logger.Log*` calls for OTP
    codes, passwords, tokens, emails, and raw payloads; mask or lower them.
    Known candidate: Bedrock parse-failure log writes `Raw={Raw}`.
11. `docs/ARCHITECTURE.md`: rewrite the Observability Contract section.

### Phase 3: VPS (user runs; agent provides exact commands)

1. In `/root/apps/oboxsteam/oboxsteam-be/.env`: confirm
   `TRACEWAY_BACKEND_TOKEN` (Phase 1 step 3) and add
   `OTEL_EXPORTER_OTLP_ENDPOINT=https://cloud.tracewayapp.com/api/otel`.
2. In the VPS `docker-compose.yml`: add the four variables from Phase 2 step 9.
3. Check free memory (`free -h`), then install the host agent:
   `curl -fsSL https://install.tracewayapp.com/install.sh | TRACEWAY_TOKEN=<token> TRACEWAY_SERVICE_NAME=obox-vps bash`
   (`TRACEWAY_ENDPOINT` omitted for Traceway Cloud).
4. Push to `main`; the existing workflow builds and recreates `obox-backend`.

### Phase 4: Verify in Traceway (last 15 minutes)

- `/endpoints`: call one parametrized route with 3 different ids; exactly one
  row (for example `GET /api/programs/{id}`) with real status codes.
- `/issues`: one deliberate 500 appears with stack trace, linked to its
  endpoint; a 404/400 business error does not appear.
- Endpoint detail: Npgsql SQL and outgoing HTTP/AWS calls show as child spans.
- `/tasks`: each hosted service appears under its stable name.
- `/logs`: INFO and ERROR lines carry the request's trace id.
- `/ai-traces`: one strength-match call with model and token counts.
- `/dashboards` and `/organization`: runtime metrics and host `obox-vps`.

## Risks And Recovery

- Telemetry volume vs Traceway Cloud plan limits: EF command logs filtered,
  Swagger/SignalR filtered; review usage after 24 hours.
- Sensitive data in logs: Phase 2 step 10 audit before production enable.
- VPS memory (agent): check `free -h` first; skip the agent if tight.
- Traceway outage: exports are batched in the background; failures drop
  batches and never block requests.
- Rollback (no redeploy): remove `OTEL_EXPORTER_OTLP_ENDPOINT` from the VPS
  `.env`, then `docker compose up -d --force-recreate obox-backend`. Export is
  off. Code rollback: revert the commit.

## Progress

- [x] Phase 0: setup token validated, plan draft submitted.
- [x] Phase 1: project `OboxSTEAM Backend` created, `TRACEWAY_BACKEND_TOKEN`
  written to local `.env`, wire check `HTTP 200 {}` (application/json),
  temporary setup files deleted. VPS `.env` token pending (user, Next Steps panel).
- [x] Phase 2: code changes done. `dotnet build` 0 errors (11 pre-existing
  warnings in untouched files); `run-coverage.ps1` 2243/2243 passed, including
  4 new `TelemetrySourcesTests`.
- [ ] Phase 3: VPS env, compose, host agent, deploy.
- [ ] Phase 4: dashboard verification.

## Validation

- Focused proof: Phase 1 wire check (`200 {}` JSON); Phase 4 dashboard checks.
- Repository-required checks: `dotnet build OboxSteam.API/OboxSteam.API.csproj`
  with zero warnings introduced; `.\scripts\run-coverage.ps1` passes (export is
  off in tests because the variables are unset).

## Result

Pending.
