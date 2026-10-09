# External Integrations

## PostgreSQL

- ORM: Entity Framework Core with Npgsql (`IocContainer.SetupDbContext`).
- Connection string `ConnectionStrings:DefaultConnection` (env
  `ConnectionStrings__DefaultConnection`); startup throws when missing.
  `appsettings.json` carries a localhost default (port 5433).
- Npgsql retry on failure: 10 retries, max 30 s delay. Legacy timestamp
  behavior is disabled. `CurriculumChangeInterceptor` is attached to the
  context.
- Migrations applied at startup (`MigrationExtensions.ApplyMigrations`); a
  migration failure stops the app. After migrating, startup backfills built-in
  portfolio sections (`IPortfolioService.EnsureBuiltInSectionsForAllPortfoliosAsync`).
- Design-time factory: `OboxSteamDbContextFactory`.
- `docker-compose.yml` runs `postgres:15` as `oboxsteam.database`.

## AWS S3

- `IBlobService` / `BlobService` for object storage (avatars, media, uploads,
  and portfolio-scoped images under `portfolio/{studentId}/{portfolioId}/`).
- `IAmazonS3` singleton requires `AWS_ACCESS_KEY` and `AWS_SECRET_KEY`
  (startup throws when missing); `AWS_REGION` defaults to `ap-southeast-1`.
- Bucket `AWS_S3_BUCKET` (default `oboxsteam-bucket-main`).
- At startup `EnsureBucketExistsAsync` creates the bucket if missing, disables
  Block Public Access, and applies a public `s3:GetObject` policy for every key
  except `materials/*` (served via authorized or presigned URLs).

## AWS Rekognition

- Face collection `oboxsteam-faces` created at startup if missing.
- `IFaceRecognitionService` indexes avatar faces, searches faces in images,
  and starts async video face search and label detection.
- Async jobs publish completion to SNS only when both `AWS_SNS_TOPIC_ARN` and
  `AWS_REKOGNITION_ROLE_ARN` are set; otherwise results must be polled
  (e.g. `POST /api/media/{mediaId}/process-tags`).
- `FaceEmbedding` entity links users to face data.

## AWS MediaConvert

- `IVideoConverterService` transcodes activity media and builds personal
  highlight videos.
- Requires `AWS_MEDIACONVERT_ENDPOINT` at startup and
  `AWS_MEDIACONVERT_ROLE_ARN` when submitting jobs. Optional
  `AWS_WATERMARK_URI` overrides the default highlight-video watermark image.
- Completion arrives as an EventBridge `MediaConvert Job State Change` event
  delivered via SNS to `AwsWebhookController` (`/api/webhooks/aws`).

## AWS Bedrock (Mantle)

- `IStrengthMatchService` is implemented by
  `BedrockMantleStrengthMatchService`, used by `PersonalVideoService` to match
  a student's described strength against face windows and Rekognition label
  timelines (or labels only when no face was detected).
- `IocContainer.SetupBedrockMantle` registers a singleton OpenAI SDK
  `ChatClient` for model `moonshotai.kimi-k2.5` against the OpenAI-compatible
  endpoint `https://bedrock-mantle.{BEDROCK_MANTLE_REGION}.api.aws/v1`.
- Auth is a Bedrock API key (`BEDROCK_API_KEY`, required at startup), not the
  IAM access keys. `BEDROCK_MANTLE_REGION` defaults to `ap-southeast-2`.
- Calls use temperature 0 and at most 4096 output tokens. Matches below score
  0.65 are dropped; point detections need 0.75 and specific activity evidence.
- Each call emits an `OboxSteam.AI` `strength-match` span with `gen_ai.*`
  metadata; prompt and completion text are not attached.

## Resend (Email)

- `IEmailService` sends transactional email: registration success, OTP
  verification, password change, forgot-password link, magic link, approve
  link, payment request to parent, payment invoice, enrollment confirmation,
  inbox notification, and staff account credentials (Mentor/Expert
  provisioning). Templates are Vietnamese.
