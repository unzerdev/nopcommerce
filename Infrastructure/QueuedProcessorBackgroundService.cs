using Microsoft.Extensions.Hosting;
using Nop.Services.Logging;

namespace Unzer.Plugin.Payments.Unzer.Infrastructure;
public class QueuedProcessorBackgroundService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceProvider _serviceProvider;    
    private readonly ILogger _logger;

    public QueuedProcessorBackgroundService(IBackgroundTaskQueue taskQueue, IServiceProvider serviceProvider, ILogger logger)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _logger.InformationAsync("Queued Processor Background Service is starting.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(cancellationToken);

            try
            {
                await workItem(_serviceProvider, cancellationToken);
            }
            catch (Exception ex)
            {
               await _logger.ErrorAsync($"Error occurred executing {nameof(workItem)}.", ex);
            }
        }

        await _logger.InformationAsync("Queued Processor Background Service is stopping.");
    }
}
