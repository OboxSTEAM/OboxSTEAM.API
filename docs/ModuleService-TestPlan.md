# ModuleService — Test Plan

Test plan for `OboxSteam.Application.Services.ModuleService`.

## Scope

| Method | Description |
|--------|-------------|
| `GetModuleByIdAsync` | Get module by ID |
| `GetModuleByNameAsync` | Get module by name (case-insensitive) |
| `GetAllModulesAsync` | Pagination, search, filter, sort |
| `AddModuleAsync` | Create a new module |
| `UpdateModuleAsync` | Update an existing module |
| `DeleteModuleAsync` | Soft-delete a module |

---

## Test Setup

### Approach

- **Unit tests** with mocked `IUnitOfWork` and repositories, or
- **Integration-style unit tests** with in-memory DbContext + real `GenericRepository`

> **Note:** `AddModuleAsync` and `UpdateModuleAsync` validate `ProgramId` and optional `PrerequisiteModuleId` (same program, not deleted). Prefer **in-memory DbContext** for realistic `FirstOrDefaultAsync` / `GetByIdAsync` behavior.

### Dependencies to mock / seed

- `IUnitOfWork.Modules`
- `IUnitOfWork.Programs`
- `ILogger<ModuleService>`

### Assertion conventions

| Scenario | Expected behavior |
|----------|-------------------|
| Happy path | Return correct DTO, correct count, correct sort order |
| Not found (Get/Update/Delete/invalid FK) | Throw `NotFoundException` |
| Business rule violation | Throw `ConflictException` or `BadRequestException` |
| No changes on update | Return current DTO without calling `Update` / `SaveChangesAsync` |

> **Difference from CourseService:** Module service throws `NotFoundException` instead of returning `null` or `false` for missing entities.

---

## Sample Data

Use the following seed data for all test cases.

### Programs (parent entities)

| Key | Id | Code | Name | IsDeleted |
|-----|----|------|------|-----------|
| programA | P1 | PRG-ROBOTICS-01 | Robotics Master Track | false |
| programB | P2 | PRG-WEBDEV-01 | Web Development Bootcamp | false |
| programC | P3 | PRG-ADV-AI | Advanced AI Program | false |
| programDeleted | P4 | PRG-OLD-01 | Legacy Program | true |

### Modules

| Key | Id | Code | ProgramId | Name | ModuleOrder | ModuleType | PrerequisiteModuleId | Price | IsDeleted | CreatedAt |
|-----|----|------|-----------|------|-------------|------------|----------------------|-------|-----------|-----------|
| M1 | M1 | MOD-ROBOTICS-01 | P1 | Basics of Robotics | 1 | Theory | — | 100 | false | T1 |
| M2 | M2 | MOD-ROBOTICS-02 | P1 | Advanced Robotics | 2 | Experiential | M1 | 150 | false | T2 |
| M3 | M3 | MOD-WEBDEV-01 | P2 | HTML & CSS Foundations | 1 | Theory | — | 80 | false | T3 |
| M4 | M4 | MOD-RESEARCH-01 | P2 | Research Project | 2 | Research | M3 | 200 | false | T4 |
| M5_other_program | M5 | MOD-AI-01 | P3 | AI Foundations | 1 | Theory | — | 120 | false | T5 |
| M6_deleted | M6 | MOD-X | P2 | Deleted Module | 99 | Theory | — | 50 | true | T0 |

---

## Test Cases

### 1. GetModuleByIdAsync

#### TC-1.1 — Happy: existing active module

| Step | Action |
|------|--------|
| 1 | Call `GetModuleByIdAsync(M1)` |
| 2 | Assert result is not null |
| 3 | Assert `Id`, `Code`, `Name`, `ProgramId`, `ModuleType`, `ModuleOrder` match M1 |

#### TC-1.2 — Unhappy: module not found

| Step | Action |
|------|--------|
| 1 | Call `GetModuleByIdAsync` with a non-existent GUID |
| 2 | Assert throws `NotFoundException` |

#### TC-1.3 — Unhappy: soft-deleted module

| Step | Action |
|------|--------|
| 1 | Call `GetModuleByIdAsync(M6)` |
| 2 | Assert throws `NotFoundException` |

---

### 2. GetModuleByNameAsync

#### TC-2.1 — Happy: exact name match

| Step | Action |
|------|--------|
| 1 | Call `GetModuleByNameAsync("Basics of Robotics")` |
| 2 | Assert result matches M1 |

#### TC-2.2 — Happy: case-insensitive match

| Step | Action |
|------|--------|
| 1 | Call `GetModuleByNameAsync("basics of robotics")` |
| 2 | Assert result matches M1 |

#### TC-2.3 — Unhappy: name not found

