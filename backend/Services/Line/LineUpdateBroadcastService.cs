using Microsoft.AspNetCore.SignalR;
using backend.Interfaces.Production;

namespace backend.Services.Line;

public class LineUpdateBroadcastService : BackgroundService
{
    private readonly IHubContext<LinesHub> _hubContext;
    private readonly IProductionRuntimeService _runtimeService;
    private readonly ILogger<LineUpdateBroadcastService> _logger;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
    private string? _lastSignature;
    private DateTime _lastBroadcastUtc = DateTime.MinValue;

    public LineUpdateBroadcastService(
        IHubContext<LinesHub> hubContext,
        IProductionRuntimeService runtimeService,
        ILogger<LineUpdateBroadcastService> logger)
    {
        _hubContext = hubContext;
        _runtimeService = runtimeService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = DateTime.UtcNow;
                var snapshot = _runtimeService.GetDashboardSnapshot();
                var signature = BuildMeaningfulSignature(snapshot);
                var shouldBroadcast = !string.Equals(_lastSignature, signature, StringComparison.Ordinal)
                    || now - _lastBroadcastUtc >= HeartbeatInterval;

                if (!shouldBroadcast)
                {
                    continue;
                }

                await _hubContext.Clients.All.SendAsync(
                    "SnapshotRefreshRequired",
                    new
                    {
                        reason = "runtime-state-change",
                        timestampUtc = now,
                    },
                    stoppingToken);

                _lastSignature = signature;
                _lastBroadcastUtc = now;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Line update broadcaster is stopping.");
        }
    }

    private static string BuildMeaningfulSignature(DTOs.Runtime.DashboardSnapshotDto snapshot)
    {
        var rows = snapshot.Lines
            .OrderBy(line => line.Id)
            .Select(line => string.Join('|',
                line.Id,
                line.Status,
                line.ControlMode,
                line.ProductId,
                Math.Round(line.TotalLength, 2),
                Math.Round(line.TotalVariance, 3),
                Math.Round(line.AutoModeVariance, 3),
                Math.Round(line.ManualModeVariance, 3),
                Math.Round(line.PercentAutoMode, 2),
                Math.Round(line.PercentManualMode, 2)))
            .ToArray();

        return string.Join(';', rows);
    }
}
