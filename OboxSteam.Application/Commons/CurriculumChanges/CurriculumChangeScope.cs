namespace OboxSteam.Application.Commons.CurriculumChanges;

/// <summary>
/// Suppresses curriculum change recording for the current async flow (seed and bulk
/// maintenance). Suppressed saves do not bump <c>CurriculumVersion</c>, write change rows,
/// enforce the Active lock, or auto-revoke approvals.
/// </summary>
public static class CurriculumChangeScope
{
    private static readonly AsyncLocal<int> Depth = new();

    public static bool IsSuppressed => Depth.Value > 0;

    public static IDisposable Suppress()
    {
        Depth.Value++;
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
            Depth.Value--;
        }
    }
}