| Step | Action |
|------|--------|
| 1 | Call `GetModuleByNameAsync("Non-existent Module")` |
| 2 | Assert throws `NotFoundException` |

#### TC-2.4 — Unhappy: soft-deleted module name

| Step | Action |
|------|--------|
| 1 | Call `GetModuleByNameAsync("Deleted Module")` |
| 2 | Assert throws `NotFoundException` |

#### TC-2.5 — Note: empty or whitespace name

`GetModuleByNameAsync` does not validate empty names at the service layer (unlike `CourseService`). Do not assert `BadRequestException` here unless testing intentional future validation.

---

### 3. AddModuleAsync

#### TC-3.1 — Happy: create valid module without prerequisite

**Request:**

```json
{
  "code": "MOD-NEW-01",
  "programId": "P1",
  "name": "New Module",
  "moduleType": "Theory",
  "moduleOrder": 3,
  "isMandatory": true,
  "price": 90,
  "retakeFee": 25
}
```

| Step | Action |
|------|--------|
| 1 | Call `AddModuleAsync` with the request above |
| 2 | Assert returned DTO has correct `Code`, `ProgramId`, `Name`, `ModuleType`, `ModuleOrder`, `Price`, `RetakeFee` |
| 3 | Verify `Modules.AddAsync` called once |
| 4 | Verify `SaveChangesAsync` called once |

#### TC-3.2 — Happy: create module with valid prerequisite (same program)

**Request:**

```json
{
  "code": "MOD-NEW-02",
  "programId": "P1",
  "name": "Capstone Robotics",
  "moduleType": "Experiential",
  "moduleOrder": 4,
  "prerequisiteModuleId": "M1",
  "price": 200,
  "retakeFee": 50
}
```

| Step | Action |
|------|--------|
| 1 | Call `AddModuleAsync` with the request above |
| 2 | Assert `PrerequisiteModuleId` is M1 |
| 3 | Assert creation succeeds |

#### TC-3.3 — Unhappy: program not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `ProgramId` |
| 2 | Assert throws `NotFoundException` |

#### TC-3.4 — Unhappy: program is soft-deleted

| Step | Action |
|------|--------|
| 1 | Call with `ProgramId = P4` |
| 2 | Assert throws `NotFoundException` |

#### TC-3.5 — Unhappy: duplicate code (active module)

| Step | Action |
|------|--------|
| 1 | Call with `Code = "MOD-ROBOTICS-01"` (already used by M1) |
| 2 | Assert throws `ConflictException` |

#### TC-3.6 — Happy: code reused from soft-deleted module

| Step | Action |
|------|--------|
| 1 | Call with `Code = "MOD-X"` (used only by M6_deleted) |
| 2 | Assert creation succeeds (service checks `!IsDeleted`) |

#### TC-3.7 — Unhappy: prerequisite not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `PrerequisiteModuleId` |
| 2 | Assert throws `NotFoundException` |

#### TC-3.8 — Unhappy: prerequisite is soft-deleted

| Step | Action |
|------|--------|
| 1 | Call with `PrerequisiteModuleId = M6` |
| 2 | Assert throws `NotFoundException` |

#### TC-3.9 — Unhappy: prerequisite belongs to different program

| Step | Action |
|------|--------|
| 1 | Call with `ProgramId = P1` and `PrerequisiteModuleId = M3` (M3 belongs to P2) |
| 2 | Assert throws `BadRequestException` with message about same program |

---

### 4. UpdateModuleAsync

#### TC-4.1 — Happy: update name and price

**Request:**

```json
{
  "name": "Basics of Robotics - Updated",
  "price": 110
}
```

| Step | Action |
|------|--------|
| 1 | Call `UpdateModuleAsync(M1, request)` |
| 2 | Assert result is not null |
| 3 | Assert `Name` and `Price` are updated |
| 4 | Verify `Modules.Update` and `SaveChangesAsync` called |

#### TC-4.2 — Happy: update code to a new unique value

| Step | Action |
|------|--------|
| 1 | Call with `{ "code": "MOD-ROBOTICS-01-UPDATED" }` |
| 2 | Assert code updated successfully |

#### TC-4.3 — Happy: update ProgramId to valid program

| Step | Action |
|------|--------|
| 1 | Call `UpdateModuleAsync(M1, { "programId": "P2" })` |
| 2 | Assert `ProgramId` updated to P2 |

#### TC-4.4 — Happy: set valid prerequisite (same program)

| Step | Action |
|------|--------|
| 1 | Call `UpdateModuleAsync(M1, { "prerequisiteModuleId": "M2" })` (both in P1) |
| 2 | Assert `PrerequisiteModuleId` is M2 |
| 3 | Verify `Modules.Update` and `SaveChangesAsync` called |

#### TC-4.4b — Happy: prerequisite unchanged (idempotent)