- Every send retries up to 3 attempts, then throws a 500 `InternalException`.
  `MentorService` and `ExpertService` roll back the new account when the
  credentials email still fails.
- `RESEND_APITOKEN` is required at startup. `RESEND_FROM` defaults to
  `noreply@contact.oboxsteam.website`. Links use `APP_BASE_URL` (default
  `https://oboxsteam.website`); certificate and payment links prefer
  `APP_FRONTEND_URL` and fall back to `APP_BASE_URL`.
- `Email:SkipInDevelopment=true` skips sending and logs a warning instead.

## Stripe (Payments)

- `IStripePaymentService` / `StripePaymentService` creates Checkout sessions;
  Stripe is the only gateway accepted for online checkout (`VnPay` and
  `BankTransfer` exist on `PaymentGateway` but are rejected).
- Settings from `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY`,
  `STRIPE_WEBHOOK_SECRET` (`SetupPaymentGateways`; empty when unset, no
  startup check).

## JaaS (8x8 Meetings)

- `IJaasJwtService` issues RS256 meeting JWTs (3-hour lifetime) for LiveOnline
  sessions via `POST /api/class-sessions/{id}/join`. Mentors are moderators;
  recording, livestreaming, transcription, and outbound calls are disabled.
- `IocContainer.SetupJaas` requires `JaaS__AppId` and `JaaS__KeyId` at startup;
  `JaaS__Domain` defaults to `8x8.vc`.
- Private key: `JaasPrivateKeyResolver` prefers the PEM file at
  `JaaS__PrivateKeyPath`, else inline `JaaS__PrivateKey` (escaped `\n`
  allowed); startup throws when neither is set or the file is missing or
  unreadable.

## SignalR

- Hub `/hubs/notifications` (`NotificationHub`, `[Authorize]`), detailed
  errors enabled, enums serialized as strings.
- JS clients pass the JWT as `?access_token=` on hub requests.
- Client events `notificationReceived` and `syncEvent` are defined in
  `docs/product/notifications.md` (Realtime Sync Events).

## Webhooks

`AwsWebhookController` (`POST /api/webhooks/aws`, no JWT) handles AWS SNS:

- Verifies SNS signature version 1 against the signing certificate; the
  certificate URL and `SubscribeURL` must be HTTPS on `*.amazonaws.com` or
  `*.aws.amazon.com`. Invalid signatures return 401.
- Confirms `SubscriptionConfirmation` messages.
- Routes MediaConvert `COMPLETE`/`ERROR` to `IMediaService`, falling back to
  `IPersonalVideoService` for highlight-video jobs.
- Routes Rekognition `SUCCEEDED`/`FAILED`: `StartLabelDetection` to label
  detection handling, everything else to face search handling.

`PaymentController.StripeWebhook` (`POST /api/payments/stripe-webhook`,
anonymous):

- Requires the `Stripe-Signature` header and verifies it with
  `STRIPE_WEBHOOK_SECRET` (invalid signature returns 400).
- `checkout.session.completed` marks the payment successful;
  `checkout.session.expired` marks it failed; other events are ignored.

## OpenTelemetry (Traceway)

- `ObservabilityExtensions.AddObservability` exports traces, metrics, and logs
  over OTLP/HTTP (protobuf) only when both `OTEL_EXPORTER_OTLP_ENDPOINT` (base
  URL ending in `/api/otel`) and `TRACEWAY_BACKEND_TOKEN` are set; otherwise
  nothing is exported.
- Service name `OTEL_SERVICE_NAME` (default `oboxsteam-api`), version
  `IMAGE_TAG` (default `unknown`), `deployment.environment` from the host
  environment.
- Traces: ASP.NET Core (excluding `/`, `/swagger`, `/hubs`,
  `/custom-swagger.js`, `/favicon.ico`, `/index.html`; tagged with `user.id`
  and `user.role`), HttpClient, AWS SDK, Npgsql, `OboxSteam.BackgroundJobs`,
  `OboxSteam.AI`. Metrics: .NET runtime.
