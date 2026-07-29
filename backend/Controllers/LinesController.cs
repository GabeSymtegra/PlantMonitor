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
            return BadRequest(new { message = "Line number must be greater than 0." });
        }

        if (_dbContext.LineProtocolAssignments.Any(x => x.LineNumber == request.LineNumber))
        {
            return Conflict(new { message = "A line configuration already exists for that line number." });
        }

        var entity = new LineProtocolAssignmentEntity();
        ApplyRequest(entity, request);
        entity.LineId = request.LineNumber;
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
            return NotFound(new { message = "Line configuration not found." });
        }

        ApplyRequest(entity, request);
        entity.LineId = lineId;
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
            return NotFound(new { message = "Line configuration not found." });
        }

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
        entity.IsActive = request.IsActive;
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
            IsActive = entity.IsActive,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }
}