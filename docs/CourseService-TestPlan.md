# CourseService — Test Plan

Test plan for `OboxSteam.Application.Services.CourseService`.

## Scope

| Method | Description |
|--------|-------------|
| `GetAllCoursesAsync` | Pagination, search, filter, sort |
| `GetCourseByIdAsync` | Get course by ID |
| `GetCourseByNameAsync` | Get course by name (case-insensitive) |
| `CreateCourseAsync` | Create a new course |
| `UpdateCourseAsync` | Update an existing course |
| `DeleteCourseAsync` | Soft-delete a course |

---

## Test Setup

### Approach

- **Unit tests** with mocked `IUnitOfWork` and repositories, or
- **Integration-style unit tests** with in-memory DbContext + real `GenericRepository`

> **Note:** `GetAllCoursesAsync` filters by `moduleName` and `mentorName` via navigation properties (`c.Module.Name`, `c.Mentor.FullName`). Prefer **in-memory DbContext** so navigation works correctly. If using mocks, ensure `GetQueryable()` returns `IQueryable<Course>` with `Module` and `Mentor` objects assigned.

### Dependencies to mock / seed

- `IUnitOfWork.Courses`
- `IUnitOfWork.Modules`
- `IUnitOfWork.Users`
- `ILogger<CourseService>`

### Assertion conventions

| Scenario | Expected behavior |
|----------|-------------------|
| Happy path | Return correct data, correct count, correct sort order |
| Not found (Get/Update/Delete) | Return `null` or `false` |
| Business rule violation (Create/Update) | Throw `BadRequestException`, `NotFoundException`, or `ConflictException` |

---

## Sample Data

Use the following seed data for all test cases.

### Mentors (Users)

| Key | Id | Code | FullName | IsDeleted |
|-----|----|------|----------|-----------|
| mentorA | U1 | MNT-001 | John Mentor | false |
| mentorB | U2 | MNT-002 | Alice Mentor | false |
| mentorDeleted | U3 | MNT-003 | Deleted Mentor | true |

### Modules

| Key | Id | Code | Name | IsDeleted |
|-----|----|------|------|-----------|
| moduleA | M1 | MOD-ROBOTICS-01 | Basics of Robotics | false |
| moduleB | M2 | MOD-WEBDEV-01 | HTML & CSS Foundations | false |
| moduleDeleted | M3 | MOD-X | Deleted Module | true |

### Courses

Ensure `Course.Module` and `Course.Mentor` navigation properties are assigned for filter tests.

| Key | Id | Code | Name | ModuleId | MentorId | IsDeleted | CreatedAt |
|-----|----|------|------|----------|----------|-----------|-----------|
| C1 | C1 | CRS-ROBOTICS-01 | Robotics 101 - Cohort A | M1 | U1 | false | T1 |
| C2 | C2 | CRS-ROBOTICS-02 | Robotics 101 - Cohort B | M1 | U1 | false | T2 |
| C3 | C3 | CRS-WEBDEV-01 | HTML & CSS - Evening Class | M2 | U2 | false | T3 |
| C4_deleted | C4 | CRS-OLD-01 | Old Course | M2 | U2 | true | T0 |

---

## Test Cases

### 1. GetCourseByIdAsync

#### TC-1.1 — Happy: existing active course

| Step | Action |
|------|--------|
| 1 | Call `GetCourseByIdAsync(C1)` |
| 2 | Assert result is not null |
| 3 | Assert `Id`, `Code`, `Name`, `ModuleId`, `MentorId` match C1 |

#### TC-1.2 — Unhappy: course not found

| Step | Action |
|------|--------|
| 1 | Call `GetCourseByIdAsync` with a non-existent GUID |
| 2 | Assert result is `null` |

#### TC-1.3 — Unhappy: soft-deleted course

| Step | Action |
|------|--------|
| 1 | Call `GetCourseByIdAsync(C4)` |
| 2 | Assert result is `null` |

---

### 2. GetCourseByNameAsync

#### TC-2.1 — Happy: exact name match

| Step | Action |
|------|--------|
| 1 | Call `GetCourseByNameAsync("Robotics 101 - Cohort A")` |
| 2 | Assert result matches C1 |

#### TC-2.2 — Happy: case-insensitive match

| Step | Action |
|------|--------|
| 1 | Call `GetCourseByNameAsync("robotics 101 - cohort a")` |
| 2 | Assert result matches C1 |

#### TC-2.3 — Unhappy: empty or whitespace name

| Step | Action |
|------|--------|
| 1 | Call with `null`, `""`, or `"   "` |
| 2 | Assert throws `BadRequestException` |

#### TC-2.4 — Unhappy: name not found

| Step | Action |
|------|--------|
| 1 | Call `GetCourseByNameAsync("Non-existent Course")` |
| 2 | Assert result is `null` |

#### TC-2.5 — Unhappy: soft-deleted course name

| Step | Action |
|------|--------|
| 1 | Call `GetCourseByNameAsync("Old Course")` |
| 2 | Assert result is `null` |

---

### 3. CreateCourseAsync

#### TC-3.1 — Happy: create valid course

