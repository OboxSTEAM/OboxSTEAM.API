# Mentor Skills

## Summary

Mentors publish structured expertise against the shared STEAM `Skill` catalog.
Managers use the full skill set when staffing classes. Students see public
skills on mentor profiles so they know what each mentor does.

## Catalog link

Each `MentorSkill` references one catalog `Skill` (`Code`, `Name`, `Category`,
optional `Subcategory`). Mentors do not invent free-text skill names outside
the catalog.

## Mentor skill snapshot (`MentorSkill`)

One active row per `(MentorId, SkillId)` (soft-delete filtered unique index):

| Field | Meaning |
| --- | --- |
| `ProficiencyLevel` | Beginner, Intermediate, Advanced, Expert |
| `YearsOfExperience` | Integer 0–60 |
| `Description` | What the mentor actually does with this skill |
| `Notes` | Optional short private/operational note |
| `IsPublic` | Mentor-controlled; default `true` |

Mentors own create, update, delete, and visibility. There is **no** manager
verification workflow.

## Evidence (`MentorSkillEvidence`)

Structured evidence entries linked to a `MentorSkill`:

| Field | Meaning |
| --- | --- |
| `Title` | Credential or artifact name |
| `Issuer` | Issuing organization |
| `Url` | HTTPS link to proof |
| `IssuedAt` | Optional issue date (not in the future) |
| `CredentialId` | Optional external certificate / credential id |

## Visibility

| Viewer | Skills returned |
| --- | --- |
| Mentor (own profile / me/skills) | All owned skills |
| Manager / SuperAdmin | All skills on that mentor |
| Student (mentor profile by id) | Only `IsPublic == true` |

## Out of scope (current slice)

- Manager approve / reject / review notes
- Free-text skills outside the catalog
- Automatic LLM assessment of mentor skills
