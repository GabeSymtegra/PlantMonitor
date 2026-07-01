using Microsoft.AspNetCore.SignalR;

namespace backend.Services.Line;

public class LineUpdateBroadcastService : BackgroundService
{
    private readonly IHubContext<LinesHub> _hubContext;
    private readonly ILogger<LineUpdateBroadcastService> _logger;

    public LineUpdateBroadcastService(
        IHubContext<LinesHub> hubContext,
        ILogger<LineUpdateBroadcastService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _hubContext.Clients.All.SendAsync(
                    "SnapshotRefreshRequired",
                    new
                    {
                        reason = "mock-tick",
                        timestampUtc = DateTime.UtcNow,
                    },
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Line update broadcaster is stopping.");
        }
    }
}
