# ProgramService — Test Plan

Test plan for `OboxSteam.Application.Services.ProgramService`.

## Scope

| Method | Description |
|--------|-------------|
| `GetProgramByIdAsync` | Get program by ID (includes active modules) |
| `GetProgramByNameAsync` | Get program by name (case-insensitive) |
| `GetAllProgramAsync` | Pagination, search, filter, sort (modules loaded per page) |
| `AddProgramAsync` | Create a new program |
| `UpdateProgramAsync` | Update an existing program |
| `DeleteProgramAsync` | Soft-delete a program |

---

## Test Setup

### Approach

- **Unit tests** with mocked `IUnitOfWork` and repositories, or
- **Integration-style unit tests** with in-memory DbContext + real `GenericRepository`

> **Note:** `GetProgramByIdAsync` and `GetProgramByNameAsync` include `Modules` ordered by `ModuleOrder`. `GetAllProgramAsync` loads modules via `IUnitOfWork.Repository<Module>()` for programs on the current page only. Prefer **in-memory DbContext** so navigation and secondary module queries work correctly.

### Dependencies to mock / seed

- `IUnitOfWork.Programs`
- `IUnitOfWork.Repository<Module>()`
- `ILogger<ProgramService>`

### Assertion conventions

| Scenario | Expected behavior |
|----------|-------------------|
| Happy path | Return correct DTO, correct count, correct sort order, modules ordered by `ModuleOrder` |
| Not found (Get/Update/Delete) | Throw `NotFoundException` |
| Business rule violation (Add/Update) | Throw `ConflictException` |
| No changes on update | Return current DTO without calling `Update` / `SaveChangesAsync` |

> **Difference from CourseService:** Program/Module services throw `NotFoundException` instead of returning `null` or `false` for missing entities.

---

## Sample Data

Use the following seed data for all test cases.

### Programs

| Key | Id | Code | Name | Level | Rating | SkillsGained | Status | Price | IsDeleted | CreatedAt |
|-----|----|------|------|-------|--------|--------------|--------|-------|-----------|-----------|
| P1 | P1 | PRG-ROBOTICS-01 | Robotics Master Track | Beginner | 4.5 | robotics, coding | Active | 999 | false | T1 |
| P2 | P2 | PRG-WEBDEV-01 | Web Development Bootcamp | Intermediate | 4.8 | html, css, javascript | Active | 799 | false | T2 |
| P3 | P3 | PRG-ADV-AI | Advanced AI Program | Advanced | 4.2 | ai, machine learning | Draft | 1299 | false | T3 |
| P4_deleted | P4 | PRG-OLD-01 | Legacy Program | Beginner | 3.0 | legacy | Archived | 100 | true | T0 |

### Modules (for GetById / GetAll module inclusion tests)

Assign `Module.ProgramId` and ensure only non-deleted modules appear in responses.

| Key | Id | Code | ProgramId | Name | ModuleOrder | ModuleType | IsDeleted |
|-----|----|------|-----------|------|-------------|------------|-----------|
| MO1 | MO1 | MOD-ROB-01 | P1 | Intro Robotics | 1 | Theory | false |
| MO2 | MO2 | MOD-ROB-02 | P1 | Build a Bot | 2 | Experiential | false |
| MO3 | MO3 | MOD-WEB-01 | P2 | HTML Basics | 1 | Theory | false |
| MO4_deleted | MO4 | MOD-OLD | P2 | Old Module | 99 | Theory | true |

---

## Test Cases

### 1. GetProgramByIdAsync

#### TC-1.1 — Happy: existing active program with modules

| Step | Action |
|------|--------|
| 1 | Call `GetProgramByIdAsync(P1)` |
| 2 | Assert result is not null |
| 3 | Assert `Id`, `Code`, `Name`, `Level`, `Status` match P1 |
| 4 | Assert `Modules` contains MO1 and MO2 only (excludes MO4_deleted) |
| 5 | Assert modules ordered by `ModuleOrder` ascending (MO1, then MO2) |

#### TC-1.2 — Unhappy: program not found

