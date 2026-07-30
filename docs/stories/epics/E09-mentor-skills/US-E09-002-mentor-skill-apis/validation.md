# Validation

## Commands

```powershell
dotnet test OboxSteam.Test/OboxSteam.Test.csproj --filter "FullyQualifiedName~MentorServiceTests"
dotnet build OboxSteam.API/OboxSteam.API.csproj
```

## Acceptance Evidence

- MentorServiceTests cover create/update/visibility/evidence/student filter.
- Build succeeds.