**Request:**

```json
{
  "code": "CRS-NEW-01",
  "moduleId": "M1",
  "mentorId": "U1",
  "name": "New Course",
  "description": "Sample description"
}
```

| Step | Action |
|------|--------|
| 1 | Call `CreateCourseAsync` with the request above |
| 2 | Assert returned DTO has correct `Code`, `ModuleId`, `MentorId`, `Name`, `Description` |
| 3 | Verify `Courses.AddAsync` called once |
| 4 | Verify `SaveChangesAsync` called once |

#### TC-3.2 — Unhappy: module not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `ModuleId` |
| 2 | Assert throws `NotFoundException` |

#### TC-3.3 — Unhappy: module is soft-deleted

| Step | Action |
|------|--------|
| 1 | Call with `ModuleId = M3` |
| 2 | Assert throws `NotFoundException` |

#### TC-3.4 — Unhappy: mentor not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `MentorId` |
| 2 | Assert throws `NotFoundException` |

#### TC-3.5 — Unhappy: mentor is soft-deleted

| Step | Action |
|------|--------|
| 1 | Call with `MentorId = U3` |
| 2 | Assert throws `NotFoundException` |

#### TC-3.6 — Unhappy: duplicate code (active course)

| Step | Action |
|------|--------|
| 1 | Call with `Code = "CRS-ROBOTICS-01"` (already used by C1) |
| 2 | Assert throws `ConflictException` |

#### TC-3.7 — Happy: code reused from soft-deleted course

| Step | Action |
|------|--------|
| 1 | Call with `Code = "CRS-OLD-01"` (used only by C4_deleted) |
| 2 | Assert creation succeeds (service checks `!IsDeleted`) |

---

### 4. UpdateCourseAsync

#### TC-4.1 — Happy: update name and description

**Request:**

```json
{
  "name": "Robotics 101 - Updated",
  "description": "Updated description"
}
```

| Step | Action |
|------|--------|
| 1 | Call `UpdateCourseAsync(C1, request)` |
| 2 | Assert result is not null |
| 3 | Assert `Name` and `Description` are updated |
| 4 | Verify `Courses.Update` and `SaveChangesAsync` called |

#### TC-4.2 — Happy: update code to a new unique value

| Step | Action |
|------|--------|
| 1 | Call with `{ "code": "CRS-ROBOTICS-01-UPDATED" }` |
| 2 | Assert code updated successfully |

#### TC-4.3 — Happy: update ModuleId to valid module

| Step | Action |
|------|--------|
| 1 | Call with `{ "moduleId": "M2" }` |
| 2 | Assert `ModuleId` updated to M2 |

#### TC-4.4 — Happy: update MentorId to valid mentor

| Step | Action |
|------|--------|
| 1 | Call with `{ "mentorId": "U2" }` |
| 2 | Assert `MentorId` updated to U2 |

#### TC-4.5 — Unhappy: course not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `courseId` |
| 2 | Assert result is `null` |

#### TC-4.6 — Unhappy: soft-deleted course

| Step | Action |
|------|--------|
| 1 | Call `UpdateCourseAsync(C4, request)` |
| 2 | Assert result is `null` |

#### TC-4.7 — Unhappy: duplicate code

| Step | Action |
|------|--------|
| 1 | Update C1 with `{ "code": "CRS-ROBOTICS-02" }` (used by C2) |
| 2 | Assert throws `ConflictException` |

#### TC-4.8 — Unhappy: invalid ModuleId

| Step | Action |
|------|--------|
| 1 | Call with non-existent or soft-deleted `ModuleId` |
| 2 | Assert throws `NotFoundException` |

#### TC-4.9 — Unhappy: invalid MentorId

| Step | Action |
|------|--------|
| 1 | Call with non-existent or soft-deleted `MentorId` |
| 2 | Assert throws `NotFoundException` |

---

### 5. DeleteCourseAsync

#### TC-5.1 — Happy: soft-delete existing course

| Step | Action |
|------|--------|
| 1 | Call `DeleteCourseAsync(C1)` |
| 2 | Assert returns `true` |
| 3 | Verify `Courses.SoftRemove` called once |
| 4 | Verify `SaveChangesAsync` called once |

#### TC-5.2 — Unhappy: course not found

| Step | Action |
|------|--------|
| 1 | Call with a non-existent `courseId` |
| 2 | Assert returns `false` |

#### TC-5.3 — Unhappy: already soft-deleted course

| Step | Action |
|------|--------|
| 1 | Call `DeleteCourseAsync(C4)` |
| 2 | Assert returns `false` |

---

### 6. GetAllCoursesAsync

#### TC-6.1 — Happy: baseline pagination

| Step | Action |
|------|--------|
| 1 | Call with `page=1`, `pageSize=2`, no filters |
| 2 | Assert `Items.Count == 2` |
| 3 | Assert `TotalCount == 3` (excludes C4_deleted) |

#### TC-6.2 — Happy: search by name or code

| Step | Action |
|------|--------|
| 1 | Call with `search = "robotics"` |
| 2 | Assert returns C1 and C2 only |

