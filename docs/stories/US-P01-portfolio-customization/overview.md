# Overview

## Current Behavior

Portfolios support profile fields, a flat JSON theme (`templateId`, colors, font,
layout, `sectionOrder`), links, manual + auto-imported items, subdomain claim,
and a live `IsPublic` flag. `GET /api/portfolios/by-subdomain/{subdomain}`
serves the live draft directly. No portfolio-scoped media uploads, no custom
sections, no per-item styling, no HTML sanitization, and no draft/publish
separation exist.

## Target Behavior

- Theme gains deep customization fields (heading font, font scale, line height,
  density, accent color, background style/image, card style) with strict
  validation.
- Students upload portfolio-scoped images (jpg/jpeg/png, max 5 MB) to S3 via
  `POST /api/portfolios/me/media`, list and delete them. The media `type`
  contract exposes only `Image` because portfolio uploads do not support video.
- Portfolio gains `coverImageUrl` and a writable portfolio-specific
  `avatarUrl`. Items gain `mediaAssets` galleries (replace-on-update, cap 20),
  `accentColor`, `isFeatured`, and `span`.
- New `PortfolioSection` entity with built-in group kinds
  (`ProjectsGroup`, `ActivitiesGroup`, `LinksGroup` — hide/reorder only) and
  custom kinds (`RichText`, `Gallery`, `Embed` — full CRUD). Sections supersede
  `theme.sectionOrder` (kept readable for migration).
- Server-side HTML sanitization on write for summary, item description, item
  studentEditedBody, and section contentHtml.
- Publish captures an immutable JSONB snapshot; `by-subdomain` serves the
  snapshot only. `GET /me` returns the draft plus `lastPublishedAt` and
  `hasUnpublishedChanges`.

## Affected Users

- Student (owner of the portfolio).
- Anonymous visitors of the public portfolio page.

## Affected Product Docs

- `docs/product/api-conventions.md` (no envelope change; new routes)
- `docs/product/integrations.md` (S3 usage for portfolio media)

## Non-Goals

- Video uploads through the portfolio media route.
- Reusing the activity media pipeline (`/api/media`, Rekognition,
  MediaConvert) for portfolio media.
- Frontend rendering behavior.
