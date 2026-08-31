namespace OboxSteam.Application.Commons;

/// <summary>
/// Pauses leftover-fail window scans while <see cref="Services.SeedService"/> is writing
/// in-progress roster data. Hosted <c>AssignmentWindowCloseService</c> otherwise AcademicFails
/// Active seats whose required windows already ended, before seed can write blocking submissions.
/// </summary>
public static class SeedExecutionGuard
{
    private static int _depth;

    public static bool IsSeeding => Volatile.Read(ref _depth) > 0;

    public static IDisposable Begin() => new Scope();

    private sealed class Scope : IDisposable
    {
        public Scope() => Interlocked.Increment(ref _depth);

        public void Dispose() => Interlocked.Decrement(ref _depth);
    }
}