#### TC-6.3 — Happy: filter by code

| Step | Action |
|------|--------|
| 1 | Call with `code = "WEBDEV"` |
| 2 | Assert returns C3 only |

#### TC-6.4 — Happy: filter by moduleName

| Step | Action |
|------|--------|
| 1 | Call with `moduleName = "robotics"` |
| 2 | Assert returns C1 and C2 (module name: "Basics of Robotics") |

#### TC-6.5 — Happy: filter by mentorName

| Step | Action |
|------|--------|
| 1 | Call with `mentorName = "john"` |
| 2 | Assert returns courses of U1 (C1, C2) |

#### TC-6.6 — Happy: sort by name ascending

| Step | Action |
|------|--------|
| 1 | Call with `sortBy = "name"`, `isDescending = false` |
| 2 | Assert items ordered by `Name` ascending |

#### TC-6.7 — Happy: sort by createdAt descending

| Step | Action |
|------|--------|
| 1 | Call with `sortBy = "createdAt"`, `isDescending = true` |
| 2 | Assert newest course first (C3, C2, C1) |

#### TC-6.8 — Unhappy: no matching results

| Step | Action |
|------|--------|
| 1 | Call with `search = "zzz"` or `moduleName = "not-exist"` |
| 2 | Assert `Items.Count == 0` and `TotalCount == 0` |

#### TC-6.9 — Unhappy: page beyond available data

| Step | Action |
|------|--------|
| 1 | Call with `page = 10`, `pageSize = 10` |
| 2 | Assert `Items.Count == 0` |
| 3 | Assert `TotalCount` still reflects total active courses |

#### TC-6.10 — Note: invalid pagination

Pagination validation (`page < 1` or `pageSize < 1`) is handled in `CourseController`, not in `CourseService`. Do not test invalid pagination at the service layer unless verifying current behavior intentionally.

---

## Recommended Implementation Order

| Step | Task |
|------|------|
| 1 | Create `CourseServiceTests` + in-memory DbContext or mock setup |
| 2 | Implement CRUD tests: `GetById`, `Delete` |
| 3 | Implement `CreateCourseAsync` unhappy cases (NotFound, Conflict) |
| 4 | Implement `UpdateCourseAsync` (duplicate code, invalid Module/Mentor) |
| 5 | Implement `GetAllCoursesAsync` (filter, sort, pagination — one test per scenario) |

---

## Test Checklist Summary

| # | Test Case | Type | Expected |
|---|-----------|------|----------|
| TC-1.1 | Get by ID — existing | Happy | Returns course |
| TC-1.2 | Get by ID — not found | Unhappy | `null` |
| TC-1.3 | Get by ID — deleted | Unhappy | `null` |
| TC-2.1 | Get by name — exact | Happy | Returns course |
| TC-2.2 | Get by name — case-insensitive | Happy | Returns course |
| TC-2.3 | Get by name — empty | Unhappy | `BadRequestException` |
| TC-2.4 | Get by name — not found | Unhappy | `null` |
| TC-2.5 | Get by name — deleted | Unhappy | `null` |
| TC-3.1 | Create — valid | Happy | Returns new course |
| TC-3.2 | Create — module not found | Unhappy | `NotFoundException` |
| TC-3.3 | Create — module deleted | Unhappy | `NotFoundException` |
| TC-3.4 | Create — mentor not found | Unhappy | `NotFoundException` |
| TC-3.5 | Create — mentor deleted | Unhappy | `NotFoundException` |
| TC-3.6 | Create — duplicate code | Unhappy | `ConflictException` |
| TC-3.7 | Create — reuse deleted code | Happy | Success |
| TC-4.1 | Update — name/description | Happy | Updated |
| TC-4.2 | Update — new code | Happy | Updated |
| TC-4.3 | Update — module | Happy | Updated |
| TC-4.4 | Update — mentor | Happy | Updated |
| TC-4.5 | Update — not found | Unhappy | `null` |
| TC-4.6 | Update — deleted course | Unhappy | `null` |
| TC-4.7 | Update — duplicate code | Unhappy | `ConflictException` |
| TC-4.8 | Update — invalid module | Unhappy | `NotFoundException` |
| TC-4.9 | Update — invalid mentor | Unhappy | `NotFoundException` |
| TC-5.1 | Delete — existing | Happy | `true` |
| TC-5.2 | Delete — not found | Unhappy | `false` |
| TC-5.3 | Delete — already deleted | Unhappy | `false` |
| TC-6.1 | Get all — pagination | Happy | Correct page |
| TC-6.2 | Get all — search | Happy | Filtered results |
| TC-6.3 | Get all — filter code | Happy | Filtered results |
| TC-6.4 | Get all — filter moduleName | Happy | Filtered results |
| TC-6.5 | Get all — filter mentorName | Happy | Filtered results |
| TC-6.6 | Get all — sort name | Happy | Sorted |
| TC-6.7 | Get all — sort createdAt | Happy | Sorted |
| TC-6.8 | Get all — no match | Unhappy | Empty list |
| TC-6.9 | Get all — page overflow | Unhappy | Empty page, correct total |
