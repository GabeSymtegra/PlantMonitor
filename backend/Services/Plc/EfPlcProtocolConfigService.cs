using backend.Data;
using backend.DTOs.Plc;
using backend.Interfaces.Plc;
using backend.Models.Plc;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Plc;

public sealed class EfPlcProtocolConfigService : IPlcProtocolConfigService
{
    private static readonly HashSet<string> RequiredTagKeys =
    [
        "status",
        "product",
        "runtime_seconds",
        "total_length",
        "control_mode",
    ];

    private static readonly HashSet<string> AllowedDataTypes =
    [
        "bool",
        "int",
        "dint",
        "real",
        "string",
    ];

    private static readonly IReadOnlyList<TagSlotDefinitionDto> RequiredTagSlots =
    [
        new() { LogicalKey = "line_id", DisplayName = "Line ID", IsRequired = true, Description = "Unique line identifier from PLC." },
        new() { LogicalKey = "product_id", DisplayName = "Product ID", IsRequired = true, Description = "Current product identifier/serial." },
        new() { LogicalKey = "control_mode", DisplayName = "Control Mode", IsRequired = true, Description = "Current control mode (Auto/Manual)." },
        new() { LogicalKey = "machine_state", DisplayName = "Machine State", IsRequired = true, Description = "Current machine state/status code." },
        new() { LogicalKey = "production_length", DisplayName = "Production Length", IsRequired = true, Description = "Current produced length." },
        new() { LogicalKey = "bare_setpoint", DisplayName = "Bare Setpoint", IsRequired = true, Description = "Bare OD setpoint." },
        new() { LogicalKey = "bare_actual", DisplayName = "Bare Actual", IsRequired = true, Description = "Bare OD actual." },
        new() { LogicalKey = "hot_setpoint", DisplayName = "Hot Setpoint", IsRequired = true, Description = "Hot OD setpoint." },
        new() { LogicalKey = "hot_actual", DisplayName = "Hot Actual", IsRequired = true, Description = "Hot OD actual." },
        new() { LogicalKey = "cold_setpoint", DisplayName = "Cold Setpoint", IsRequired = true, Description = "Cold OD setpoint." },
        new() { LogicalKey = "cold_actual", DisplayName = "Cold Actual", IsRequired = true, Description = "Cold OD actual." },
    ];

    private readonly PlantMonitorDbContext _dbContext;
    private readonly IPlcTagAddressValidator _addressValidator;

    public EfPlcProtocolConfigService(PlantMonitorDbContext dbContext, IPlcTagAddressValidator addressValidator)
    {
        _dbContext = dbContext;
        _addressValidator = addressValidator;
    }

    public IReadOnlyCollection<PlcPresetDto> GetPresets(string? manufacturer)
    {
        var query = _dbContext.PlcProtocolPresets
            .AsNoTracking()
            .Include(p => p.Tags)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(manufacturer))
        {
            var normalizedManufacturer = NormalizeManufacturer(manufacturer).ToLower();
            query = query.Where(p =>
                p.Manufacturer.ToLower() == normalizedManufacturer
                || (normalizedManufacturer == "allenbradley" && p.Manufacturer.ToLower() == "ab"));
        }

        var presets = query
            .OrderBy(p => p.Manufacturer)
            .ThenBy(p => p.PresetName)
            .ThenBy(p => p.PresetVersion)
            .ToList();

