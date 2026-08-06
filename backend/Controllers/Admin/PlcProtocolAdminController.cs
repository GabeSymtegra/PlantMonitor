using backend.DTOs.Plc;
using backend.Data;
using backend.Interfaces.Plc;
using backend.Models.Plc;
using backend.Services.Plc;
using Microsoft.EntityFrameworkCore;
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
    private readonly PlantMonitorDbContext _dbContext;

    public PlcProtocolAdminController(
        IPlcProtocolConfigService protocolConfigService,
        IPlcConnectionService connectionService,
        PlantMonitorDbContext dbContext)
    {
        _protocolConfigService = protocolConfigService;
        _connectionService = connectionService;
        _dbContext = dbContext;
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

        ResetLineToDraftIfActive(lineId);

        return Ok(tags);
    }

    [HttpPut("lines/{lineId:int}/protocol-assignment")]
    public IActionResult UpsertLineProtocolAssignment(int lineId, [FromBody] UpdateLineProtocolAssignmentRequestDto request)
    {
        var line = _dbContext.LineProtocolAssignments.AsNoTracking().SingleOrDefault(x => x.LineId == lineId);
        if (line is null)
        {
            return NotFoundProblem("Line configuration not found.", "line_config_not_found");
        }

        if (!_protocolConfigService.TryUpsertAssignment(lineId, request, out var error))
        {
            return BadRequestProblem(error ?? "Protocol assignment update failed.", "invalid_protocol_assignment");
        }

        ResetLineToDraftIfActive(lineId);

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

        ResetLineToDraftIfActive(lineId);

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
            if (string.Equals(result.Message, "Wrong driver selected. Choose AllenBradley or Siemens.", StringComparison.OrdinalIgnoreCase)
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

        var tags = await driver!.BrowseTagsAsync(request.IpAddress.Trim(), request.Options, request.Search, cancellationToken);
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

        var discovered = await driver!.BrowseTagsAsync(request.IpAddress.Trim(), request.Options, request.Search, cancellationToken);
        var readableLeafTags = discovered
            .Where(PlcTagCatalogContract.IsDirectlyReadablePrimitiveTag)
            .ToList();

        var slotDefinitions = _protocolConfigService.GetRequiredTagSlots();
        var usedTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestions = new List<LineTagCatalogEntryDto>();
        var suggestionDetails = new List<AutoMapTagSuggestionDto>();
        var missing = new List<string>();

        foreach (var slot in slotDefinitions)
        {
            var match = FindBestMatch(slot.LogicalKey, readableLeafTags, usedTagNames);

            if (match is null)
            {
                missing.Add(slot.LogicalKey);
                continue;
            }

            usedTagNames.Add(match.Tag.Name);
            suggestions.Add(new LineTagCatalogEntryDto
            {
                LogicalKey = slot.LogicalKey,
                DisplayName = slot.DisplayName,
                Driver = driver.DriverName,
                PlcAddress = match.Tag.Name,
                DataType = PlcTagCatalogContract.NormalizeDataType(match.Tag.DataType),
                IsRequired = slot.IsRequired,
                IsEnabled = true,
                SortOrder = suggestions.Count,
                ReadFrequencyMs = 1000,
                Description = slot.Description,
            });

            suggestionDetails.Add(new AutoMapTagSuggestionDto
            {
                LogicalKey = slot.LogicalKey,
                PlcAddress = match.Tag.Name,
                Confidence = match.Confidence,
                Reason = match.Reason,
            });
        }

        return Ok(new AutoMapTagCatalogResultDto
        {
            Driver = driver.DriverName,
            ScannedTagCount = readableLeafTags.Count,
            SuggestedMappings = suggestions,
            SuggestionDetails = suggestionDetails,
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

        var result = await driver!.ReadTagAsync(request.IpAddress.Trim(), request.Options, request.TagName.Trim(), cancellationToken);
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

        var result = await driver!.ReadTagsAsync(request.IpAddress.Trim(), request.Options, request.TagNames, cancellationToken);
        return Ok(result);
    }

    [HttpGet("lines/{lineId:int}/commissioning-check")]
    public async Task<ActionResult<CommissioningReadinessDto>> GetCommissioningReadiness(int lineId, CancellationToken cancellationToken)
    {
        var line = _dbContext.LineProtocolAssignments.SingleOrDefault(x => x.LineId == lineId);
        if (line is null)
        {
            return NotFoundProblem("Line configuration not found.", "line_config_not_found");
        }

        line.LineLifecycleState = LineLifecycleState.Commissioning;
        line.IsActive = false;
        line.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.SaveChanges();

        var readiness = await BuildCommissioningReadinessAsync(line, cancellationToken);

        if (!readiness.IsReady)
        {
            line.LineLifecycleState = LineLifecycleState.CommissioningFailed;
            line.IsActive = false;
            line.UpdatedAtUtc = DateTime.UtcNow;
            _dbContext.SaveChanges();
        }

        return Ok(readiness);
    }

    [HttpPost("lines/{lineId:int}/commissioning-activate")]
    public async Task<ActionResult> ActivateCommissionedLine(int lineId, CancellationToken cancellationToken)
    {
        var line = _dbContext.LineProtocolAssignments.SingleOrDefault(x => x.LineId == lineId);
        if (line is null)
        {
            return NotFoundProblem("Line configuration not found.", "line_config_not_found");
        }

        var readiness = await BuildCommissioningReadinessAsync(line, cancellationToken);
        if (!readiness.IsReady)
        {
            line.LineLifecycleState = LineLifecycleState.CommissioningFailed;
            line.IsActive = false;
            line.UpdatedAtUtc = DateTime.UtcNow;
            _dbContext.SaveChanges();

            var detail = readiness.Issues.Count == 0
                ? "Commissioning validation failed."
                : string.Join(" ", readiness.Issues);

            return ConflictProblem(detail, "commissioning_validation_failed");
        }

        line.LineLifecycleState = LineLifecycleState.Active;
        line.IsActive = true;
        line.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.SaveChanges();

        return Ok(new
        {
            lineId,
            lineLifecycleState = LineLifecycleState.Active,
        });
    }

    private async Task<CommissioningReadinessDto> BuildCommissioningReadinessAsync(LineProtocolAssignmentEntity line, CancellationToken cancellationToken)
    {
        var lineId = line.LineId;
        var assignment = _protocolConfigService.GetAssignment(lineId);
        if (assignment is null)
        {
            return new CommissioningReadinessDto
            {
                LineId = lineId,
                IsReady = false,
                RequiredTagCount = 0,
                MappedRequiredTagCount = 0,
                MissingRequiredTagKeys = [],
                Issues = ["Protocol assignment not found for line."],
                CheckedAtUtc = DateTime.UtcNow,
            };
        }

        var requiredSlots = _protocolConfigService.GetRequiredTagSlots()
            .Where(slot => slot.IsRequired)
            .ToList();

        var issues = new List<string>();
        var requiredMappings = new Dictionary<string, LineTagCatalogEntryDto>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(assignment.PresetName) || assignment.PresetVersion <= 0)
        {
            issues.Add("Protocol assignment is incomplete. Preset name and version are required.");
        }

        if (!_connectionService.IsValidIpAddress(line.PlcIp))
        {
            issues.Add("Line PLC IP address is invalid.");
        }

        var tagCatalog = _protocolConfigService.GetTagCatalog(lineId)
            .Where(x => x.IsEnabled)
            .ToList();

        if (tagCatalog.Count == 0)
        {
            issues.Add("Tag catalog is empty. A complete required tag catalog is needed before commissioning.");
        }

        foreach (var slot in requiredSlots)
        {
            var mapping = tagCatalog.FirstOrDefault(x => x.LogicalKey.Equals(slot.LogicalKey, StringComparison.OrdinalIgnoreCase));
            if (mapping is null)
            {
                continue;
            }

            requiredMappings[slot.LogicalKey] = mapping;

            if (string.IsNullOrWhiteSpace(mapping.PlcAddress))
            {
                issues.Add($"Required logical key '{slot.LogicalKey}' is missing a PLC address.");
                continue;
            }

            ValidateLogicalKeyDataType(slot.LogicalKey, mapping.DataType, issues);
        }

        var missingRequiredKeys = requiredSlots
            .Select(slot => slot.LogicalKey)
            .Where(requiredKey => !requiredMappings.ContainsKey(requiredKey))
            .ToList();

        if (missingRequiredKeys.Count > 0)
        {
            issues.Add($"Missing required logical keys: {string.Join(", ", missingRequiredKeys)}.");
        }

        if (!_connectionService.TryResolveDriver(line.Manufacturer, out var driver, out var driverError)
            || driver is null)
        {
            issues.Add(driverError ?? "Unable to resolve PLC driver for commissioning.");
        }

        if (issues.Count == 0)
        {
            var connectionResult = await _connectionService.TestConnectionAsync(new PlcConnectionRequest
            {
                Driver = line.Manufacturer,
                IpAddress = line.PlcIp.Trim(),
                Options = BuildConnectionOptions(assignment),
            }, cancellationToken);

            if (!connectionResult.IsConnected)
            {
                issues.Add($"PLC connection test failed: {connectionResult.Message}");
            }
        }

        if (issues.Count == 0 && driver is not null)
        {
            var requiredAddresses = requiredMappings
                .Values
                .Select(x => x.PlcAddress.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var readResults = await driver.ReadTagsAsync(line.PlcIp.Trim(), BuildConnectionOptions(assignment), requiredAddresses, cancellationToken);
            var readMap = readResults
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToDictionary(x => x.Name.Trim(), x => x, StringComparer.OrdinalIgnoreCase);

            foreach (var (logicalKey, mapping) in requiredMappings)
            {
                var address = mapping.PlcAddress.Trim();

                if (!readMap.TryGetValue(address, out var read))
                {
                    issues.Add($"Required logical key '{logicalKey}' did not return a read result.");
                    continue;
                }

                if (read.CanRead == false)
                {
                    issues.Add($"Required logical key '{logicalKey}' is not readable at '{address}'.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(read.Error))
                {
                    issues.Add($"Required logical key '{logicalKey}' read failed at '{address}': {read.Error}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(read.Value))
                {
                    issues.Add($"Required logical key '{logicalKey}' returned an empty value from '{address}'.");
                    continue;
                }

                if (LooksLikeStructuredOrRawPayload(read.Value))
                {
                    issues.Add($"Required logical key '{logicalKey}' at '{address}' returned a structured/raw payload that cannot be used by runtime mappings.");
                    continue;
                }

                if (!PlcTagCatalogContract.TryConvertToExpectedRuntimeValue(logicalKey, read.Value, out var conversionError))
                {
                    issues.Add($"Required logical key '{logicalKey}' returned unsupported value '{read.Value}' at '{address}': {conversionError}");
                }
            }
        }

        return new CommissioningReadinessDto
        {
            LineId = lineId,
            Manufacturer = assignment.Manufacturer,
            PresetName = assignment.PresetName,
            PresetVersion = assignment.PresetVersion,
            IsReady = issues.Count == 0,
            RequiredTagCount = requiredSlots.Count,
            MappedRequiredTagCount = requiredMappings.Count,
            MissingRequiredTagKeys = missingRequiredKeys,
            Issues = issues,
            CheckedAtUtc = DateTime.UtcNow,
        };
    }

    private static bool IsAllenBradleyManufacturer(string? manufacturer)
    {
        return string.Equals(manufacturer?.Trim(), "AllenBradley", StringComparison.OrdinalIgnoreCase)
            || string.Equals(manufacturer?.Trim(), "AB", StringComparison.OrdinalIgnoreCase);
    }

    private static PlcConnectionOptionsDto BuildConnectionOptions(LineProtocolAssignmentDto assignment)
    {
        var rack = assignment.Rack;
        var slot = assignment.Slot;

        if ((!rack.HasValue || !slot.HasValue)
            && TryParseRackSlot(assignment.RoutePath, out var parsedRack, out var parsedSlot))
        {
            rack = parsedRack;
            slot = parsedSlot;
        }

        return new PlcConnectionOptionsDto
        {
            RoutePath = string.IsNullOrWhiteSpace(assignment.RoutePath) ? "1,0" : assignment.RoutePath,
            ProcessorType = string.IsNullOrWhiteSpace(assignment.ProcessorType) ? "ControlLogix" : assignment.ProcessorType,
            Rack = rack,
            Slot = slot,
            ConnectionTimeoutMs = assignment.ConnectionTimeoutMs <= 0 ? 3000 : assignment.ConnectionTimeoutMs,
            ReadTimeoutMs = assignment.ReadTimeoutMs <= 0 ? 3000 : assignment.ReadTimeoutMs,
            RetryCount = assignment.RetryCount < 0 ? 0 : assignment.RetryCount,
            RetryDelayMs = assignment.RetryDelayMs < 0 ? 0 : assignment.RetryDelayMs,
        };
    }

    private static bool TryParseRackSlot(string? routePath, out int rack, out int slot)
    {
        rack = 0;
        slot = 1;

        if (string.IsNullOrWhiteSpace(routePath))
        {
            return false;
        }

        var segments = routePath.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        return int.TryParse(segments[^2], out rack) && int.TryParse(segments[^1], out slot);
    }

    private static bool LooksLikeStructuredOrRawPayload(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.Contains("\"kind\":\"metadata\"", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("\"fields\"", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("\"typeClass\"", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateLogicalKeyDataType(string logicalKey, string? dataType, ICollection<string> issues)
    {
        var normalizedDataType = PlcTagCatalogContract.NormalizeDataType(dataType ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedDataType))
        {
            issues.Add($"Required logical key '{logicalKey}' has no configured data type.");
            return;
        }

        if (PlcTagCatalogContract.IsNumericLogicalKey(logicalKey))
        {
            if (!PlcTagCatalogContract.IsNumericType(normalizedDataType))
            {
                issues.Add($"Required logical key '{logicalKey}' must use a numeric data type (int, dint, real, sint, lint, lreal). Received '{normalizedDataType}'.");
            }

            return;
        }

        if (logicalKey.Equals("control_mode", StringComparison.OrdinalIgnoreCase)
            || logicalKey.Equals("machine_state", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlcTagCatalogContract.IsTextOrCodeType(normalizedDataType))
            {
                issues.Add($"Required logical key '{logicalKey}' must use int, dint, sint, lint, or string. Received '{normalizedDataType}'.");
            }

            return;
        }

        if (logicalKey.Equals("product_id", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlcTagCatalogContract.IsProductIdType(normalizedDataType))
            {
                issues.Add($"Required logical key '{logicalKey}' must use string, int, dint, sint, or lint. Received '{normalizedDataType}'.");
            }

            return;
        }

        if (logicalKey.Equals("line_id", StringComparison.OrdinalIgnoreCase)
            && !PlcTagCatalogContract.IsTextOrCodeType(normalizedDataType))
        {
            issues.Add($"Required logical key '{logicalKey}' must use int, dint, sint, lint, or string. Received '{normalizedDataType}'.");
        }
    }

    private void ResetLineToDraftIfActive(int lineId)
    {
        var line = _dbContext.LineProtocolAssignments.SingleOrDefault(x => x.LineId == lineId);
        if (line is null)
        {
            return;
        }

        if (!string.Equals(line.LineLifecycleState, LineLifecycleState.Active, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        line.LineLifecycleState = LineLifecycleState.Draft;
        line.IsActive = false;
        line.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.SaveChanges();
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

    private sealed record TagMatchCandidate(
        PlcTagBrowseItemDto Tag,
        int Score,
        int Confidence,
        string Reason);

    private static TagMatchCandidate? FindBestMatch(
        string logicalKey,
        IReadOnlyCollection<PlcTagBrowseItemDto> tags,
        HashSet<string> usedTagNames)
    {
        var aliases = GetAliases(logicalKey);
        return tags
            .Where(tag => !usedTagNames.Contains(tag.Name))
            .Select(tag => BuildCandidate(logicalKey, aliases, tag))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Tag.Name.Length)
            .FirstOrDefault();
    }

    private static TagMatchCandidate? BuildCandidate(
        string logicalKey,
        IReadOnlyCollection<string> aliases,
        PlcTagBrowseItemDto tag)
    {
        var normalizedDataType = PlcTagCatalogContract.NormalizeDataType(tag.DataType);
        if (!PlcTagCatalogContract.IsLogicalKeyCompatible(logicalKey, normalizedDataType))
        {
            return null;
        }

        var score = Math.Max(
            ComputeAliasScore(tag.Name, aliases),
            string.IsNullOrWhiteSpace(tag.DisplayName) ? 0 : ComputeAliasScore(tag.DisplayName, aliases));
        if (score <= 0)
        {
            return null;
        }

        if (PlcTagCatalogContract.IsNumericLogicalKey(logicalKey) && normalizedDataType is "dint" or "real")
        {
            score += 10;
        }

        var confidence = score switch
        {
            >= 190 => 95,
            >= 150 => 85,
            >= 120 => 75,
            _ => 60,
        };

        return new TagMatchCandidate(
            tag,
            score,
            confidence,
            BuildSuggestionReason(logicalKey, tag.DisplayName ?? tag.Name, normalizedDataType, score));
    }

    private static string BuildSuggestionReason(string logicalKey, string tagName, string normalizedDataType, int score)
    {
        var aliasStrength = score switch
        {
            >= 200 => "exact alias",
            >= 150 => "suffix alias",
            >= 120 => "contains alias",
            _ => "partial alias",
        };

        return $"Matched by {aliasStrength}; selected '{tagName}' with compatible data type '{normalizedDataType}' for logical key '{logicalKey}'.";
    }

    private static bool IsAutoMappableDataType(string? dataType)
    {
        return PlcTagCatalogContract.IsAutoMappableDataType(dataType);
    }

    private static bool IsLogicalKeyCompatible(string logicalKey, string normalizedDataType)
    {
        return PlcTagCatalogContract.IsLogicalKeyCompatible(logicalKey, normalizedDataType);
    }

    private static int ComputeAliasScore(string tagName, IReadOnlyCollection<string> aliases)
    {
        var normalizedName = PlcTagCatalogContract.NormalizeTagName(tagName);
        var score = 0;

        foreach (var alias in aliases)
        {
            var normalizedAlias = PlcTagCatalogContract.NormalizeTagName(alias);
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
        return PlcTagCatalogContract.GetAliases(logicalKey);
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
}
