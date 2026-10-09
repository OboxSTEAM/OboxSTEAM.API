# Mentor Skills

## Summary

Mentors publish structured expertise against the shared STEAM `Skill` catalog.
Managers and Admins see the full skill set when staffing classes. Students see
public skills on mentor profiles so they know what each mentor does.

## Catalog link

Each `MentorSkill` references one non-deleted catalog `Skill` (`Code`, `Name`,
`Category`, optional `Subcategory`); responses embed it as a skill summary.
Mentors do not invent free-text skill names outside the catalog.

## Mentor skill snapshot (`MentorSkill`)

One active row per `(MentorId, SkillId)` (soft-delete filtered unique index);
adding a skill already on the profile returns 409.

| Field | Meaning |
| --- | --- |
| `ProficiencyLevel` | Beginner, Intermediate, Advanced, Expert (default Beginner) |
| `YearsOfExperience` | Integer 0–60 |
| `Description` | What the mentor actually does with this skill (max 4000) |
| `Notes` | Optional short note (max 500) |
| `IsPublic` | Mentor-controlled; default `true` |

Mentors own create, update, delete, and visibility; a mentor can only change
their own rows (403 otherwise). There is **no** manager verification
workflow. Update replaces `ProficiencyLevel`, `YearsOfExperience`,
`Description`, `Notes`, and `IsPublic` with the request values. Delete
soft-deletes the skill and its evidence.

## Evidence (`MentorSkillEvidence`)

Structured evidence entries linked to a `MentorSkill` (at most 20 per skill):

| Field | Meaning |
| --- | --- |
| `Title` | Credential or artifact name (required, max 255) |
| `Issuer` | Optional issuing organization (max 255) |
| `Url` | Absolute HTTPS link to proof (required, max 2000) |
| `IssuedAt` | Optional issue date (not in the future) |
| `CredentialId` | Optional external certificate / credential id (max 100) |

Evidence can be sent on create. On update, a non-null `evidences` list
replaces all evidence rows (an empty list clears them); null leaves evidence
unchanged.

## Endpoints (`MentorController`, `/api/mentors`)

| Route | Roles | Behavior |
| --- | --- | --- |
| `GET me/skills` | Mentor | All own skills (public and private) |
| `POST me/skills` | Mentor | Add a skill (optional evidence, `isPublic`); returns 201 |
| `PUT me/skills/{id}` | Mentor | Update fields; optional evidence replacement |
| `PUT me/skills/{id}/visibility` | Mentor | Set `isPublic` only |
| `DELETE me/skills/{id}` | Mentor | Remove a skill and its evidence |
| `GET me/profile` | Mentor | Own profile with all skills |
| `GET` | Admin, Manager | Paged mentors with all skills |
| `GET {id}` | Admin, Manager, Student | Mentor profile; skill visibility per table below |

## Visibility

| Viewer | Skills returned |
| --- | --- |
| Mentor (`me/profile`, `me/skills`) | All owned skills |
| Manager / Admin (`GET /api/mentors`, `GET /api/mentors/{id}`) | All skills on that mentor |
| Student (`GET /api/mentors/{id}`) | Only `IsPublic == true` |

Other roles cannot read mentor profiles by id. Visible skills are returned
with all their fields, including `Notes` and evidence.

## Class skill matching (`ClassSkill`)

Classes carry optional required/desired skill tags (`requiredSkillIds` on
class create/update; one active row per `(ClassId, SkillId)`). They are an
informational signal, not a gate on who may apply. On the mentor board
(`GET /api/class-mentor-requests/board`), each class reports
`matchesMySkills`, and `matchMySkills=true` limits results to classes sharing
at least one skill with the mentor's skills, public or private.

## Out of scope (current slice)

- Manager approve / reject / review notes
- Free-text skills outside the catalog
- Automatic LLM assessment of mentor skills
