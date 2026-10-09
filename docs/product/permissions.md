# Permissions and Authorization

## Role Model

`RoleType` enum (`OboxSteam.Domain.Enums`):

| Role | Description |
| --- | --- |
| Admin | Platform-wide administration |
| Manager | Curriculum and operational management |
| Mentor | Delivers courses and mentors class cohorts |
| Parent | Views and acts on behalf of linked students |
| Student | Learns, enrolls, submits work |
| Expert | Framework blueprints, advisory chat and curriculum approval, Offline co-teach |

JWT role claims must match enum names exactly (e.g. `"Student"`, `"Admin"`, `"Expert"`).

Controllers authorize with `[Authorize]` / `[Authorize(Roles = "...")]` at
class or action level. There is no global fallback policy: an action with no
attribute on a controller with no class-level attribute is anonymous. The named
policies registered in `IocContainer` (`AdminPolicy`, `ManagerPolicy`,
`MentorPolicy`, `ParentPolicy`, `StudentPolicy`) are not referenced by any
controller. Admin is not implied by Manager: routes that list only `Manager`
exclude Admin.

## Authorization Patterns

### Public (no auth)

- `POST /api/auth/register`, `login`, `send-resetlink`, `forgot-password`,
  `refresh-token`, `resend-otp`, `verify-otp`. Register accepts only the
  Student, Parent, and Mentor roles.
- Catalog reads: `GET` on programs (list, `with-modules`, `{id}`,
  `{id}/curriculum`, `{id}/open-classes`, `name/{name}`), modules, courses,
  activities, assignments (list, `{id}`), question banks, research milestones,
  classes (list, `{id}`, `with-sessions/{classId}`), class sessions (list,
  `{id}`), program reviews (list), experts (list, `{id}`, `{id}/profile`),
  materials (list).
- `[AllowAnonymous]`: `GET /api/certificates/verify/{code}`,
  `GET /api/materials/activity/{activityId}` (role-aware when a token is
  present), `POST /api/parent/magic-login`, `POST /api/payments/parent-checkout`
  (payment-request token), `POST /api/payments/stripe-webhook`
  (Stripe signature), `PATCH /api/payments/{id}/cancel`,
  `GET /api/portfolios/by-subdomain/{subdomain}`.
- `POST /api/webhooks/aws` (SNS signature verified in the service).
- `POST /api/materials/upload`, `PUT /api/materials/{id}`,
  `DELETE /api/materials/{id}` carry no auth attribute (see Security Notes).
- `/api/seed` (see Security Notes).

### Authenticated (any role)

- `POST /api/auth/logout`
- `/api/account/*` (`me`, `PUT me`, `me/avatar`, `GET {userId}`)
- `/api/notifications/*` (own inbox only) and the SignalR hub
  `/hubs/notifications`
- `/api/invoices/*` (`my`, `{id}`, `by-payment/{paymentId}`)
- `GET /api/payments/{id}`
- `/api/media/*` base routes: `upload`, list, `{mediaId}`, `{mediaId}/progress`,
  `class-session/{id}`, `{mediaId}/process-tags`, `DELETE {mediaId}`. List
  endpoints are role-scoped in `MediaService`; Expert and other roles get 403
  on `GET /api/media`.

### Manager only

- `POST /api/skills` — add a catalog skill (`GET /api/skills` is
  Mentor, Manager, Admin).
- `DELETE /api/classes/{id}`.
- `PUT /api/class-enrollments/manager-transfer/{id}`.

Admin is not included on these routes.

### Manager / Admin

Create, update, delete for:

- Programs (including thumbnail), modules, courses, activities, classes
  (create, update, ready-for-mentor, open, start, complete), class sessions
  (create, generate, delete).
- Assignments (create, delete), question banks (create, delete, import,
  delete question), research milestones (create, update, delete).
- Program advisory and approval: `PUT /api/programs/{id}/advisor`,
  `POST /api/programs/{id}/approval/request`, `POST /api/programs/{id}/publish`,
  `POST /api/programs/{id}/framework-version`.
- Experts: create, update, delete, avatar, degrees, publications, and program
  board links (`POST|PUT|DELETE /api/experts/{expertId}/programs/{programId}`).
- Mentors: list, create, `PUT /api/mentors/{id}/class-limit`.
- Class mentor requests: list, approve, reject. Co-teach: invite and withdraw.
- `GET /api/dashboard/*`.
- `POST /api/materials/from-discussion-attachment`.
- Media tag management (`POST/PATCH/DELETE /api/media/{id}/tags...`), shared
  with Mentor.
- `GET /api/media` returns all media in any processing state (optional class
  and student filters).

Manager/Admin also read enrollments, certificates, portfolios (`GET me`),
dashboards, and progress through routes shared with Student and Parent.

### Student

- Program purchase and class selection: `rebuy-classes`, `select-class`,
  `release-class-hold`, payments `checkout`, `checkout/retake`,
  `request-parent`, `request-parent/retake`.
