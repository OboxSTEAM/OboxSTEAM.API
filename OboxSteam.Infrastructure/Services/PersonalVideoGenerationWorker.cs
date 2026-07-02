using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Background worker that drains <see cref="IPersonalVideoQueue"/> and runs the heavy
/// clip-building + MediaConvert submission off the HTTP request thread. Each job is processed
/// in its own DI scope so it gets a fresh scoped <c>IUnitOfWork</c> and services.
/// </summary>
public class PersonalVideoGenerationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPersonalVideoQueue _queue;
    private readonly ILogger<PersonalVideoGenerationWorker> _logger;

    public PersonalVideoGenerationWorker(
        IServiceProvider serviceProvider,
        IPersonalVideoQueue queue,
        ILogger<PersonalVideoGenerationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PersonalVideoGenerationWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            PersonalVideoJob job;
            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPersonalVideoService>();
                await service.ProcessGenerationAsync(job);
            }
            catch (Exception ex)
            {
                // ProcessGenerationAsync handles its own failures (marks the record Failed);
                // this catch is a last-resort guard so one bad job never kills the worker loop.
                _logger.LogError(ex,
                    "PersonalVideoGenerationWorker: unhandled error processing ItemId={Id}",
                    job.ItemId);
            }
        }

        _logger.LogInformation("PersonalVideoGenerationWorker stopped.");
    }
}
