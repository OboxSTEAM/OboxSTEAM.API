# External Integrations

## PostgreSQL

- ORM: Entity Framework Core with Npgsql.
- Connection via environment configuration (see `IocContainer.SetupDbContext`).
- Migrations applied at startup (`MigrationExtensions.ApplyMigrations`).
- Design-time factory: `OboxSteamDbContextFactory`.

## AWS S3

- `IBlobService` / `BlobService` for object storage (avatars, media, uploads).
- Requires `AWS_ACCESS_KEY`, `AWS_SECRET_KEY`, `AWS_REGION` (default
  `ap-southeast-1`).
- Bucket existence checked at startup.

## AWS Rekognition

- Face collection `oboxsteam-faces` created at startup if missing.
- `IFaceRecognitionService` for face embedding and recognition flows.
- `FaceEmbedding` entity links users to face data.

## AWS MediaConvert

- `IVideoConverterService` for highlight video transcoding.
- Completion signaled via SNS → `AwsWebhookController` (`/api/webhooks/aws`).

## AWS Bedrock

- Registered in DI for AI-assisted features (`IAmazonBedrockRuntime`).

## Resend (Email)

- `IEmailService` sends transactional email (OTP, password reset, notifications).
- Requires `RESEND_APITOKEN` configuration.

## SignalR

- Hub registered with detailed errors enabled.
- Real-time client contracts are not defined in this API repo.

## Webhooks

`AwsWebhookController` handles AWS SNS notifications:

- Confirms SNS subscriptions.
- Validates signing certificates.
- Routes MediaConvert job completion to video services.

## Environment Loading

`EnvFileLoader.LoadFromSolutionRoot()` loads `.env` from solution root before
configuration binding.

## Redis (Optional / Commented)

`IRedisService` and Redis DI setup exist but are commented out in
`IocContainer`. Not active in current startup path.

## Operational Dependencies

| Variable / Config | Required for |
| --- | --- |
| PostgreSQL connection string | Database |
| `AWS_ACCESS_KEY`, `AWS_SECRET_KEY` | S3, Rekognition, MediaConvert, Bedrock |
| `RESEND_APITOKEN` | Email |
| `CORS_ALLOWED_ORIGINS` | Production browser clients |
| JWT settings in configuration | Authentication |

Local development may use `.env` and Development CORS policy (allow all).