| Step | Action |
|------|--------|
| 1 | Call `GetProgramByIdAsync` with a non-existent GUID |
| 2 | Assert throws `NotFoundException` |

#### TC-1.3 — Unhappy: soft-deleted program

| Step | Action |
|------|--------|
| 1 | Call `GetProgramByIdAsync(P4)` |
| 2 | Assert throws `NotFoundException` |

---

### 2. GetProgramByNameAsync

#### TC-2.1 — Happy: exact name match

| Step | Action |
|------|--------|
| 1 | Call `GetProgramByNameAsync("Robotics Master Track")` |
| 2 | Assert result matches P1 |
| 3 | Assert `Modules` populated and ordered by `ModuleOrder` |

#### TC-2.2 — Happy: case-insensitive match

| Step | Action |
|------|--------|
| 1 | Call `GetProgramByNameAsync("robotics master track")` |
| 2 | Assert result matches P1 |

#### TC-2.3 — Unhappy: name not found

| Step | Action |
|------|--------|
| 1 | Call `GetProgramByNameAsync("Non-existent Program")` |
| 2 | Assert throws `NotFoundException` |

#### TC-2.4 — Unhappy: soft-deleted program name

| Step | Action |
|------|--------|
| 1 | Call `GetProgramByNameAsync("Legacy Program")` |
| 2 | Assert throws `NotFoundException` |

#### TC-2.5 — Note: empty or whitespace name

`GetProgramByNameAsync` does not validate empty names at the service layer (unlike `CourseService`). Empty-name behavior depends on route/controller input. Do not assert `BadRequestException` here unless testing intentional future validation.

---

### 3. AddProgramAsync

#### TC-3.1 — Happy: create valid program

**Request:**

```json
{
  "code": "PRG-NEW-01",
  "name": "New Program",
  "seriesName": "Obox Track",
  "description": "Sample description",
  "level": "Beginner",
  "estimatedDuration": "3 months",
  "skillsGained": "steam, robotics",
  "thumbnailUrl": "https://example.com/thumb.png",
  "status": "Active",
  "price": 500
}
```

| Step | Action |
|------|--------|
| 1 | Call `AddProgramAsync` with the request above |
| 2 | Assert returned DTO has correct `Code`, `Name`, `Level`, `Status`, `Price` |
| 3 | Assert `Modules` is empty |
| 4 | Verify `Programs.AddAsync` called once |
| 5 | Verify `SaveChangesAsync` called once |

#### TC-3.2 — Unhappy: duplicate code (active program)

| Step | Action |
|------|--------|
| 1 | Call with `Code = "PRG-ROBOTICS-01"` (already used by P1) |
| 2 | Assert throws `ConflictException` |

#### TC-3.3 — Happy: code reused from soft-deleted program

| Step | Action |
|------|--------|
| 1 | Call with `Code = "PRG-OLD-01"` (used only by P4_deleted) |
| 2 | Assert creation succeeds (service checks `!IsDeleted`) |

#### TC-3.4 — Happy: duplicate code case-insensitive

| Step | Action |
|------|--------|
| 1 | Call with `Code = "prg-webdev-01"` |
| 2 | Assert throws `ConflictException` (conflicts with P2) |

---

### 4. UpdateProgramAsync

#### TC-4.1 — Happy: update name and description

**Request:**

```json
{
  "name": "Robotics Master Track - Updated",
  "description": "Updated description"
}
```

| Step | Action |
|------|--------|
| 1 | Call `UpdateProgramAsync(P1, request)` |
| 2 | Assert result is not null |
| 3 | Assert `Name` and `Description` are updated |
| 4 | Verify `Programs.Update` and `SaveChangesAsync` called |

#### TC-4.2 — Happy: update code to a new unique value

| Step | Action |
|------|--------|
| 1 | Call with `{ "code": "PRG-ROBOTICS-01-UPDATED" }` |
| 2 | Assert code updated successfully |

#### TC-4.3 — Happy: update level and status

| Step | Action |
|------|--------|
| 1 | Call with `{ "level": "Intermediate", "status": "Draft" }` |
| 2 | Assert `Level` and `Status` updated |

