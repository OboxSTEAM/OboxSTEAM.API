# 0008 Mentor Self-Managed Skill Profiles

Date: 2026-07-30

## Status

Accepted

## Context

`MentorSkill` only linked a mentor to a catalog `Skill` with proficiency and
short notes. That was too thin for managers staffing classes and for students
browsing mentor expertise. A manager-verification workflow was considered, but
requiring approval for every skill would slow mentors without enough product
value in this phase.

## Decision

1. Mentors freely create, update, and delete their own skill profile rows and
   structured evidence — no manager verification, pending, or reject states.
2. Enrich `MentorSkill` with `YearsOfExperience`, `Description`, and mentor-
   controlled `IsPublic` (default public).
3. Add `MentorSkillEvidence` (title, issuer, HTTPS URL, optional issue date,
   optional credential ID) as child rows.
4. Students see only `IsPublic` skills on mentor profiles. Managers and
   SuperAdmins see all skills when reviewing mentors for assignment.
5. Skills still reference the shared `Skill` catalog — no free-text skill names
   outside the taxonomy.

## Alternatives Considered

1. Mentor submits → manager verifies before publication — rejected as too slow
   for mentors in this phase.
2. All skills always visible with no `IsPublic` — deferred; mentors need a way
   to draft or hide individual skills.
3. Evidence only as links to platform `Certificate` / `MediaAsset` — rejected for
   v1; external certificates and credentials are common for mentors.

## Consequences

Positive:

- Transparent mentor expertise on profiles without approval bottlenecks.
- Managers still see private skills for staffing.

Tradeoffs:

- Students may see unverified self-claims; trust is mentor-owned for now.
- Application layer must validate evidence URLs, years bounds, and visibility
  filtering by viewer role.

## Follow-Up

- Application services / controller APIs (create, update, visibility, filtered
  profile reads).
- Seed sample structured mentor skills and evidence.
- Unit tests for ownership, visibility filtering, and validation.