- Enrollment actions: program `withdraw`, `curriculum-mind-map`, activity
  `checkpoint` and `complete`, activity progress `POST` / `done`, class
  enrollment create and transfer, module `continuity-classes`.
- Quiz attempts, file and research submissions, retrospectives.
- Assessment recovery requests (create, `me`, withdraw), class redelivery
  `select-class`.
- QR / code check-in (`checkin-by-token`, `{id}/checkin`), `GET /api/me/schedule`,
  class galleries (`class/{classId}/gallery`, `my-gallery`).
- Portfolios (all `me` routes and `subdomain-available`).
- Program reviews (own reviews).
- Parent link requests (student-initiated).
- Media: own face-tagged ready media only (`GET /api/media`, class-session media filtered).

### Parent

- View linked student enrollments and progress (shared endpoints with Student,
  Admin, Manager).
- Parent-specific endpoints under `/api/parent/*`: `complete-profile`,
  `approve-link`, `links` (shared with Student),
  `children/{studentId}/progression`,
  `children/{studentId}/enrollments/{enrollmentId}/progression`.
- `GET /api/schedules/weekly` (shared with Student).
- Media: ready media tagged for verified linked students (`studentId` required on
  `GET /api/media`).

### Mentor

- Assigned via `Course.MentorId` and `Class.MentorId`; mentor-scoped behavior
  is enforced in services, not only at controller level.
- Mentor-only routes: `GET /api/classes/mentor-board`, class mentor requests
  (`board`, create, delete, `mine`), class quiz sets (`pull`, edit question),
  `/api/mentors/me/*`.
- Shared with Manager (no Admin): `POST /api/activity-progresses/force-complete`
  and `mentor-complete-bulk`.
- Shared with Manager/Admin: assignment update (mentors may change only Title
  and Description of assignments they own), assignment submissions list,
  grading (file, research), session attendance update, session update (a
  mentor may update only `AssignmentWindow` sessions of their own class, and
  may not change kind, title, location, links, attendance flags, or status), check-in token, session evidence, assessment
  recovery `pending` / approve / reject, research milestone activity links,
  `GET /api/skills`.
- Class curriculum progress rollup: `GET /api/classes/{classId}/curriculum-progress`
  (assigned mentor only). Returns activity Done/InProgress and assignment
  submitted/graded aggregates over active enrollments — no roster PII.
- Class student progress (detail pane):
  `GET /api/classes/{classId}/activities/{activityId}/student-progress` and
  `GET /api/classes/{classId}/assignments/{assignmentId}/student-progress`
  (assigned mentor only). Roster-complete rows with identity + progress /
  latest attempt.
- Media review: list media for mentored class activities; add/remove/verify
  student tags on that media (`Mentor,Manager,Admin` on tag mutation routes).
  AI tags start with `IsVerified = false`; manually added tags start verified.
- Mentor skill profile: freely create, update, delete, and set `IsPublic` on
  own `MentorSkill` rows and evidence (no manager verification). See
  `docs/product/mentor-skills.md`.
- Manager/Admin provision mentors via `POST /api/mentors` (email + full name;
  optional phone). Temporary password is auto-generated and emailed; creation
  rolls back if email delivery fails after retries. Mentors complete profile
  and skills themselves after login.

### Expert

- Dedicated login role (`"Expert"` JWT claim). Accounts are provisioned by
  Manager/Admin via `POST /api/experts` (code, email, and full name required;
  temporary password auto-generated and emailed). Creation rolls back if the
  credentials email fails after retries. The expert can log in immediately
  (`IsEmailVerified = true`). Public `POST /api/auth/register` does not allow
  Expert. Password reset uses the existing `POST /api/auth/forgot-password`
  OTP flow — OTP is not sent at provisioning.
- Updating an expert does not change login credentials. Deleting an expert
  locks the linked user (`AccountStatus.Locked`). Self-service profile routes
  are `/api/experts/me/*` (profile, avatar, degrees, publications).
- Intended surfaces: program framework blueprints, the advisory chat and
  curriculum approval, and Offline co-teach invitations.
  Framework APIs: `/api/program-frameworks` (class-level
  `Expert,Manager,Admin`). Experts list and read their own frameworks;
  Manager/Admin may list and read all, not write. Create, update, delete,
  archive, create draft version, and publish version are owning-Expert only.
  Update edits the current draft version (409 when none exists); delete
  archives the framework. Rubric and criteria routes return 410
  `ENDPOINT_REMOVED`.
  Curriculum approval: the program advisor approves
  (`POST /api/programs/{id}/approval`) and may revoke
  (`POST /api/programs/{id}/approval/revoke`). Manager/Admin request approval,
  reopen (revoke), assign the advisor, upgrade the framework version, and
  publish. Curriculum change log: `GET /api/programs/{id}/curriculum/changes`
  and `POST .../seen` (`Expert,Manager,Admin`).
  Offline co-teach:
  `POST|GET /api/class-session-experts` (Student `GET` is Accepted-only for a
  class they are actively enrolled in; feedback fields are omitted),
  Expert `GET /mine`,
  `POST /{id}/accept|decline`,
  Manager/Admin `POST /{id}/withdraw` (Invited only). `GET /{id}` is
  Admin, Manager, Expert. Removing the expert from
  `ProgramBoard` also unlinks their Invited and Accepted co-teach rows on that
  program. Owning Expert
  `PUT /{id}/feedback` after the session is Completed and its start is not in the future (Accepted only).
  Mentor, Manager, and Admin read `coTeachFeedback` / `coTeachFeedbacks` on
  `GET /api/classes/{classId}/sessions/with-students/{sessionId}`. Students
  receive public `coTeach` / `coTeaches` cards only — never feedback text or
  rating.

