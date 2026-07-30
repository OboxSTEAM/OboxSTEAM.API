# Design

## Interface Contract

| Method | Route | Role |
| --- | --- | --- |
| GET | `/api/mentors/me/skills` | Mentor |
| POST | `/api/mentors/me/skills` | Mentor |
| PUT | `/api/mentors/me/skills/{id}` | Mentor |
| PUT | `/api/mentors/me/skills/{id}/visibility` | Mentor |
| DELETE | `/api/mentors/me/skills/{id}` | Mentor |
| GET | `/api/mentors` | Manager, SuperAdmin (all skills) |
| GET | `/api/mentors/{id}` | Student (public only); Manager/SuperAdmin (all) |

Evidence HTTPS URL, years 0–60, and non-future `IssuedAt` validated in
`MentorSkillValidator`. Update with `Evidences != null` replaces all evidence.

## Application Flow

`MentorController` → `MentorService` → `IUnitOfWork` (`MentorSkills`,
`MentorSkillEvidences`). Manual DTO mapping; no AutoMapper.
