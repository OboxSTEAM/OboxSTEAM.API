# Design

## Domain Model

```text
MentorSkill
  MentorId + SkillId (unique when not deleted)
  ProficiencyLevel (Beginner|Intermediate|Advanced|Expert)
  YearsOfExperience (0–60, default 0)
  Description? (text)
  Notes? (max 500)
  IsPublic (default true)
  Evidences[]

MentorSkillEvidence
  MentorSkillId
  Title (required)
  Issuer? 
  Url (HTTPS)
  IssuedAt?
  CredentialId?
```

No verification status, verifier, or review notes on mentor skills.

## Data Model

- Soft-delete query filter on `MentorSkillEvidence`.
- Cascade delete evidence when parent mentor skill is hard-removed by EF;
  soft-delete remains application-controlled.
- Existing `MentorSkills` rows: `IsPublic = true`, `YearsOfExperience = 0`,
  null description.

## Interface Contract

Deferred to the next story slice (CRUD + visibility APIs).

## Alternatives Considered

1. Manager verification before publish — rejected (see decision 0008).
2. Reuse `StudentSkillEvidence` FK shape — rejected; mentors need external URL
   credentials more than platform submission links.
