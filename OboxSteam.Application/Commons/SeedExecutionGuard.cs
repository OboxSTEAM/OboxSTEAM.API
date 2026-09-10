namespace OboxSteam.Application.Commons;

/// <summary>
/// Pauses leftover-fail window scans while <see cref="Services.SeedService"/> is writing
/// in-progress roster data. Hosted <c>AssignmentWindowCloseService</c> otherwise AcademicFails
/// Active seats whose required windows already ended, before seed can write blocking submissions.
/// </summary>
public static class SeedExecutionGuard
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static int _depth;

    public static bool IsSeeding => Volatile.Read(ref _depth) > 0;

    public static async Task<IDisposable> BeginAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        Interlocked.Increment(ref _depth);
        return new Scope();
    }

    /// <summary>
    /// Synchronous compatibility entry point for tests and synchronous callers.
    /// Production seed execution should use <see cref="BeginAsync"/>.
    /// </summary>
    public static IDisposable Begin()
    {
        Gate.Wait();
        Interlocked.Increment(ref _depth);
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Interlocked.Decrement(ref _depth);
            Gate.Release();
        }
    }
}