- Logs pass through `TelemetryLogRedactionProcessor`, which masks email
  addresses and redacts `Desc`, `Body`, and `Raw` values. Console logging is
  unaffected.
- `GlobalExceptionMiddleware` records only 5xx exceptions on the request span.

## Environment Loading

`EnvFileLoader.LoadFromSolutionRoot()` runs before the host is built. It walks
up from the current directory to the first `.env`, skips blank and `#` lines,
strips surrounding quotes, and never overwrites variables already set.
Configuration then layers `appsettings.json`,
`appsettings.{Environment}.json`, and environment variables.

## Redis (Inactive)

`IRedisService` / `RedisService` (StackExchange.Redis) exist, but
`SetupRedis` is commented out in `IocContainer` and the Redis service is
commented out in `docker-compose.yml`. Redis is not used at runtime.

## Operational Dependencies

| Variable / Config | Required for |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Database (startup fails if missing) |
| `JWT__SecretKey`, `JWT__Issuer`, `JWT__Audience` | Authentication (startup fails if missing; token issuance needs a secret of at least 32 characters) |
| `AWS_ACCESS_KEY`, `AWS_SECRET_KEY` | S3, Rekognition, MediaConvert (startup fails if missing) |
| `AWS_REGION` | S3 and Rekognition region (default `ap-southeast-1`) |
| `AWS_S3_BUCKET` | Bucket name (default `oboxsteam-bucket-main`) |
| `AWS_MEDIACONVERT_ENDPOINT` | MediaConvert client (startup fails if missing) |
| `AWS_MEDIACONVERT_ROLE_ARN` | MediaConvert job submission |
| `AWS_SNS_TOPIC_ARN`, `AWS_REKOGNITION_ROLE_ARN` | Rekognition SNS completion (optional) |
| `AWS_WATERMARK_URI` | Highlight-video watermark override (optional) |
| `BEDROCK_API_KEY` | Bedrock Mantle (startup fails if missing) |
| `BEDROCK_MANTLE_REGION` | Bedrock Mantle region (default `ap-southeast-2`) |
| `RESEND_APITOKEN` | Email (startup fails if missing) |
| `RESEND_FROM` | Sender address (optional) |
| `APP_BASE_URL`, `APP_FRONTEND_URL` | Links in emails, payments, certificates (optional) |
| `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY`, `STRIPE_WEBHOOK_SECRET` | Stripe checkout and webhook |
| `JaaS__AppId`, `JaaS__KeyId` | JaaS meetings (startup fails if missing) |
| `JaaS__PrivateKeyPath` or `JaaS__PrivateKey` | JaaS signing key (startup fails if neither) |
| `JaaS__Domain` | JaaS domain (default `8x8.vc`) |
| `CORS_ALLOWED_ORIGINS` (or `CORS_ALLOWED_ORIGIN`) | Non-Development browser clients |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `TRACEWAY_BACKEND_TOKEN` | Telemetry export (optional) |
| `OTEL_SERVICE_NAME`, `IMAGE_TAG` | Telemetry resource attributes (optional) |
| `Email:SkipInDevelopment` | Skip email sending (optional) |

`docker-compose.yml` passes these through to `obox-backend` (except
`APP_FRONTEND_URL`, `AWS_WATERMARK_URI`, and `Email:SkipInDevelopment`) plus
compose-only `DOCKERHUB_USERNAME`, `API_PORT`, and `POSTGRES_*`.
`.env.example` lists the same keys except the optional `APP_FRONTEND_URL`,
`AWS_WATERMARK_URI`, `JaaS__PrivateKeyPath`, `CORS_ALLOWED_ORIGIN`,
`Email:SkipInDevelopment`, and the OpenTelemetry/Traceway variables
(`IMAGE_TAG` is included). `appsettings.json` ships default
`DefaultConnection` and `JWT` values.

Local development may use `.env` and the Development CORS policy (allow all).