| Step | Action |
|------|--------|
| 1 | Call `UpdateModuleAsync(M2, { "prerequisiteModuleId": "M1" })` when M2 already has `PrerequisiteModuleId = M1` |
| 2 | Assert no error; may return without save if no other fields change |

#### TC-4.5 — Happy: no changes (idempotent update)

| Step | Action |
|------|--------|
| 1 | Call with empty `ModuleUpdateDto` or values identical to current entity |
| 2 | Assert returns current module DTO |
| 3 | Verify `Modules.Update` and `SaveChangesAsync` are **not** called |

#### TC-4.6 — Unhappy: module not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `id` |
| 2 | Assert throws `NotFoundException` |

#### TC-4.7 — Unhappy: soft-deleted module

| Step | Action |
|------|--------|
| 1 | Call `UpdateModuleAsync(M6, request)` |
| 2 | Assert throws `NotFoundException` |

#### TC-4.8 — Unhappy: module cannot be its own prerequisite

| Step | Action |
|------|--------|
| 1 | Call `UpdateModuleAsync(M1, { "prerequisiteModuleId": "M1" })` |
| 2 | Assert throws `BadRequestException` |

#### TC-4.9 — Unhappy: duplicate code

| Step | Action |
|------|--------|
| 1 | Update M1 with `{ "code": "MOD-ROBOTICS-02" }` (used by M2) |
| 2 | Assert throws `ConflictException` |

#### TC-4.10 — Unhappy: invalid ProgramId

| Step | Action |
|------|--------|
| 1 | Call with non-existent or soft-deleted `ProgramId` |
| 2 | Assert throws `NotFoundException` |

#### TC-4.11 — Unhappy: invalid prerequisite (not found or deleted)

| Step | Action |
|------|--------|
| 1 | Call with non-existent or soft-deleted `PrerequisiteModuleId` |
| 2 | Assert throws `NotFoundException` |

#### TC-4.12 — Unhappy: prerequisite from different program

| Step | Action |
|------|--------|
| 1 | Call `UpdateModuleAsync(M1, { "prerequisiteModuleId": "M3" })` (M3 is in P2, M1 in P1) |
| 2 | Assert throws `BadRequestException` |

#### TC-4.13 — Unhappy: prerequisite wrong program after ProgramId change

| Step | Action |
|------|--------|
| 1 | Call `UpdateModuleAsync(M1, { "programId": "P2", "prerequisiteModuleId": "M1" })` (M1 prereq still in P1) |
| 2 | Assert throws `BadRequestException` (prerequisite must belong to target program P2) |

---

### 5. DeleteModuleAsync

#### TC-5.1 — Happy: soft-delete existing module

| Step | Action |
|------|--------|
| 1 | Call `DeleteModuleAsync(M1)` |
| 2 | Assert returns `true` |
| 3 | Verify `Modules.SoftRemove` called once |
| 4 | Verify `SaveChangesAsync` called once |

#### TC-5.2 — Unhappy: module not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `id` |
| 2 | Assert throws `NotFoundException` |

#### TC-5.3 — Unhappy: already soft-deleted module

| Step | Action |
|------|--------|
| 1 | Call `DeleteModuleAsync(M6)` |
| 2 | Assert throws `NotFoundException` |

---

### 6. GetAllModulesAsync

#### TC-6.1 — Happy: baseline pagination

| Step | Action |
|------|--------|
| 1 | Call with `page=1`, `pageSize=2`, no filters |
| 2 | Assert `Items.Count == 2` |
| 3 | Assert `TotalCount == 5` (excludes M6_deleted) |

#### TC-6.2 — Happy: search by name or code

| Step | Action |
|------|--------|
| 1 | Call with `search = "robotics"` |
| 2 | Assert returns M1 and M2 only |

#### TC-6.3 — Happy: filter by code

| Step | Action |
|------|--------|
| 1 | Call with `code = "WEBDEV"` |
| 2 | Assert returns M3 only |

#### TC-6.4 — Happy: filter by moduleType

| Step | Action |
|------|--------|
| 1 | Call with `moduleType = Research` |
| 2 | Assert returns M4 only |

#### TC-6.5 — Happy: sort by moduleOrder ascending

| Step | Action |
|------|--------|
| 1 | Call with `sortBy = "moduleOrder"`, `isDescending = false` |
| 2 | Assert items ordered by `ModuleOrder` ascending |

#### TC-6.6 — Happy: sort by createdAt descending

| Step | Action |
|------|--------|
| 1 | Call with `sortBy = "createdAt"`, `isDescending = true` |
| 2 | Assert newest module first (M5, M4, M3, M2, M1) |

#### TC-6.7 — Happy: sort by price descending

| Step | Action |
|------|--------|
| 1 | Call with `sortBy = "price"`, `isDescending = true` |
| 2 | Assert highest `Price` first (M4, then M2, etc.) |

