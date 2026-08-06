using backend.Data;
using backend.DTOs.Plc;
using backend.Models.Plc;
using backend.Interfaces.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace backend.Controllers;

[ApiController]
[Route("api/lines")]
[Authorize]
public sealed class LinesController : ControllerBase
{
    private readonly PlantMonitorDbContext _dbContext;
    private readonly IProductionRuntimeService _runtimeService;

    public LinesController(PlantMonitorDbContext dbContext, IProductionRuntimeService runtimeService)
    {
        _dbContext = dbContext;
        _runtimeService = runtimeService;
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
        var validationResult = ValidateRequest(request, null);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var normalizedIp = request.PlcIp.Trim();
        var normalizedManufacturer = NormalizeManufacturer(request.Manufacturer);

        var entity = _dbContext.LineProtocolAssignments.SingleOrDefault(x =>
            x.LineLifecycleState == LineLifecycleState.Disabled
            && (x.LineNumber == request.LineNumber
                || (x.PlcIp == normalizedIp && x.Manufacturer.ToLower() == normalizedManufacturer.ToLower())));

        var isReuse = entity is not null;
        entity ??= new LineProtocolAssignmentEntity();
        ApplyRequest(entity, request);
        entity.LineId = request.LineNumber;
        entity.LineLifecycleState = LineLifecycleState.Draft;
        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (!isReuse)
        {
            _dbContext.LineProtocolAssignments.Add(entity);
        }

        _dbContext.SaveChanges();
        _runtimeService.RefreshAssignmentsNow();

        return isReuse
            ? Ok(MapLine(entity))
            : CreatedAtAction(nameof(GetLines), MapLine(entity));
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

        var validationResult = ValidateRequest(request, lineId);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var wasActive = IsActiveState(entity.LineLifecycleState);
        var connectionChanged = !string.Equals(entity.PlcIp, request.PlcIp.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(entity.Manufacturer, request.Manufacturer.Trim(), StringComparison.OrdinalIgnoreCase)
            || entity.PollIntervalMs != request.PollIntervalMs;

        ApplyRequest(entity, request);
        entity.LineId = lineId;

        var requestedLifecycleState = NormalizeLifecycleState(request.LineLifecycleState);
        if (requestedLifecycleState is not null)
        {
            entity.LineLifecycleState = requestedLifecycleState;
        }

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
        _runtimeService.RefreshAssignmentsNow();
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

        _dbContext.LineProtocolAssignments.Remove(entity);
        _dbContext.SaveChanges();
        _runtimeService.RefreshAssignmentsNow();

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
        entity.Manufacturer = NormalizeManufacturer(request.Manufacturer);
        entity.PollIntervalMs = request.PollIntervalMs;
    }

    private static string? NormalizeLifecycleState(string? lifecycleState)
    {
        if (LineLifecycleState.IsKnown(lifecycleState))
        {
            return lifecycleState;
        }

        return null;
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

    private ActionResult? ValidateRequest(UpsertLineConfigRequestDto request, int? currentLineId)
    {
        if (request.LineNumber <= 0)
        {
            return BadRequestProblem("Line number must be greater than 0.", "invalid_line_number");
        }

        if (string.IsNullOrWhiteSpace(request.LineName))
        {
            return BadRequestProblem("Line name is required.", "missing_line_name");
        }

        if (!IsValidIpv4(request.PlcIp))
        {
            return BadRequestProblem("PLC IP must be a valid IPv4 address.", "invalid_plc_ip");
        }

        if (!IsSupportedManufacturer(request.Manufacturer))
        {
            return BadRequestProblem("Manufacturer must be AllenBradley or Siemens.", "unsupported_manufacturer");
        }

        if (request.PollIntervalMs is < 500 or > 60000)
        {
            return BadRequestProblem("Poll interval must be between 500 and 60000 milliseconds.", "invalid_poll_interval");
        }

        if (request.ProductId.Trim().Length is 0 or > 128)
        {
            return BadRequestProblem("Product must be between 1 and 128 characters.", "invalid_product_length");
        }

        if (request.RecipeId.Trim().Length is 0 or > 128)
        {
            return BadRequestProblem("Recipe ID must be between 1 and 128 characters.", "invalid_recipe_length");
        }

        if (request.MachineId.Trim().Length is 0 or > 128)
        {
            return BadRequestProblem("Machine ID must be between 1 and 128 characters.", "invalid_machine_length");
        }

        if (request.OperatorName.Trim().Length is 0 or > 128)
        {
            return BadRequestProblem("Operator must be between 1 and 128 characters.", "invalid_operator_length");
        }

        var lineNumberConflict = _dbContext.LineProtocolAssignments.Any(x =>
            x.LineLifecycleState != LineLifecycleState.Disabled
            &&
            x.LineNumber == request.LineNumber
            && (!currentLineId.HasValue || x.LineId != currentLineId.Value));

        if (lineNumberConflict)
        {
            return ConflictProblem("A line configuration already exists for that line number.", "line_number_conflict");
        }

        var normalizedIp = request.PlcIp.Trim();
        var normalizedManufacturer = NormalizeManufacturer(request.Manufacturer);
        var duplicateConnectionConflict = _dbContext.LineProtocolAssignments.Any(x =>
            x.LineLifecycleState != LineLifecycleState.Disabled
            &&
            x.PlcIp == normalizedIp
            && x.Manufacturer.ToLower() == normalizedManufacturer.ToLower()
            && (!currentLineId.HasValue || x.LineId != currentLineId.Value));

        if (duplicateConnectionConflict)
        {
            return ConflictProblem("A line configuration with the same PLC connection already exists.", "duplicate_plc_connection");
        }

        return null;
    }

    private static bool IsValidIpv4(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!IPAddress.TryParse(value.Trim(), out var address))
        {
            return false;
        }

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    private static bool IsSupportedManufacturer(string? manufacturer)
    {
        var normalized = NormalizeManufacturer(manufacturer);
        return string.Equals(normalized, "AllenBradley", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Siemens", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeManufacturer(string? manufacturer)
    {
        var normalized = manufacturer?.Trim() ?? string.Empty;
        if (string.Equals(normalized, "AB", StringComparison.OrdinalIgnoreCase))
        {
            return "AllenBradley";
        }

        if (string.Equals(normalized, "S7", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "S7-1200", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "S7-1217C", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "S7-1500", StringComparison.OrdinalIgnoreCase))
        {
            return "Siemens";
        }

        return normalized;
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