using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class PlcProtocolAdminController : ControllerBase
{
    private readonly IPlcProtocolConfigService _protocolConfigService;
    private readonly IPlcConnectionService _connectionService;

    public PlcProtocolAdminController(
        IPlcProtocolConfigService protocolConfigService,
        IPlcConnectionService connectionService)
    {
        _protocolConfigService = protocolConfigService;
        _connectionService = connectionService;
    }

    [HttpGet("plc/presets")]
    public ActionResult<IReadOnlyCollection<PlcPresetDto>> GetPresets([FromQuery] string? manufacturer)
    {
        var presets = _protocolConfigService.GetPresets(manufacturer);
        return Ok(presets);
    }

    [HttpGet("lines/{lineId:int}/protocol-assignment")]
    public ActionResult<LineProtocolAssignmentDto> GetLineProtocolAssignment(int lineId)
    {
        var assignment = _protocolConfigService.GetAssignment(lineId);

        if (assignment is null)
        {
            return NotFound(new { message = "Protocol assignment not found for line." });
        }

        return Ok(assignment);
    }

    [HttpPut("lines/{lineId:int}/protocol-assignment")]
    public IActionResult UpsertLineProtocolAssignment(int lineId, [FromBody] UpdateLineProtocolAssignmentRequestDto request)
    {
        if (!_protocolConfigService.TryUpsertAssignment(lineId, request, out var error))
        {
            return BadRequest(new { message = error });
        }

        var assignment = _protocolConfigService.GetAssignment(lineId);
        return Ok(assignment);
    }

    [HttpGet("lines/{lineId:int}/effective-tags")]
    public ActionResult<IReadOnlyCollection<EffectiveTagMappingDto>> GetEffectiveTags(int lineId)
    {
        var tags = _protocolConfigService.GetEffectiveTags(lineId, out var error);

        if (tags is null)
        {
            return NotFound(new { message = error });
        }

        return Ok(tags);
    }

    [HttpPost("lines/{lineId:int}/validate-tags")]
    public ActionResult<TagValidationResponseDto> ValidateTags(int lineId, [FromBody] ValidateTagsRequestDto request)
    {
        var result = _protocolConfigService.ValidateTags(request);

        if (!result.IsValid)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPut("lines/{lineId:int}/tag-overrides")]
    public ActionResult<IReadOnlyCollection<EffectiveTagMappingDto>> UpsertTagOverrides(int lineId, [FromBody] UpdateLineTagOverridesRequestDto request)
    {
        if (!_protocolConfigService.TryUpsertOverrides(lineId, request, out var effectiveTags, out var error))
        {
            return BadRequest(new { message = error });
        }

        return Ok(effectiveTags);
    }

    [HttpPost("plc/test-connection")]
    public async Task<ActionResult<PlcConnectionResult>> TestConnection([FromBody] PlcConnectionRequest request, CancellationToken cancellationToken)
    {
        var result = await _connectionService.TestConnectionAsync(request, cancellationToken);

        return Ok(result);
    }
}
