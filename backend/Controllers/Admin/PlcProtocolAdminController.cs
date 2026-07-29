using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace backend.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
// Administration endpoints for PLC configuration, tag mapping, and live PLC
// diagnostics are grouped here behind the AdminOnly policy.
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

    // -------------------------------------------------------------------------
    // Presets, assignments, and tag catalog management
    // -------------------------------------------------------------------------

    [HttpGet("plc/presets")]
    public ActionResult<IReadOnlyCollection<PlcPresetDto>> GetPresets([FromQuery] string? manufacturer)
    {
        var presets = _protocolConfigService.GetPresets(manufacturer);
        return Ok(presets);
    }

    [HttpGet("plc/tag-slots")]
    public ActionResult<IReadOnlyCollection<TagSlotDefinitionDto>> GetTagSlots()
    {
        return Ok(_protocolConfigService.GetRequiredTagSlots());
    }

    [HttpGet("lines/{lineId:int}/protocol-assignment")]
    public ActionResult<LineProtocolAssignmentDto> GetLineProtocolAssignment(int lineId)
    {
        var assignment = _protocolConfigService.GetAssignment(lineId);

        if (assignment is null)
        {
            return NotFoundProblem("Protocol assignment not found for line.", "protocol_assignment_not_found");
        }

        return Ok(assignment);
    }

    [HttpGet("lines/{lineId:int}/tag-catalog")]
    public ActionResult<IReadOnlyCollection<LineTagCatalogEntryDto>> GetLineTagCatalog(int lineId)
    {
        return Ok(_protocolConfigService.GetTagCatalog(lineId));
    }

    [HttpPut("lines/{lineId:int}/tag-catalog")]
    public ActionResult<IReadOnlyCollection<LineTagCatalogEntryDto>> ReplaceLineTagCatalog(int lineId, [FromBody] UpdateLineTagCatalogRequestDto request)
    {
        if (!_protocolConfigService.TryReplaceTagCatalog(lineId, request, out var tags, out var error))
        {
            return BadRequestProblem(error ?? "Tag catalog update failed.", "invalid_tag_catalog");
        }

        return Ok(tags);
    }

    [HttpPut("lines/{lineId:int}/protocol-assignment")]
    public IActionResult UpsertLineProtocolAssignment(int lineId, [FromBody] UpdateLineProtocolAssignmentRequestDto request)
    {
        if (!_protocolConfigService.TryUpsertAssignment(lineId, request, out var error))
        {
            return BadRequestProblem(error ?? "Protocol assignment update failed.", "invalid_protocol_assignment");
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
            return NotFoundProblem(error ?? "Effective tags were not found.", "effective_tags_not_found");
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
            return BadRequestProblem(error ?? "Tag overrides update failed.", "invalid_tag_overrides");
        }

        return Ok(effectiveTags);
    }

    // -------------------------------------------------------------------------
    // PLC connectivity and live tag interaction
    // -------------------------------------------------------------------------

    [HttpPost("plc/test-connection")]
    public async Task<ActionResult<PlcConnectionResult>> TestConnection([FromBody] PlcConnectionRequest request, CancellationToken cancellationToken)
    {
        var result = await _connectionService.TestConnectionAsync(request, cancellationToken);

        if (!result.IsConnected)
        {
            if (string.Equals(result.Message, "Wrong driver selected. Choose AllenBradley.", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Message, "Invalid IP address. Enter a valid IPv4 address.", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(result);
            }

            return StatusCode(StatusCodes.Status502BadGateway, result);
        }

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

    [HttpPost("plc/auto-map-tags")]
    public async Task<ActionResult<AutoMapTagCatalogResultDto>> AutoMapTags(
        [FromBody] AutoMapTagCatalogRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDriverAndIp(request.Driver, request.IpAddress, out var driver, out var errorResult))
        {
            return errorResult!;
        }

        var discovered = await driver!.BrowseTagsAsync(request.IpAddress.Trim(), null, cancellationToken);
        var readableLeafTags = discovered
            .Where(tag => !tag.IsFolder && tag.CanRead != false)
            .ToList();

        var slotDefinitions = _protocolConfigService.GetRequiredTagSlots();
        var usedTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestions = new List<LineTagCatalogEntryDto>();
        var missing = new List<string>();

        foreach (var slot in slotDefinitions)
        {
            var match = FindBestMatch(slot.LogicalKey, readableLeafTags, usedTagNames);

            if (match is null)
            {
                missing.Add(slot.LogicalKey);
                continue;
            }

            usedTagNames.Add(match.Name);
            suggestions.Add(new LineTagCatalogEntryDto
            {
                LogicalKey = slot.LogicalKey,
                DisplayName = slot.DisplayName,
                Driver = driver.DriverName,
                PlcAddress = match.Name,
                DataType = NormalizeDataType(match.DataType),
                IsRequired = slot.IsRequired,
                IsEnabled = true,
                SortOrder = suggestions.Count,
                ReadFrequencyMs = 1000,
                Description = slot.Description,
            });
        }

        return Ok(new AutoMapTagCatalogResultDto
        {
            Driver = driver.DriverName,
            ScannedTagCount = readableLeafTags.Count,
            SuggestedMappings = suggestions,
            MissingLogicalKeys = missing,
        });
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
            return BadRequestProblem("Tag name is required.", "missing_tag_name");
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
            return BadRequestProblem("At least one tag name is required.", "missing_tag_names");
        }

        var result = await driver!.ReadTagsAsync(request.IpAddress.Trim(), request.TagNames, cancellationToken);
        return Ok(result);
    }

    [HttpGet("lines/{lineId:int}/commissioning-check")]
    public ActionResult<CommissioningReadinessDto> GetCommissioningReadiness(int lineId)
    {
        var assignment = _protocolConfigService.GetAssignment(lineId);
        if (assignment is null)
        {
            return NotFoundProblem("Protocol assignment not found for line.", "protocol_assignment_not_found");
        }

        var requiredSlots = _protocolConfigService.GetRequiredTagSlots()
            .Where(slot => slot.IsRequired)
            .ToList();

        var issues = new List<string>();
        var mappedRequiredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var effectiveTags = _protocolConfigService.GetEffectiveTags(lineId, out var effectiveTagsError);
        if (effectiveTags is null)
        {
            issues.Add(effectiveTagsError ?? "No effective tag mappings are available.");
        }
        else
        {
            foreach (var tag in effectiveTags)
            {
                if (tag.IsRequired && !string.IsNullOrWhiteSpace(tag.PlcAddress))
                {
                    mappedRequiredKeys.Add(tag.TagKey);
                }
            }
        }

        var missingRequiredKeys = requiredSlots
            .Select(slot => slot.LogicalKey)
            .Where(requiredKey => !mappedRequiredKeys.Contains(requiredKey))
            .ToList();

        if (missingRequiredKeys.Count > 0)
        {
            issues.Add($"Missing required logical keys: {string.Join(", ", missingRequiredKeys)}.");
        }

        return Ok(new CommissioningReadinessDto
        {
            LineId = lineId,
            Manufacturer = assignment.Manufacturer,
            PresetName = assignment.PresetName,
            PresetVersion = assignment.PresetVersion,
            IsReady = issues.Count == 0,
            RequiredTagCount = requiredSlots.Count,
            MappedRequiredTagCount = requiredSlots.Count - missingRequiredKeys.Count,
            MissingRequiredTagKeys = missingRequiredKeys,
            Issues = issues,
            CheckedAtUtc = DateTime.UtcNow,
        });
    }

    // -------------------------------------------------------------------------
    // Small controller helpers
    // -------------------------------------------------------------------------

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
            errorResult = BadRequestProblem(driverError ?? "PLC driver is not supported.", "invalid_driver");
            return false;
        }

        if (!_connectionService.IsValidIpAddress(ipAddress))
        {
            errorResult = BadRequestProblem("Invalid IP address. Enter a valid IPv4 address.", "invalid_ip_address");
            return false;
        }

        return true;
    }

    private static PlcTagBrowseItemDto? FindBestMatch(
        string logicalKey,
        IReadOnlyCollection<PlcTagBrowseItemDto> tags,
        HashSet<string> usedTagNames)
    {
        var aliases = GetAliases(logicalKey);
        return tags
            .Where(tag => !usedTagNames.Contains(tag.Name))
            .Select(tag => new
            {
                Tag = tag,
                Score = ComputeAliasScore(tag.Name, aliases),
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Tag.Name.Length)
            .Select(candidate => candidate.Tag)
            .FirstOrDefault();
    }

    private static int ComputeAliasScore(string tagName, IReadOnlyCollection<string> aliases)
    {
        var normalizedName = NormalizeTagName(tagName);
        var score = 0;

        foreach (var alias in aliases)
        {
            var normalizedAlias = NormalizeTagName(alias);
            if (normalizedName == normalizedAlias)
            {
                score = Math.Max(score, 200);
            }
            else if (normalizedName.EndsWith(normalizedAlias, StringComparison.OrdinalIgnoreCase))
            {
                score = Math.Max(score, 150);
            }
            else if (normalizedName.Contains(normalizedAlias, StringComparison.OrdinalIgnoreCase))
            {
                score = Math.Max(score, 120);
            }
            else
            {
                var splitAlias = normalizedAlias.Split('_', StringSplitOptions.RemoveEmptyEntries);
                if (splitAlias.Length > 1 && splitAlias.All(part => normalizedName.Contains(part, StringComparison.OrdinalIgnoreCase)))
                {
                    score = Math.Max(score, 100);
                }
            }
        }

        return score;
    }

    private static IReadOnlyCollection<string> GetAliases(string logicalKey)
    {
        return logicalKey switch
        {
            "line_id" => ["lineid", "line_id", "line.number", "line.numberid"],
            "product_id" => ["productid", "product_id", "product", "productserial"],
            "control_mode" => ["controlmode", "control_mode", "mode", "automanual"],
            "machine_state" => ["machinestate", "machine_state", "state", "status"],
            "production_length" => ["productionlength", "production_length", "totallength", "length"],
            "bare_setpoint" => ["bareodsetpoint", "bare_setpoint", "bare.od.sp", "baretarget"],
            "bare_actual" => ["bareodactual", "bare_actual", "bare.od.pv", "baremeasured"],
            "hot_setpoint" => ["hotodsetpoint", "hot_setpoint", "hot.od.sp", "hottarget"],
            "hot_actual" => ["hotodactual", "hot_actual", "hot.od.pv", "hotmeasured"],
            "cold_setpoint" => ["coldodsetpoint", "cold_setpoint", "cold.od.sp", "coldtarget"],
            "cold_actual" => ["coldodactual", "cold_actual", "cold.od.pv", "coldmeasured"],
            _ => [logicalKey],
        };
    }

    private static string NormalizeDataType(string rawDataType)
    {
        var normalized = rawDataType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "float" or "double" => "real",
            "integer" => "int",
            "int32" => "dint",
            _ => normalized,
        };
    }

    private static string NormalizeTagName(string tagName)
    {
        return Regex.Replace(tagName.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
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
