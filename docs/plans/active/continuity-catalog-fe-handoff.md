# FE handoff: Class continuity catalog (BE)

Date: 2026-09-05

## Wire

- Shared catalog DTO: `RebuyClassCatalogDto` (`context`, `checkoutAmount`, `isEligible`, `creditHint`, `classes[].moduleSessions` on Active).
- Active (still enrolled):
  - `GET /api/module-enrollments/{moduleEnrollmentId}/continuity-classes`
  - or `POST /api/class-redelivery-requests` then `GET .../{id}/candidates`
- After Failed/Dropped: `GET /api/programs/{programId}/rebuy-classes` (409 if still Active).
- Select + pay Active: `POST .../select-class` then retake checkout / parent retake (amount = `RetakeFee ?? Price`).
- Dismiss picker = close dialog only (no program withdraw).

## Remove from FE

- Manager waitlist page, open-remedial dialog, intensive consent.
- PendingManager assign/reject UX.
- Statuses: `PendingManager`, `AwaitingIntensiveConsent`, `PendingAutoMatch` (create always returns `AwaitingClassSelection`).

## Breaking

- `GET .../candidates` returns `RebuyClassCatalogDto`, not `List<ClassRedeliveryCandidateDto>`.
- Manager/intensive endpoints return **410 Gone**.
- Active retake charge uses `RetakeFee ?? Price` (may be lower than full `Price`).