#### TC-4.4 — Happy: no changes (idempotent update)

| Step | Action |
|------|--------|
| 1 | Call with empty `ProgramUpdateDto` or values identical to current entity |
| 2 | Assert returns current program DTO |
| 3 | Verify `Programs.Update` and `SaveChangesAsync` are **not** called |

#### TC-4.5 — Unhappy: program not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `id` |
| 2 | Assert throws `NotFoundException` |

#### TC-4.6 — Unhappy: soft-deleted program

| Step | Action |
|------|--------|
| 1 | Call `UpdateProgramAsync(P4, request)` |
| 2 | Assert throws `NotFoundException` |

#### TC-4.7 — Unhappy: duplicate code

| Step | Action |
|------|--------|
| 1 | Update P1 with `{ "code": "PRG-WEBDEV-01" }` (used by P2) |
| 2 | Assert throws `ConflictException` |

#### TC-4.8 — Happy: update code to same value (case change only)

| Step | Action |
|------|--------|
| 1 | Update P1 with `{ "code": "prg-robotics-01" }` |
| 2 | Assert no `ConflictException` (same program, case-insensitive match) |

---

### 5. DeleteProgramAsync

#### TC-5.1 — Happy: soft-delete existing program

| Step | Action |
|------|--------|
| 1 | Call `DeleteProgramAsync(P1)` |
| 2 | Assert returns `true` |
| 3 | Verify `Programs.SoftRemove` called once |
| 4 | Verify `SaveChangesAsync` called once |

#### TC-5.2 — Unhappy: program not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `id` |
| 2 | Assert throws `NotFoundException` |

#### TC-5.3 — Unhappy: already soft-deleted program

| Step | Action |
|------|--------|
| 1 | Call `DeleteProgramAsync(P4)` |
| 2 | Assert throws `NotFoundException` |

---

### 6. GetAllProgramAsync

#### TC-6.1 — Happy: baseline pagination

| Step | Action |
|------|--------|
| 1 | Call with `page=1`, `pageSize=2`, no filters |
| 2 | Assert `Items.Count == 2` |
| 3 | Assert `TotalCount == 3` (excludes P4_deleted) |
| 4 | Assert each item includes only non-deleted modules for that program |

#### TC-6.2 — Happy: search by name or code

| Step | Action |
|------|--------|
| 1 | Call with `search = "robotics"` |
| 2 | Assert returns P1 only |

#### TC-6.3 — Happy: filter by code

| Step | Action |
|------|--------|
| 1 | Call with `code = "WEBDEV"` |
| 2 | Assert returns P2 only |

#### TC-6.4 — Happy: filter by level

| Step | Action |
|------|--------|
| 1 | Call with `level = Advanced` |
| 2 | Assert returns P3 only |

#### TC-6.5 — Happy: filter by minimum rating

| Step | Action |
|------|--------|
| 1 | Call with `rating = 4.5` |
| 2 | Assert returns P1 (4.5) and P2 (4.8) only (`Rating >= 4.5`) |
| 3 | Assert P3 (4.2) is excluded |

#### TC-6.6 — Happy: filter by skillsGained

| Step | Action |
|------|--------|
| 1 | Call with `skillsGained = "html"` |
| 2 | Assert returns P2 only |

#### TC-6.7 — Happy: filter by status (exact, case-insensitive)

| Step | Action |
|------|--------|
| 1 | Call with `status = "draft"` |
| 2 | Assert returns P3 only |

#### TC-6.8 — Happy: sort by name ascending

| Step | Action |
|------|--------|
| 1 | Call with `sortBy = "name"`, `isDescending = false` |
| 2 | Assert items ordered by `Name` ascending |

#### TC-6.9 — Happy: sort by rating descending

| Step | Action |
|------|--------|
| 1 | Call with `sortBy = "rating"`, `isDescending = true` |
| 2 | Assert highest rating first (P2, then P1, then P3) |

#### TC-6.10 — Happy: default sort by createdAt

| Step | Action |
|------|--------|
| 1 | Call with no `sortBy` |
| 2 | Assert default order by `CreatedAt` ascending (or descending per `isDescending`) |

