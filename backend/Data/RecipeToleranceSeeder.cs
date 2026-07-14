using backend.Models.Production;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public static class RecipeToleranceSeeder
{
    public static async Task SeedAsync(PlantMonitorDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var rows = new List<RecipeToleranceEntity>
        {
            // 1/0 profile
            Build("RCP-1-0", "1/0", "BareOd", 347.4m, 0.8m, 0.8m),
            Build("RCP-1-0", "1/0", "HotOd", 461.4m, 1.0m, 1.0m),
            Build("RCP-1-0", "1/0", "ColdOd", 459.1m, 1.0m, 1.0m),
            Build("RCP-1-0", "1/0", "BareWall", 3.4m, 0.2m, 0.2m),
            Build("RCP-1-0", "1/0", "HotWall", 3.5m, 0.2m, 0.2m),
            Build("RCP-1-0", "1/0", "ColdWall", 3.5m, 0.2m, 0.2m),

            // 250 MCM profile
            Build("RCP-250", "250 MCM", "BareOd", 539.0m, 1.2m, 1.2m),
            Build("RCP-250", "250 MCM", "HotOd", 675.0m, 1.4m, 1.4m),
            Build("RCP-250", "250 MCM", "ColdOd", 671.6m, 1.4m, 1.4m),
            Build("RCP-250", "250 MCM", "BareWall", 4.2m, 0.3m, 0.3m),
            Build("RCP-250", "250 MCM", "HotWall", 4.4m, 0.3m, 0.3m),
            Build("RCP-250", "250 MCM", "ColdWall", 4.4m, 0.3m, 0.3m),
        };

        var existingKeys = await dbContext.RecipeTolerances
            .Select(r => new { r.RecipeId, r.MeasurementType, r.Version })
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(
            existingKeys.Select(k => BuildKey(k.RecipeId, k.MeasurementType, k.Version)),
            StringComparer.OrdinalIgnoreCase);

        var rowsToInsert = rows
            .Where(r => !existingSet.Contains(BuildKey(r.RecipeId, r.MeasurementType, r.Version)))
            .ToList();

        if (rowsToInsert.Count == 0)
        {
            return;
        }

        dbContext.RecipeTolerances.AddRange(rowsToInsert);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildKey(string recipeId, string measurementType, int version)
    {
        return $"{recipeId}::{measurementType}::{version}";
    }

    private static RecipeToleranceEntity Build(
        string recipeId,
        string productId,
        string measurementType,
        decimal target,
        decimal minus,
        decimal plus)
    {
        return new RecipeToleranceEntity
        {
            RecipeId = recipeId,
            ProductId = productId,
            MeasurementType = measurementType,
            TargetValue = target,
            ToleranceMinus = minus,
            TolerancePlus = plus,
            Version = 1,
            IsActive = true,
            Source = "baseline-seed",
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