#### TC-6.8 — Unhappy: no matching results

| Step | Action |
|------|--------|
| 1 | Call with `search = "zzz"` or `moduleType` with no matches |
| 2 | Assert `Items.Count == 0` and `TotalCount == 0` |

#### TC-6.9 — Unhappy: page beyond available data

| Step | Action |
|------|--------|
| 1 | Call with `page = 10`, `pageSize = 10` |
| 2 | Assert `Items.Count == 0` |
| 3 | Assert `TotalCount` still reflects total active modules |

#### TC-6.10 — Note: invalid pagination

Pagination validation (`page < 1` or `pageSize < 1`) is handled in `ModuleController`, not in `ModuleService`. Do not test invalid pagination at the service layer unless verifying current behavior intentionally.

---

## Recommended Implementation Order

| Step | Task |
|------|------|
| 1 | Create `ModuleServiceTests` + in-memory DbContext or mock setup |
| 2 | Implement `GetModuleByIdAsync` and `DeleteModuleAsync` |
| 3 | Implement `AddModuleAsync` (program FK, duplicate code, prerequisite rules) |
| 4 | Implement `UpdateModuleAsync` (self-prerequisite, cross-program prereq, ProgramId change) |
| 5 | Implement `GetAllModulesAsync` (filter, sort, pagination) |
| 6 | Implement `GetModuleByNameAsync` unhappy cases |

---

## Test Checklist Summary

| # | Test Case | Type | Expected |
|---|-----------|------|----------|
| TC-1.1 | Get by ID — existing | Happy | Returns module |
| TC-1.2 | Get by ID — not found | Unhappy | `NotFoundException` |
| TC-1.3 | Get by ID — deleted | Unhappy | `NotFoundException` |
| TC-2.1 | Get by name — exact | Happy | Returns module |
| TC-2.2 | Get by name — case-insensitive | Happy | Returns module |
| TC-2.3 | Get by name — not found | Unhappy | `NotFoundException` |
| TC-2.4 | Get by name — deleted | Unhappy | `NotFoundException` |
| TC-3.1 | Add — valid, no prereq | Happy | Returns new module |
| TC-3.2 | Add — valid prereq | Happy | Returns new module |
| TC-3.3 | Add — program not found | Unhappy | `NotFoundException` |
| TC-3.4 | Add — program deleted | Unhappy | `NotFoundException` |
| TC-3.5 | Add — duplicate code | Unhappy | `ConflictException` |
| TC-3.6 | Add — reuse deleted code | Happy | Success |
| TC-3.7 | Add — prereq not found | Unhappy | `NotFoundException` |
| TC-3.8 | Add — prereq deleted | Unhappy | `NotFoundException` |
| TC-3.9 | Add — prereq wrong program | Unhappy | `BadRequestException` |
| TC-4.1 | Update — name/price | Happy | Updated |
| TC-4.2 | Update — new code | Happy | Updated |
| TC-4.3 | Update — program | Happy | Updated |
| TC-4.4 | Update — set valid prereq | Happy | Updated |
| TC-4.4b | Update — prereq unchanged | Happy | Success / no save |
| TC-4.5 | Update — no changes | Happy | DTO returned, no save |
| TC-4.6 | Update — not found | Unhappy | `NotFoundException` |
| TC-4.7 | Update — deleted | Unhappy | `NotFoundException` |
| TC-4.8 | Update — self prereq | Unhappy | `BadRequestException` |
| TC-4.9 | Update — duplicate code | Unhappy | `ConflictException` |
| TC-4.10 | Update — invalid program | Unhappy | `NotFoundException` |
| TC-4.11 | Update — invalid prereq | Unhappy | `NotFoundException` |
| TC-4.12 | Update — prereq wrong program | Unhappy | `BadRequestException` |
| TC-4.13 | Update — prereq after program move | Unhappy | `BadRequestException` |
| TC-5.1 | Delete — existing | Happy | `true` |
| TC-5.2 | Delete — not found | Unhappy | `NotFoundException` |
| TC-5.3 | Delete — already deleted | Unhappy | `NotFoundException` |
| TC-6.1 | Get all — pagination | Happy | Correct page |
| TC-6.2 | Get all — search | Happy | Filtered results |
| TC-6.3 | Get all — filter code | Happy | Filtered results |
| TC-6.4 | Get all — filter moduleType | Happy | Filtered results |
| TC-6.5 | Get all — sort moduleOrder | Happy | Sorted |
| TC-6.6 | Get all — sort createdAt | Happy | Sorted |
| TC-6.7 | Get all — sort price | Happy | Sorted |
| TC-6.8 | Get all — no match | Unhappy | Empty list |
| TC-6.9 | Get all — page overflow | Unhappy | Empty page, correct total |
