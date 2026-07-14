using backend.DTOs.Production;

namespace backend.Interfaces.Production;

public interface IRecipeToleranceService
{
    IReadOnlyCollection<RecipeToleranceDto> GetActiveTolerances(string? recipeId, string? productId);
}
