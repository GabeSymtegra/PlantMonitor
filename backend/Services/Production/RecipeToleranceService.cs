using backend.Data;
using backend.DTOs.Production;
using backend.Interfaces.Production;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Production;

public sealed class RecipeToleranceService : IRecipeToleranceService
{
    private readonly PlantMonitorDbContext _dbContext;

    public RecipeToleranceService(PlantMonitorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyCollection<RecipeToleranceDto> GetActiveTolerances(string? recipeId, string? productId)
    {
        var query = _dbContext.RecipeTolerances
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(recipeId))
        {
            var normalized = recipeId.Trim().ToLowerInvariant();
            query = query.Where(x => x.RecipeId.ToLower() == normalized);
        }

        if (!string.IsNullOrWhiteSpace(productId))
        {
            var normalized = productId.Trim().ToLowerInvariant();
            query = query.Where(x => x.ProductId.ToLower() == normalized);
        }

        return query
            .OrderBy(x => x.RecipeId)
            .ThenBy(x => x.MeasurementType)
            .Select(x => new RecipeToleranceDto
            {
                RecipeId = x.RecipeId,
                ProductId = x.ProductId,
                MeasurementType = x.MeasurementType,
                TargetValue = x.TargetValue,
                ToleranceMinus = x.ToleranceMinus,
                TolerancePlus = x.TolerancePlus,
                MinValue = x.TargetValue - x.ToleranceMinus,
                MaxValue = x.TargetValue + x.TolerancePlus,
            })
            .ToList();
    }
}
