# Design

## Domain Model

- `Portfolio` (extended): `AvatarUrl` (500), `CoverImageUrl` (500),
  `LastPublishedAt` (nullable), `HasUnpublishedChanges` (bool, default false),
  `PublishedSnapshot` (JSONB, nullable). Navigations: `Sections`,
  `MediaAssets`.
- `PortfolioCustomItem` (extended): `AccentColor` (20, hex), `IsFeatured`
  (nullable bool), `Span` (nullable enum `PortfolioItemSpan`:
  Single|Wide|Tall|Large). Navigation: `MediaPlacements`.
- `PortfolioSection` (new): `PortfolioId`, `Kind` (enum
  `PortfolioSectionKind`: ProjectsGroup|ActivitiesGroup|LinksGroup|RichText|
  Gallery|Embed), `Title` (255), `DisplayOrder`, `IsVisible`, `ContentHtml`
  (sanitized), `SettingsJson` (JSONB). Navigation: `MediaPlacements`.
  Unique filtered index on `(PortfolioId, Kind)` for built-in group kinds.
- `PortfolioMediaAsset` (new): `PortfolioId`, `Type` (enum
  `PortfolioMediaType`: Image),
  `Url` (500), `S3Key` (512), `FileName` (255), `ContentType` (100),
  `SizeBytes` (long). Owned by the portfolio (and thus the student).
- `PortfolioMediaPlacement` (new): join/placement row referencing
  `PortfolioMediaAssetId` and exactly one owner —
  `PortfolioCustomItemId` (nullable) or `PortfolioSectionId` (nullable) —
  plus `Caption` (255) and `DisplayOrder`. Replace-on-update semantics; cap 20
  per owner (service-enforced).

All new entities extend `BaseEntity` (soft delete + audit) and get global
`IsDeleted` query filters. Enums stored as strings via `UseStringForEnums`.

## Application Flow

- Stage 1 (this story slice): schema only — entities, DbContext config,
  UnitOfWork repositories, EF CLI migration. No behavior change.
- Later stages: theme DTO extension, sanitizer, media endpoints, section
  endpoints, snapshot publication, startup backfill.

## Interface Contract

Unchanged in stage 1. Later: `POST/GET/DELETE /api/portfolios/me/media`,
`POST/PUT/DELETE /api/portfolios/me/sections`,
`PUT /api/portfolios/me/sections/reorder`, extended theme/item bodies,
snapshot-backed `GET /api/portfolios/by-subdomain/{subdomain}`.

## Data Model

- New tables: `PortfolioSections`, `PortfolioMediaAssets`,
  `PortfolioMediaPlacements`.
- New columns on `Portfolios`: `AvatarUrl`, `CoverImageUrl`,
  `LastPublishedAt`, `HasUnpublishedChanges`, `PublishedSnapshot` (jsonb).
- New columns on `PortfolioCustomItems`: `AccentColor`, `IsFeatured`, `Span`.
- Delete behavior: portfolio → sections/media cascade;
  placement → media asset restrict (must detach placements first);
  placement → item/section cascade.
- Indexes: `(PortfolioId, DisplayOrder)` on sections;
  `(PortfolioId)` on media assets; filtered unique
  `(PortfolioId, Kind)` on built-in section kinds;
  `(PortfolioMediaAssetId)`, `(PortfolioCustomItemId)`,
  `(PortfolioSectionId)` on placements.
- Migration: CLI-generated `AddPortfolioSectionsMediaAndPublishing`.
  Backfill of built-in sections happens in startup code (idempotent), not in
  hand-written migration SQL.

## UI / Platform Impact

Backend only; Next.js frontend consumes new fields later.

## Observability

Existing `GlobalExceptionMiddleware` logging; service-level `ILogger`
information logs for uploads, section mutations, publish/unpublish.

## Alternatives Considered

1. Reuse `MediaAsset` for portfolio media — rejected: activity-coupled,
   triggers face/video pipelines, different ownership semantics.
2. Normalized snapshot tables — rejected in favor of immutable JSONB snapshot
   (simpler, atomic, matches read-only serving need).
3. Embedding media JSON arrays on items/sections — rejected: no referential
   integrity to owned uploads.
