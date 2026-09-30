using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OboxSteam.Application.Commons.CurriculumChanges;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Hands pending curriculum entity changes to <see cref="ICurriculumChangeRecorder"/> before
/// each save so change rows, the version bump, and approval auto-revoke commit with the edit.
/// The recorder is resolved lazily from the request scope because it depends on the
/// unit of work, which depends on this context. Queued notifications are dropped when the
/// save fails; the unit of work publishes them after a committed save.
/// </summary>
public sealed class CurriculumChangeInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public CurriculumChangeInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureAsync(eventData.Context).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await CaptureAsync(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        DiscardNotifications();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardNotifications();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void DiscardNotifications()
        => _serviceProvider.GetService<ICurriculumChangeRecorder>()?.DiscardNotifications();

    private async Task CaptureAsync(DbContext? context)
    {
        if (context == null || CurriculumChangeScope.IsSuppressed)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                        && CurriculumChangeFieldCatalog.IsTracked(e.Entity.GetType()))
            .Select(ToEntryChange)
            .ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var recorder = _serviceProvider.GetRequiredService<ICurriculumChangeRecorder>();
        await recorder.RecordAsync(entries);
        context.ChangeTracker.DetectChanges();
    }

    private static CurriculumEntryChange ToEntryChange(EntityEntry entry)
    {
        var state = entry.State switch
        {
            EntityState.Added => CurriculumEntryState.Added,
            EntityState.Deleted => CurriculumEntryState.Deleted,
            _ => CurriculumEntryState.Modified,
        };
        IReadOnlyDictionary<string, object?> originals = state == CurriculumEntryState.Added
            ? new Dictionary<string, object?>()
            : entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
        return new CurriculumEntryChange(entry.Entity, state, originals);
    }
}
