using backend.Data;
using backend.DTOs.Common;
using backend.DTOs.Production;
using backend.Interfaces.Production;
using backend.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Services.Production;

public sealed class ProductionReportsService : IProductionReportsService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProductionReportsService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<PagedResultDto<CompletedProductionRunDto>> GetCompletedRunsAsync(
        int? lineId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = Math.Clamp(take, 1, 500);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();

        var query = dbContext.CompletedProductionRuns
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Include(x => x.ZoneStats)
            .AsQueryable();

        if (lineId.HasValue)
        {
            query = query.Where(x => x.LineId == lineId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.EndTimeUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.EndTimeUtc <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var runs = await query
            .OrderByDescending(x => x.EndTimeUtc)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<CompletedProductionRunDto>
        {
            Items = runs.Select(MapCompletedRun).ToList(),
            TotalCount = totalCount,
            Skip = normalizedSkip,
            Take = normalizedTake,
        };
    }

    public async Task<CompletedProductionRunDto?> GetCompletedRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();

        var run = await dbContext.CompletedProductionRuns
            .AsNoTracking()
            .Include(x => x.ZoneStats)
            .Where(x => !x.IsDeleted)
            .FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);

        return run is null ? null : MapCompletedRun(run);
    }

    public async Task<bool> DeleteCompletedRunAsync(
        Guid runId,
        string deletedByUsername,
        string deletedByRole,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();

        var run = await dbContext.CompletedProductionRuns
            .Include(x => x.ZoneStats)
            .FirstOrDefaultAsync(x => x.Id == runId && !x.IsDeleted, cancellationToken);

        if (run is null)
        {
            return false;
        }

        run.IsDeleted = true;
        run.DeletedByUsername = deletedByUsername;
        run.DeletedAtUtc = DateTime.UtcNow;

        dbContext.CompletedRunDeletionAudits.Add(new CompletedRunDeletionAuditEntity
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            LineId = run.LineId,
            LineNumber = run.LineNumber,
            LineName = run.LineName,
            ProductId = run.ProductId,
            DeletedByUsername = deletedByUsername,
            DeletedByRole = deletedByRole,
            DeletedAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PagedResultDto<RuntimeEventDto>> GetRuntimeEventsAsync(
        int? lineId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = Math.Clamp(take, 1, 1000);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlantMonitorDbContext>();

        var query = dbContext.RuntimeEvents
            .AsNoTracking()
            .AsQueryable();

        if (lineId.HasValue)
        {
            query = query.Where(x => x.LineId == lineId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<RuntimeEventDto>
        {
            Items = rows.Select(x => new RuntimeEventDto
            {
                Id = x.Id,
                LineId = x.LineId,
                LineNumber = x.LineNumber,
                LineName = x.LineName,
                EventType = x.EventType,
                PreviousValue = x.PreviousValue,
                CurrentValue = x.CurrentValue,
                OccurredAtUtc = x.OccurredAtUtc,
            }).ToList(),
            TotalCount = totalCount,
            Skip = normalizedSkip,
            Take = normalizedTake,
        };
    }

    private static CompletedProductionRunDto MapCompletedRun(CompletedProductionRunEntity run)
    {
        return new CompletedProductionRunDto
        {
            Id = run.Id,
            LineId = run.LineId,
            LineNumber = run.LineNumber,
            LineName = run.LineName,
            ProductId = run.ProductId,
            RecipeId = run.RecipeId,
            MachineId = run.MachineId,
            OperatorName = run.OperatorName,
            FinalStatus = run.FinalStatus,
            StartTimeUtc = run.StartTimeUtc,
            EndTimeUtc = run.EndTimeUtc,
            RuntimeSeconds = run.RuntimeSeconds,
            ProductionLength = run.ProductionLength,
            AutoTimeSeconds = run.AutoTimeSeconds,
            ManualTimeSeconds = run.ManualTimeSeconds,
            AutoPercentage = run.AutoPercentage,
            ManualPercentage = run.ManualPercentage,
            ZoneStats = run.ZoneStats
                .OrderBy(stat => stat.Zone)
                .ThenBy(stat => stat.Segment)
                .Select(stat => new CompletedProductionRunZoneStatDto
                {
                    Zone = stat.Zone,
                    Segment = stat.Segment,
                    MeasurementCount = stat.MeasurementCount,
                    SkippedCount = stat.SkippedCount,
                    AverageAbsoluteDeviation = stat.AverageAbsoluteDeviation,
                    MaxPositiveDeviation = stat.MaxPositiveDeviation,
                    MaxNegativeDeviation = stat.MaxNegativeDeviation,
                    CurrentDeviation = stat.CurrentDeviation,
                })
                .ToList(),
        };
    }
}