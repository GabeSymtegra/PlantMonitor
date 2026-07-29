using backend.DTOs.Plc;
using backend.Data;
using backend.Interfaces.Plc;
using backend.Models.Plc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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

        var discovered = await driver!.BrowseTagsAsync(request.IpAddress.Trim(), request.Options, null, cancellationToken);
        var readableLeafTags = discovered
            .Where(tag => !tag.IsFolder && tag.CanRead == true)
            .Where(tag => IsAutoMappableDataType(tag.DataType))
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
                DataType = NormalizeDataType(match.Tag.DataType),
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

    private static readonly HashSet<string> NumericLogicalKeys =
    [
        "production_length",
        "bare_setpoint",
        "bare_actual",
        "hot_setpoint",
        "hot_actual",
        "cold_setpoint",
        "cold_actual",
    ];

    private static readonly HashSet<string> NumericDataTypes =
    [
        "int",
        "dint",
        "real",
        "sint",
        "lint",
        "lreal",
    ];

    private static readonly HashSet<string> TextOrCodeDataTypes =
    [
        "int",
        "dint",
        "string",
    ];

    private static readonly HashSet<string> ProductIdDataTypes =
    [
        "string",
        "int",
        "dint",
        "sint",
        "lint",
    ];

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

        if (!IsAllenBradleyManufacturer(line.Manufacturer))
        {
            issues.Add("Line manufacturer must be AllenBradley for commissioning.");
        }

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

                if (!TryConvertToExpectedRuntimeValue(logicalKey, read.Value, out var conversionError))
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
        return new PlcConnectionOptionsDto
        {
            RoutePath = string.IsNullOrWhiteSpace(assignment.RoutePath) ? "1,0" : assignment.RoutePath,
            ProcessorType = string.IsNullOrWhiteSpace(assignment.ProcessorType) ? "ControlLogix" : assignment.ProcessorType,
            ConnectionTimeoutMs = assignment.ConnectionTimeoutMs <= 0 ? 3000 : assignment.ConnectionTimeoutMs,
            ReadTimeoutMs = assignment.ReadTimeoutMs <= 0 ? 3000 : assignment.ReadTimeoutMs,
            RetryCount = assignment.RetryCount < 0 ? 0 : assignment.RetryCount,
            RetryDelayMs = assignment.RetryDelayMs < 0 ? 0 : assignment.RetryDelayMs,
        };
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
        var normalizedDataType = dataType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDataType))
        {
            issues.Add($"Required logical key '{logicalKey}' has no configured data type.");
            return;
        }

        if (NumericLogicalKeys.Contains(logicalKey))
        {
            if (!NumericDataTypes.Contains(normalizedDataType))
            {
                issues.Add($"Required logical key '{logicalKey}' must use a numeric data type (int, dint, real). Received '{normalizedDataType}'.");
            }

            return;
        }

        if (logicalKey.Equals("control_mode", StringComparison.OrdinalIgnoreCase)
            || logicalKey.Equals("machine_state", StringComparison.OrdinalIgnoreCase))
        {
            if (!TextOrCodeDataTypes.Contains(normalizedDataType))
            {
                issues.Add($"Required logical key '{logicalKey}' must use int, dint, or string. Received '{normalizedDataType}'.");
            }

            return;
        }

        if (logicalKey.Equals("product_id", StringComparison.OrdinalIgnoreCase))
        {
            if (!ProductIdDataTypes.Contains(normalizedDataType))
            {
                issues.Add($"Required logical key '{logicalKey}' must use string, int, or dint. Received '{normalizedDataType}'.");
            }

            return;
        }

        if (logicalKey.Equals("line_id", StringComparison.OrdinalIgnoreCase)
            && !TextOrCodeDataTypes.Contains(normalizedDataType))
        {
            issues.Add($"Required logical key '{logicalKey}' must use int, dint, or string. Received '{normalizedDataType}'.");
        }
    }

    private static bool TryConvertToExpectedRuntimeValue(string logicalKey, string rawValue, out string error)
    {
        error = string.Empty;
        var trimmed = rawValue.Trim();

        if (NumericLogicalKeys.Contains(logicalKey))
        {
            if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                error = "Expected a numeric value.";
                return false;
            }

            return true;
        }

        if (logicalKey.Equals("control_mode", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryConvertControlMode(trimmed, out _))
            {
                error = "Control mode conversion is not supported. Expected Auto/Manual or 1/0.";
                return false;
            }

            return true;
        }

        if (logicalKey.Equals("machine_state", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryConvertMachineState(trimmed, out _))
            {
                error = "Machine state conversion is not supported. Expected known state text or code (0-5).";
                return false;
            }

            return true;
        }

        return !string.IsNullOrWhiteSpace(trimmed);
    }

    private static bool TryConvertControlMode(string rawValue, out string normalized)
    {
        normalized = string.Empty;
        var value = rawValue.Trim().ToLowerInvariant();

        if (value is "1" or "auto")
        {
            normalized = "Auto";
            return true;
        }

        if (value is "0" or "manual")
        {
            normalized = "Manual";
            return true;
        }

        if (value.Contains("auto", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Auto";
            return true;
        }

        if (value.Contains("manual", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Manual";
            return true;
        }

        return false;
    }

    private static bool TryConvertMachineState(string rawValue, out string normalized)
    {
        normalized = string.Empty;
        var value = rawValue.Trim().ToLowerInvariant();

        if (value == "0" || value.Contains("stop", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Stopped";
            return true;
        }

        if (value == "1" || value.Contains("run", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Running";
            return true;
        }

        if (value == "2" || value.Contains("bleedout", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Bleedout";
            return true;
        }

        if (value == "3" || value.Contains("startup", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Startup";
            return true;
        }

        if (value == "4" || value.Contains("fault", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Faulted";
            return true;
        }

        if (value == "5" || value.Contains("maintenance", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Maintenance";
            return true;
        }

        return false;
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
        var normalizedDataType = NormalizeDataType(tag.DataType);
        if (!IsLogicalKeyCompatible(logicalKey, normalizedDataType))
        {
            return null;
        }

        var score = ComputeAliasScore(tag.Name, aliases);
        if (score <= 0)
        {
            return null;
        }

        if (NumericLogicalKeys.Contains(logicalKey) && normalizedDataType is "dint" or "real")
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
            BuildSuggestionReason(logicalKey, tag.Name, normalizedDataType, score));
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
        var normalized = NormalizeDataType(dataType ?? string.Empty);
        return normalized is "bool" or "int" or "dint" or "real" or "string" or "sint" or "lint" or "lreal";
    }

    private static bool IsLogicalKeyCompatible(string logicalKey, string normalizedDataType)
    {
        if (string.IsNullOrWhiteSpace(normalizedDataType))
        {
            return false;
        }

        if (NumericLogicalKeys.Contains(logicalKey))
        {
            return NumericDataTypes.Contains(normalizedDataType);
        }

        if (logicalKey.Equals("control_mode", StringComparison.OrdinalIgnoreCase)
            || logicalKey.Equals("machine_state", StringComparison.OrdinalIgnoreCase)
            || logicalKey.Equals("line_id", StringComparison.OrdinalIgnoreCase))
        {
            return TextOrCodeDataTypes.Contains(normalizedDataType);
        }

        if (logicalKey.Equals("product_id", StringComparison.OrdinalIgnoreCase))
        {
            return ProductIdDataTypes.Contains(normalizedDataType);
        }

        return true;
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
        if (string.IsNullOrWhiteSpace(rawDataType))
        {
            return "unknown";
        }

        var normalized = rawDataType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "float" or "double" => "real",
            "integer" => "int",
            "int32" => "dint",
            "type-193" => "bool",
            "type-194" => "sint",
            "type-195" => "int",
            "type-196" => "dint",
            "type-197" => "lint",
            "type-202" => "real",
            "type-203" => "lreal",
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
