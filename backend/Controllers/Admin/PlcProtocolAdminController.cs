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

    [HttpPost("plc/browse-tags")]
    public async Task<ActionResult<IReadOnlyCollection<PlcTagBrowseItemDto>>> BrowseTags(
        [FromBody] PlcTagBrowseRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDriverAndIp(request.Driver, request.IpAddress, out var driver, out var errorResult))
        {
            return errorResult!;
        }

        var tags = await driver!.BrowseTagsAsync(request.IpAddress.Trim(), request.Search, cancellationToken);
        return Ok(tags);
    }

    [HttpPost("plc/read-tag")]
    public async Task<ActionResult<PlcTagReadResultDto>> ReadTag(
        [FromBody] PlcTagReadRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDriverAndIp(request.Driver, request.IpAddress, out var driver, out var errorResult))
        {
            return errorResult!;
        }

        if (string.IsNullOrWhiteSpace(request.TagName))
        {
            return BadRequest(new { message = "Tag name is required." });
        }

        var result = await driver!.ReadTagAsync(request.IpAddress.Trim(), request.TagName.Trim(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("plc/read-tags")]
    public async Task<ActionResult<IReadOnlyCollection<PlcTagReadResultDto>>> ReadTags(
        [FromBody] PlcTagReadManyRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDriverAndIp(request.Driver, request.IpAddress, out var driver, out var errorResult))
        {
            return errorResult!;
        }

        if (request.TagNames is null || request.TagNames.Count == 0)
        {
            return BadRequest(new { message = "At least one tag name is required." });
        }

        var result = await driver!.ReadTagsAsync(request.IpAddress.Trim(), request.TagNames, cancellationToken);
        return Ok(result);
    }

    [HttpPost("plc/write-tag")]
    public async Task<ActionResult<PlcTagWriteResultDto>> WriteTag(
        [FromBody] PlcTagWriteRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDriverAndIp(request.Driver, request.IpAddress, out var driver, out var errorResult))
        {
            return errorResult!;
        }

        if (string.IsNullOrWhiteSpace(request.TagName))
        {
            return BadRequest(new { message = "Tag name is required." });
        }

        var result = await driver!.WriteTagAsync(
            request.IpAddress.Trim(),
            request.TagName.Trim(),
            request.Value,
            cancellationToken);

        return Ok(result);
    }

    private bool TryResolveDriverAndIp(
        string driverName,
        string ipAddress,
        out IPlcDriver? driver,
        out ActionResult? errorResult)
    {
        driver = null;
        errorResult = null;

        if (!_connectionService.TryResolveDriver(driverName, out driver, out var driverError))
        {
            errorResult = BadRequest(new { message = driverError });
            return false;
        }

        if (!_connectionService.IsValidIpAddress(ipAddress))
        {
            errorResult = BadRequest(new { message = "Invalid IP address. Enter a valid IPv4 address." });
            return false;
        }

        return true;
    }
}
