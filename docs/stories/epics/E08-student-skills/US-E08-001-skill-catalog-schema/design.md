# Design

## Entities

```text
Skill
  Code (unique), Name, Category, Subcategory?, Description?

StudentSkill
  StudentId + SkillId (unique when not deleted)
  ProficiencyLevel (Beginner|Intermediate|Advanced|Expert)
  Source (Manual|Llm|Mentor|System)
  ConfidenceScore?, LastAssessedAt?
  VerifiedBy?, VerifiedAt?
  EvidenceSummary?, Reasoning?

StudentSkillEvidence
  StudentSkillId
  SubmissionId? | CertificateId? | MediaAssetId?
  (at least one FK required in application layer)
```

## Enums

- `SkillCategory`: Science, Technology, Engineering, Arts, Math, SoftSkill
- `SkillProficiencyLevel`: Beginner, Intermediate, Advanced, Expert
- `SkillAssessmentSource`: Manual, Llm, Mentor, System

## Migration notes

Legacy `StudentSkills.SkillName` / `SkillType` / integer proficiency are removed.
Table was unused by application services; no data backfill in this story.
