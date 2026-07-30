# Overview

## Current Behavior

Mentor skill schema includes years, description, public flag, and evidence
entities. Application APIs still only expose thin proficiency + notes.

## Target Behavior (API slice)

- Mentors freely create, update, delete skills and evidence; toggle `IsPublic`.
- Students see only public skills on `GET /api/mentors/{id}`.
- Managers/SuperAdmins see all skills on list and detail.
- Seed data includes structured expertise and sample evidence.

## Affected Users

- Mentor, Manager, SuperAdmin, Student

## Affected Product Docs

- `docs/product/mentor-skills.md`
- `docs/product/permissions.md`
- `docs/decisions/0008-mentor-self-managed-skill-profiles.md`

## Non-Goals

- Manager verification workflow
- Free-text skills outside the catalog
