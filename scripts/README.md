# Scripts

## Harness core (`harness.exe`)

Maintains the installed Harness repository protocol (core **0.1.10**). It does
**not** record intakes, stories, or traces in SQLite.

```powershell
.\scripts\bin\harness.exe --version
.\scripts\bin\harness.exe status
.\scripts\bin\harness.exe doctor
.\scripts\bin\harness.exe update --dry-run
.\scripts\bin\harness.exe update
```

Binary path: `scripts/bin/harness.exe` (gitignored). Provenance and BASE copies
live under `.harness-core/`.

Reinstall or merge from upstream:

```powershell
& ([scriptblock]::Create((irm "https://raw.githubusercontent.com/hoangnb24/repository-harness/main/scripts/install-harness.ps1"))) -Merge -Yes
```

## Coverage

```powershell
.\scripts\run-coverage.ps1
.\scripts\run-coverage.ps1 -Filter "FullyQualifiedName~ClassServiceTests"
```

Durable agent memory is Git: `docs/plans/`, `docs/decisions/`, `docs/product/`,
and tests — not SQLite.
