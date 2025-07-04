using ClaudeCLIRunner.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaudeCLIRunner.Services;

public class PollingBackgroundService : BackgroundService
{
    private readonly WorkItemOrchestrator _orchestrator;
    private readonly ClaudeCliConfig _config;
    private readonly ILogger<PollingBackgroundService> _logger;

    public PollingBackgroundService(
        WorkItemOrchestrator orchestrator,
        IOptions<ClaudeCliConfig> config,
        ILogger<PollingBackgroundService> logger)
    {
        _orchestrator = orchestrator;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Polling service started. Interval: {Interval} seconds", 
            _config.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Starting polling cycle");
                
                await _orchestrator.ProcessWorkItemsAsync(stoppingToken);
                
                _logger.LogDebug("Polling cycle completed. Waiting {Interval} seconds", 
                    _config.PollIntervalSeconds);
                
                await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                _logger.LogInformation("Polling service stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling cycle. Will retry after interval");
                
                // Wait before retrying
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Polling service stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping polling service");
        await base.StopAsync(cancellationToken);
    }
} 