# Stories

Optional markdown packets for large features. Prefer `docs/plans/active/` for
multi-session execution memory (`docs/templates/exec-plan.md`).

Epic overview: `backlog.md`. Proof snapshot: `docs/TEST_MATRIX.md`.

## Normal Story

Use `docs/templates/story.md` when a dedicated feature packet helps reviewers.

```text
docs/stories/epics/E04-assessment/US-002-quiz-grading-tests.md
```

## High-Risk Story

Use `docs/templates/high-risk-story/` for auth, payments, migrations, or
external providers when you want a multi-file packet.

## Status Flow

```text
planned -> in_progress -> implemented
                  |
                  v
               changed
                  |
                  v
               retired
```

Update status in the story file and in `docs/TEST_MATRIX.md` when epic proof
changes. Run `dotnet test` / `dotnet build` for executable evidence.