        return presets.Select(MapPreset).ToList();
    }

    public IReadOnlyCollection<TagSlotDefinitionDto> GetRequiredTagSlots()
    {
        return RequiredTagSlots;
    }

    public LineProtocolAssignmentDto? GetAssignment(int lineId)
    {
        var assignment = _dbContext.LineProtocolAssignments
            .AsNoTracking()
            .SingleOrDefault(a => a.LineId == lineId);

        return assignment is null ? null : MapAssignment(assignment);
    }

    public IReadOnlyCollection<LineTagCatalogEntryDto> GetTagCatalog(int lineId)
    {
        return _dbContext.LineTagCatalogEntries
            .AsNoTracking()
            .Where(x => x.LineId == lineId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LogicalKey)
            .Select(MapCatalogEntry)
            .ToList();
    }

    public bool TryReplaceTagCatalog(
        int lineId,
        UpdateLineTagCatalogRequestDto request,
        out IReadOnlyCollection<LineTagCatalogEntryDto>? tags,
        out string? error)
    {
        tags = null;
        error = null;

        if (lineId <= 0)
        {
            error = "Line id must be greater than 0.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Driver))
        {
            error = "Driver is required.";
            return false;
        }

        var normalizedDriver = NormalizeManufacturer(request.Driver);
        var allowedKeys = RequiredTagSlots
            .Select(x => x.LogicalKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredKeys = RequiredTagSlots.Where(x => x.IsRequired)
            .Select(x => x.LogicalKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var catalog = request.Tags ?? [];
        var duplicateKeys = catalog
            .GroupBy(x => x.LogicalKey, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateKeys.Count > 0)
        {
            error = $"Duplicate logical keys detected: {string.Join(", ", duplicateKeys)}";
            return false;
        }

        var unknownKeys = catalog
            .Select(x => x.LogicalKey?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(key => !allowedKeys.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknownKeys.Count > 0)
        {
            error = $"Unknown logical keys are not allowed: {string.Join(", ", unknownKeys)}";
            return false;
        }

        var missingRequired = requiredKeys
            .Where(required => !catalog.Any(x => x.LogicalKey.Equals(required, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingRequired.Count > 0)
        {
            error = $"Missing required logical keys: {string.Join(", ", missingRequired)}";
            return false;
        }

        var duplicateAddresses = catalog
            .Where(x => !string.IsNullOrWhiteSpace(x.PlcAddress))
            .GroupBy(x => x.PlcAddress.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateAddresses.Count > 0)
        {
            error = $"Each PLC address can only be assigned once per line. Duplicate addresses: {string.Join(", ", duplicateAddresses)}";
            return false;
        }

        foreach (var entry in catalog)
        {
            if (string.IsNullOrWhiteSpace(entry.LogicalKey))
            {
                error = "Logical key is required for every catalog entry.";
                return false;
            }

            var normalizedKey = entry.LogicalKey.Trim();
            var requiredSlot = RequiredTagSlots.FirstOrDefault(x => x.LogicalKey.Equals(normalizedKey, StringComparison.OrdinalIgnoreCase));
            if (requiredSlot is null)
            {
                error = $"Unknown logical key '{normalizedKey}'.";
                return false;
            }

            if (requiredSlot.IsRequired && (!entry.IsRequired || !entry.IsEnabled))
            {
                error = $"Logical key '{normalizedKey}' must remain enabled and required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.PlcAddress))
            {
                error = $"PLC address is required for logical key '{entry.LogicalKey}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(entry.Driver)
                && !entry.Driver.Trim().Equals(normalizedDriver, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Logical key '{entry.LogicalKey}' has driver '{entry.Driver}' but request driver is '{normalizedDriver}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.DataType)
                || !AllowedDataTypes.Contains(entry.DataType.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                error = $"Data type for logical key '{entry.LogicalKey}' must be one of: bool, int, dint, real, string.";
                return false;
            }

            if (entry.ReadFrequencyMs is < 250 or > 60000)
            {
                error = $"Read frequency for logical key '{entry.LogicalKey}' must be between 250 and 60000 milliseconds.";
                return false;
            }

            if (entry.Scale <= 0)
            {
                error = $"Scale for logical key '{entry.LogicalKey}' must be greater than 0.";
                return false;
            }

            if (!_addressValidator.IsValidAddress(normalizedDriver, entry.PlcAddress, out var addressMessage))
            {
                error = $"Logical key '{entry.LogicalKey}' has invalid PLC address: {addressMessage}";
                return false;
            }
        }

        var existing = _dbContext.LineTagCatalogEntries
            .Where(x => x.LineId == lineId)
            .ToList();

        _dbContext.LineTagCatalogEntries.RemoveRange(existing);

        var utcNow = DateTime.UtcNow;
        var entities = catalog.Select((x, index) => new LineTagCatalogEntryEntity
        {
            LineId = lineId,
            LogicalKey = x.LogicalKey.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? x.LogicalKey.Trim() : x.DisplayName.Trim(),
            Driver = normalizedDriver,
            PlcAddress = x.PlcAddress.Trim(),
            DataType = x.DataType.Trim().ToLowerInvariant(),
            Unit = x.Unit,
            Scale = x.Scale,
            Description = x.Description,
            IsEnabled = x.IsEnabled,
            SortOrder = x.SortOrder == 0 ? index : x.SortOrder,
            ReadFrequencyMs = x.ReadFrequencyMs <= 0 ? 1000 : x.ReadFrequencyMs,
            IsRequired = x.IsRequired,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        }).ToList();

        _dbContext.LineTagCatalogEntries.AddRange(entities);
        _dbContext.SaveChanges();

        tags = GetTagCatalog(lineId);
        return true;
    }

    public bool TryUpsertAssignment(int lineId, UpdateLineProtocolAssignmentRequestDto request, out string? error)
    {
        error = null;

        if (lineId <= 0)
        {
            error = "Line id must be greater than 0.";
            return false;
        }

        var requestedManufacturer = NormalizeManufacturer(request.Manufacturer).ToLower();
        var presetExists = _dbContext.PlcProtocolPresets.Any(p =>
            (p.Manufacturer.ToLower() == requestedManufacturer
                || (requestedManufacturer == "allenbradley" && p.Manufacturer.ToLower() == "ab"))
            && p.PresetName.ToLower() == request.PresetName.Trim().ToLower()
            && p.PresetVersion == request.PresetVersion);

        if (!presetExists)
        {
            error = "Preset not found for the selected manufacturer/version.";
            return false;
        }

        if (request.PollIntervalMs is < 500 or > 60000)
        {
            error = "Poll interval must be between 500 and 60000 milliseconds.";
            return false;
        }

        var assignment = _dbContext.LineProtocolAssignments.SingleOrDefault(a => a.LineId == lineId);

        if (assignment is null)
        {
            assignment = new LineProtocolAssignmentEntity
            {
                LineId = lineId,
                IsActive = false,
                LineLifecycleState = LineLifecycleState.Draft,
            };
            _dbContext.LineProtocolAssignments.Add(assignment);
        }

        assignment.Manufacturer = request.Manufacturer.Trim();
        assignment.PresetName = request.PresetName.Trim();
        assignment.PresetVersion = request.PresetVersion;
        assignment.PollIntervalMs = request.PollIntervalMs;
        assignment.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.SaveChanges();
        return true;
    }

    public IReadOnlyCollection<EffectiveTagMappingDto>? GetEffectiveTags(int lineId, out string? error)
    {
        error = null;

        var assignment = _dbContext.LineProtocolAssignments
            .AsNoTracking()
            .SingleOrDefault(a => a.LineId == lineId);

        if (assignment is null)
        {
            error = "Protocol assignment was not found for this line.";
            return null;
        }

        var normalizedAssignmentManufacturer = NormalizeManufacturer(assignment.Manufacturer).ToLower();
        var isAllenBradley = normalizedAssignmentManufacturer == "allenbradley";

        var preset = _dbContext.PlcProtocolPresets
            .AsNoTracking()
            .Include(p => p.Tags)
            .SingleOrDefault(p =>
                (p.Manufacturer.ToLower() == normalizedAssignmentManufacturer
                    || (isAllenBradley && p.Manufacturer.ToLower() == "ab"))
                && p.PresetName.ToLower() == assignment.PresetName.ToLower()
                && p.PresetVersion == assignment.PresetVersion);

        if (preset is null)
        {
            error = "Assigned preset was not found.";
            return null;
        }

        var effective = preset.Tags
            .Select(MapTag)
            .ToDictionary(t => t.TagKey, StringComparer.OrdinalIgnoreCase);

        var overrides = _dbContext.LineTagOverrides
            .AsNoTracking()
            .Where(t => t.LineId == lineId)
            .ToList();

        foreach (var overrideTag in overrides)
        {
            effective[overrideTag.TagKey] = MapTag(overrideTag);
        }

        return effective.Values
            .OrderBy(t => t.TagKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public TagValidationResponseDto ValidateTags(ValidateTagsRequestDto request)
    {
        var issues = new List<TagValidationIssueDto>();

        if (request.PollIntervalMs is < 500 or > 60000)
        {
            issues.Add(new TagValidationIssueDto
            {
                Field = "pollIntervalMs",
                Message = "Poll interval must be between 500 and 60000 milliseconds.",
            });
        }

        if (!request.Manufacturer.Equals("AB", StringComparison.OrdinalIgnoreCase)
            && !request.Manufacturer.Equals("AllenBradley", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new TagValidationIssueDto
            {
                Field = "manufacturer",
                Message = "Manufacturer must be AllenBradley.",
            });
        }

        var duplicateKeys = request.Tags
            .GroupBy(t => t.TagKey, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicateKey in duplicateKeys)
        {
            issues.Add(new TagValidationIssueDto
            {
                Field = "tags.tagKey",
                Message = $"Duplicate tag key detected: {duplicateKey}",
            });
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Tags.Count; index += 1)
        {
            var tag = request.Tags[index];
            var prefix = $"tags[{index}]";

            if (string.IsNullOrWhiteSpace(tag.TagKey))
            {
                issues.Add(new TagValidationIssueDto
                {
                    Field = $"{prefix}.tagKey",
                    Message = "Tag key is required.",
                });
            }
            else
            {
                seenKeys.Add(tag.TagKey);
            }

            if (string.IsNullOrWhiteSpace(tag.DataType)
                || !AllowedDataTypes.Contains(tag.DataType.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new TagValidationIssueDto
                {
                    Field = $"{prefix}.dataType",
                    Message = "Data type must be one of: bool, int, dint, real, string.",
                });
            }

            if (!_addressValidator.IsValidAddress(request.Manufacturer, tag.PlcAddress, out var addressMessage))
            {
                issues.Add(new TagValidationIssueDto
                {
                    Field = $"{prefix}.plcAddress",
                    Message = addressMessage,
                });
            }
        }

        foreach (var requiredKey in RequiredTagKeys)
        {
            if (!seenKeys.Contains(requiredKey))
            {
                issues.Add(new TagValidationIssueDto
                {
                    Field = "tags",
                    Message = $"Missing required tag mapping: {requiredKey}",
                });
            }
        }

        return new TagValidationResponseDto
        {
            IsValid = issues.Count == 0,
            Issues = issues,
        };
    }

    public bool TryUpsertOverrides(
        int lineId,
        UpdateLineTagOverridesRequestDto request,
        out IReadOnlyCollection<EffectiveTagMappingDto>? effectiveTags,
        out string? error)
    {
        effectiveTags = null;
        error = null;

        var assignment = _dbContext.LineProtocolAssignments
            .AsNoTracking()
            .SingleOrDefault(a => a.LineId == lineId);

        if (assignment is null)
        {
            error = "Protocol assignment was not found for this line.";
            return false;
        }

        var validation = ValidateTags(new ValidateTagsRequestDto
        {
            Manufacturer = assignment.Manufacturer,
            PollIntervalMs = assignment.PollIntervalMs,
            Tags = request.Tags,
        });

        if (!validation.IsValid)
        {
            error = "Tag override payload failed validation.";
            return false;
        }

        var existingOverrides = _dbContext.LineTagOverrides
            .Where(t => t.LineId == lineId)
            .ToList();

        _dbContext.LineTagOverrides.RemoveRange(existingOverrides);

        var newOverrides = request.Tags.Select(t => new LineTagOverrideEntity
        {
            LineId = lineId,
            TagKey = t.TagKey,
            PlcAddress = t.PlcAddress,
            DataType = t.DataType,
            Scale = t.Scale,
            IsRequired = t.IsRequired,
        });

        _dbContext.LineTagOverrides.AddRange(newOverrides);
        _dbContext.SaveChanges();

        effectiveTags = GetEffectiveTags(lineId, out error);
        return effectiveTags is not null;
    }

    private static PlcPresetDto MapPreset(PlcProtocolPresetEntity preset)
    {
        return new PlcPresetDto
        {
            Manufacturer = NormalizeManufacturer(preset.Manufacturer),
            PresetName = preset.PresetName,
            PresetVersion = preset.PresetVersion,
            Description = preset.Description,
            Tags = preset.Tags
                .Select(MapTag)
                .OrderBy(t => t.TagKey, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private static LineProtocolAssignmentDto MapAssignment(LineProtocolAssignmentEntity assignment)
    {
        return new LineProtocolAssignmentDto
        {
            LineId = assignment.LineId,
            Manufacturer = NormalizeManufacturer(assignment.Manufacturer),
            PresetName = assignment.PresetName,
            PresetVersion = assignment.PresetVersion,
            PollIntervalMs = assignment.PollIntervalMs,
            UpdatedAtUtc = assignment.UpdatedAtUtc,
        };
    }

    private static string NormalizeManufacturer(string manufacturer)
    {
        return manufacturer.Trim().Equals("AB", StringComparison.OrdinalIgnoreCase)
            ? "AllenBradley"
            : manufacturer.Trim();
    }

    private static EffectiveTagMappingDto MapTag(PlcProtocolPresetTagEntity tag)
    {
        return new EffectiveTagMappingDto
        {
            TagKey = tag.TagKey,
            PlcAddress = tag.PlcAddress,
            DataType = tag.DataType,
            Scale = tag.Scale,
            IsRequired = tag.IsRequired,
        };
    }

    private static EffectiveTagMappingDto MapTag(LineTagOverrideEntity tag)
    {
        return new EffectiveTagMappingDto
        {
            TagKey = tag.TagKey,
            PlcAddress = tag.PlcAddress,
            DataType = tag.DataType,
            Scale = tag.Scale,
            IsRequired = tag.IsRequired,
        };
    }

    private static LineTagCatalogEntryDto MapCatalogEntry(LineTagCatalogEntryEntity entity)
    {
        return new LineTagCatalogEntryDto
        {
            LogicalKey = entity.LogicalKey,
            DisplayName = entity.DisplayName,
            Driver = entity.Driver,
            PlcAddress = entity.PlcAddress,
            DataType = entity.DataType,
            Unit = entity.Unit,
            Scale = entity.Scale,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            SortOrder = entity.SortOrder,
            ReadFrequencyMs = entity.ReadFrequencyMs,
            IsRequired = entity.IsRequired,
        };
    }
}
