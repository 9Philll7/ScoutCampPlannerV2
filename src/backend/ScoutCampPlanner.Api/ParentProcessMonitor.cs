using System.Diagnostics;

internal sealed record ParentProcessMonitorOptions(int ProcessId);

internal sealed class ParentProcessMonitor(
    ParentProcessMonitorOptions options,
    IHostApplicationLifetime applicationLifetime,
    ILogger<ParentProcessMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!ParentIsRunning(options.ProcessId))
            {
                logger.LogInformation("Parent process {ParentProcessId} ended; stopping sidecar.", options.ProcessId);
                applicationLifetime.StopApplication();
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
        }
    }

    private static bool ParentIsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