#### TC-6.11 — Unhappy: no matching results

| Step | Action |
|------|--------|
| 1 | Call with `search = "zzz"` or `status = "not-exist"` |
| 2 | Assert `Items.Count == 0` and `TotalCount == 0` |

#### TC-6.12 — Unhappy: page beyond available data

| Step | Action |
|------|--------|
| 1 | Call with `page = 10`, `pageSize = 10` |
| 2 | Assert `Items.Count == 0` |
| 3 | Assert `TotalCount` still reflects total active programs |

#### TC-6.13 — Note: invalid pagination

Pagination validation (`page < 1` or `pageSize < 1`) is handled in `ProgramController`, not in `ProgramService`. Do not test invalid pagination at the service layer unless verifying current behavior intentionally.

---

## Recommended Implementation Order

| Step | Task |
|------|------|
| 1 | Create `ProgramServiceTests` + in-memory DbContext or mock setup |
| 2 | Implement `GetProgramByIdAsync` (modules inclusion, ordering) |
| 3 | Implement `DeleteProgramAsync` and `GetProgramByNameAsync` unhappy cases |
| 4 | Implement `AddProgramAsync` (Conflict, reuse deleted code) |
| 5 | Implement `UpdateProgramAsync` (duplicate code, no-op update) |
| 6 | Implement `GetAllProgramAsync` (filter, sort, pagination, per-page modules) |

---

## Test Checklist Summary

| # | Test Case | Type | Expected |
|---|-----------|------|----------|
| TC-1.1 | Get by ID — existing with modules | Happy | Returns program + modules |
| TC-1.2 | Get by ID — not found | Unhappy | `NotFoundException` |
| TC-1.3 | Get by ID — deleted | Unhappy | `NotFoundException` |
| TC-2.1 | Get by name — exact | Happy | Returns program |
| TC-2.2 | Get by name — case-insensitive | Happy | Returns program |
| TC-2.3 | Get by name — not found | Unhappy | `NotFoundException` |
| TC-2.4 | Get by name — deleted | Unhappy | `NotFoundException` |
| TC-3.1 | Add — valid | Happy | Returns new program |
| TC-3.2 | Add — duplicate code | Unhappy | `ConflictException` |
| TC-3.3 | Add — reuse deleted code | Happy | Success |
| TC-3.4 | Add — duplicate code case-insensitive | Unhappy | `ConflictException` |
| TC-4.1 | Update — name/description | Happy | Updated |
| TC-4.2 | Update — new code | Happy | Updated |
| TC-4.3 | Update — level/status | Happy | Updated |
| TC-4.4 | Update — no changes | Happy | DTO returned, no save |
| TC-4.5 | Update — not found | Unhappy | `NotFoundException` |
| TC-4.6 | Update — deleted | Unhappy | `NotFoundException` |
| TC-4.7 | Update — duplicate code | Unhappy | `ConflictException` |
| TC-4.8 | Update — same code different case | Happy | No conflict |
| TC-5.1 | Delete — existing | Happy | `true` |
| TC-5.2 | Delete — not found | Unhappy | `NotFoundException` |
| TC-5.3 | Delete — already deleted | Unhappy | `NotFoundException` |
| TC-6.1 | Get all — pagination | Happy | Correct page + modules |
| TC-6.2 | Get all — search | Happy | Filtered results |
| TC-6.3 | Get all — filter code | Happy | Filtered results |
| TC-6.4 | Get all — filter level | Happy | Filtered results |
| TC-6.5 | Get all — filter rating | Happy | Filtered results |
| TC-6.6 | Get all — filter skillsGained | Happy | Filtered results |
| TC-6.7 | Get all — filter status | Happy | Filtered results |
| TC-6.8 | Get all — sort name | Happy | Sorted |
| TC-6.9 | Get all — sort rating | Happy | Sorted |
| TC-6.10 | Get all — default sort | Happy | Sorted by createdAt |
| TC-6.11 | Get all — no match | Unhappy | Empty list |
| TC-6.12 | Get all — page overflow | Unhappy | Empty page, correct total |