### Removed endpoints (410 `ENDPOINT_REMOVED`)

These routes are still mapped (with their old role attributes) but always
return 410:

- `ProgramController`: `review-queue`, `{id}/curriculum-reviews`,
  `submit-review`, `withdraw-review`, `approve-review`, `request-changes`,
  `advisory/timeline`, `advisory/board`, all `advisory-threads/*` routes,
  `advisory-read`, `advisory-references` (POST and GET),
  `advisory-anchor-fields`, all `review-submissions/*` routes.
- `ProgramFrameworkController`: `PUT versions/{versionId}/rubric`,
  `POST|PUT|DELETE criteria`.
- `ClassRedeliveryRequestController`: `accept-intensive`, `decline-intensive`,
  `pending-manager`, `assign-target`, `reject`.
- `ManagerRedeliveryController`: `waitlist`, `open-remedial-class`.

### Expert advisory board permissions

- Participants are Manager/Admin, the program advisor
  (`Program.AdvisorExpertId`), and board experts (`ProgramBoard`)
  (`AdvisoryParticipantAccess`). Advisory routes on `ProgramController` are
  `Expert,Manager,Admin` at controller level; participation is checked in the
  service.
- All participants may post, read, and attach files in the advisory chat.
  Only the author may edit or delete their own message.
- Only the program advisor may pin, unpin, reopen, or resolve a pin. Only a
  Manager/Admin participant may mark a pin addressed. Board experts cannot
  manage pins.
- Only the advisor approves. The advisor or a Manager/Admin may revoke an
  approval.
- Experts read activity material only for programs they participate in (403
  otherwise).
- Advisory users never mutate curriculum through these endpoints. Curriculum
  mutations are Manager/Admin routes (Mentor has the limited assignment and
  research-milestone link edits listed above), blocked by
  `CurriculumEditGuard` only while a live cohort exists.

### Mentor skill visibility

- Mentor (own): all skills.
- Manager / Admin: all skills on a mentor (staffing).
- Student (mentor profile by id): only `IsPublic` skills.

## Parent–Student Linking

`ParentStudent` entity models the relationship; only `IsVerified` links count
for parent access and notifications. Flow in `ParentService`
(`ParentController`):

- Student `POST /api/parent/request-link` with the parent's email. A student
  may have at most two parent links. When no Parent account exists, a
  passwordless Parent account is created. An unverified link is stored and the
  parent receives `ParentLinkRequested`.
- A parent without a password receives a magic-link email
  (`POST /api/parent/magic-login`, anonymous, then
  `POST /api/parent/complete-profile` as Parent to set the profile and verify
  the link). A parent with a password receives an approve-link email
  (`POST /api/parent/approve-link` as Parent).
- On verification the parent receives `ParentLinkVerified` and the student
  receives `ParentLinkApproved`.
- `GET /api/parent/links` lists links for the calling Student or Parent.

## Claims Access

`IClaimsService` (`ClaimsService`) reads the current request's
`HttpContext`. It exposes only `GetCurrentUserId` (parsed from
`ClaimTypes.NameIdentifier`; `Guid.Empty` when missing or invalid) and
`IpAddress`. It does not expose the role: services that branch on role load the
`User` row and check `User.Role`.

## Security Notes

- Auth, OTP, and refresh-token flows are high-risk areas; changes require
  decision records and stronger validation proof.
- `SeedController` (`/api/seed`) has no auth attribute: `POST all` and
  `DELETE clear` are anonymous. It should be disabled or protected in
  production deployments (verify deployment configuration outside this repo).
- `POST /api/materials/upload`, `PUT /api/materials/{id}`, and
  `DELETE /api/materials/{id}` have no auth attribute and `MaterialService`
  performs no role check on them.
- `POST /api/media/upload`, `POST /api/media/{id}/process-tags`, and
  `DELETE /api/media/{id}` require authentication only; `MediaService` performs
  no role check on them.
- `PATCH /api/payments/{id}/cancel` is anonymous and keyed only by payment id.
- `GET /api/account/{userId}` returns any non-deleted user's profile to any
  authenticated caller.
- The SignalR access token is accepted from the `access_token` query string
  only for paths under `/hubs/notifications`.
