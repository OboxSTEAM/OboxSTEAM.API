# FE handoff: Class continuity catalog (BE)

Date: 2026-09-05 (updated pricing + cancel)

## Wire

- Shared catalog DTO: `RebuyClassCatalogDto` (`context`, `checkoutAmount`, `isEligible`, `creditHint`, `classes[].moduleSessions` on Active).
- **`checkoutAmount`:** always **50% of `Program.Price`** for Active continuity and for rebuy **inside the 1-month window**; full `Price` after the window / first purchase. Do not hardcode from `RetakeFee`.
- Active (still enrolled):
  - `GET /api/module-enrollments/{moduleEnrollmentId}/continuity-classes`
  - or `POST /api/class-redelivery-requests` then `GET .../{id}/candidates`
- After Failed/Dropped: `GET /api/programs/{programId}/rebuy-classes` (409 if still Active).
- Select + pay Active: `POST .../select-class` then retake checkout / parent retake.
- Dismiss picker = close dialog only (no program withdraw).
- Cancel open request: `POST /api/class-redelivery-requests/{id}/cancel` (alias `/withdraw` obsolete). Does **not** quit the program.

## Remove from FE

- Manager waitlist page, open-remedial dialog, intensive consent.
- PendingManager assign/reject UX.
- Statuses: `PendingManager`, `AwaitingIntensiveConsent`, `PendingAutoMatch` (create always returns `AwaitingClassSelection`).
- Resolution: `RemedialClass` (legacy only).

## Breaking / product

- Continuity fee is fixed **50%**, window **1 month** (was RetakeFee field + 3 months).
- Prefer `/cancel` over request `/withdraw` (program quit remains `POST /api/program-enrollments/{id}/withdraw` → Dropped).
