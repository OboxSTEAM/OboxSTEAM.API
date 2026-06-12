# Stories

Story packets turn product intent into bounded implementation and validation
work.

Epic-level contracts are tracked in the Harness durable layer (`US-E01` through
`US-E07`). See `backlog.md` for the full epic list and suggested next stories.

## Normal Story

Use `docs/templates/story.md` for normal feature work.

Suggested path:

```text
docs/stories/epics/E04-assessment/US-002-quiz-grading-tests.md
```

## High-Risk Story

Use `docs/templates/high-risk-story/` when feature intake classifies work as
high-risk (auth, payments, data migration, external providers).

Suggested path:

```text
docs/stories/epics/E01-auth-accounts/US-003-auth-role-tests/
  execplan.md
  overview.md
  design.md
  validation.md
```

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

Query current status:

```bash
scripts/bin/harness-cli query matrix
```
