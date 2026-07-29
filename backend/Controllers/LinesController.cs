using backend.Data;
using backend.DTOs.Plc;
using backend.Models.Plc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/lines")]
[Authorize]
public sealed class LinesController : ControllerBase
{
    private readonly PlantMonitorDbContext _dbContext;

    public LinesController(PlantMonitorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<LineConfigDto>> GetLines()
    {
        var rows = _dbContext.LineProtocolAssignments
            .AsNoTracking()
            .OrderBy(x => x.LineNumber)
            .Select(MapLine)
            .ToList();

        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public ActionResult<LineConfigDto> CreateLine([FromBody] UpsertLineConfigRequestDto request)
    {
        if (request.LineNumber <= 0)
        {
            return BadRequestProblem("Line number must be greater than 0.", "invalid_line_number");
        }

        if (_dbContext.LineProtocolAssignments.Any(x => x.LineNumber == request.LineNumber))
        {
            return ConflictProblem("A line configuration already exists for that line number.", "line_number_conflict");
        }

        var entity = new LineProtocolAssignmentEntity();
        ApplyRequest(entity, request);
        entity.LineId = request.LineNumber;
        entity.LineLifecycleState = LineLifecycleState.Draft;
        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.LineProtocolAssignments.Add(entity);
        _dbContext.SaveChanges();

        return CreatedAtAction(nameof(GetLines), MapLine(entity));
    }

    [HttpPut("{lineId:int}")]
    [Authorize(Policy = "AdminOnly")]
    public ActionResult<LineConfigDto> UpdateLine(int lineId, [FromBody] UpsertLineConfigRequestDto request)
    {
        var entity = _dbContext.LineProtocolAssignments.SingleOrDefault(x => x.LineId == lineId);
        if (entity is null)
        {
            return NotFoundProblem("Line configuration not found.", "line_config_not_found");
        }

        var wasActive = IsActiveState(entity.LineLifecycleState);
        var connectionChanged = !string.Equals(entity.PlcIp, request.PlcIp.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(entity.Manufacturer, request.Manufacturer.Trim(), StringComparison.OrdinalIgnoreCase)
            || entity.PollIntervalMs != request.PollIntervalMs;

        ApplyRequest(entity, request);
        entity.LineId = lineId;

        if (wasActive && connectionChanged)
        {
            entity.LineLifecycleState = LineLifecycleState.Draft;
            entity.IsActive = false;
        }

        if (!LineLifecycleState.IsKnown(entity.LineLifecycleState))
        {
            entity.LineLifecycleState = LineLifecycleState.Draft;
            entity.IsActive = false;
        }

        entity.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.SaveChanges();
        return Ok(MapLine(entity));
    }

    [HttpDelete("{lineId:int}")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult DeleteLine(int lineId)
    {
        var entity = _dbContext.LineProtocolAssignments.SingleOrDefault(x => x.LineId == lineId);
        if (entity is null)
        {
            return NotFoundProblem("Line configuration not found.", "line_config_not_found");
        }

        entity.LineLifecycleState = LineLifecycleState.Disabled;
        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.SaveChanges();

        return NoContent();
    }

    private static void ApplyRequest(LineProtocolAssignmentEntity entity, UpsertLineConfigRequestDto request)
    {
        static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        }

        entity.LineNumber = request.LineNumber;
        entity.LineName = request.LineName.Trim();
        entity.ProductId = request.ProductId.Trim();
        entity.RecipeId = NormalizeOptional(request.RecipeId);
        entity.MachineId = NormalizeOptional(request.MachineId);
        entity.OperatorName = NormalizeOptional(request.OperatorName);
        entity.PlcIp = request.PlcIp.Trim();
        entity.Manufacturer = request.Manufacturer.Trim();
        entity.PollIntervalMs = request.PollIntervalMs;
        if (!LineLifecycleState.IsKnown(entity.LineLifecycleState))
        {
            entity.LineLifecycleState = LineLifecycleState.Draft;
        }

        entity.IsActive = IsActiveState(entity.LineLifecycleState);
    }

    private static LineConfigDto MapLine(LineProtocolAssignmentEntity entity)
    {
        return new LineConfigDto
        {
            Id = entity.LineId,
            LineNumber = entity.LineNumber,
            LineName = entity.LineName,
            ProductId = entity.ProductId,
            RecipeId = entity.RecipeId,
            MachineId = entity.MachineId,
            OperatorName = entity.OperatorName,
            PlcIp = entity.PlcIp,
            Manufacturer = entity.Manufacturer,
            PollIntervalMs = entity.PollIntervalMs,
            IsActive = IsActiveState(entity.LineLifecycleState),
            LineLifecycleState = entity.LineLifecycleState,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }

    private static bool IsActiveState(string? lifecycleState)
    {
        return string.Equals(lifecycleState, LineLifecycleState.Active, StringComparison.OrdinalIgnoreCase);
    }

    private ActionResult BadRequestProblem(string detail, string code)
    {
        return Problem(
            title: "Invalid request parameters.",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });
    }

    private ActionResult ConflictProblem(string detail, string code)
    {
        return Problem(
            title: "Resource conflict.",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict,
            type: "https://httpstatuses.com/409",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });
    }

    private ActionResult NotFoundProblem(string detail, string code)
    {
        return Problem(
            title: "Resource not found.",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound,
            type: "https://httpstatuses.com/404",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });
    }
}