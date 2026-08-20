namespace OboxSteam.Application.Realtime;

/// <summary>Well-known <see cref="SyncEvent.Scope"/> values.</summary>
public static class SyncScopes
{
    /// <summary>Module/Course/Activity/Assignment CRUD changed the curriculum tree of a program.</summary>
    public const string CurriculumStructureChanged = "curriculum.structureChanged";
}
