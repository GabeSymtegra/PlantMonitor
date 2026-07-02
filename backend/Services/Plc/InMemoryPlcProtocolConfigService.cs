using backend.DTOs.Plc;
using backend.Interfaces.Plc;

namespace backend.Services.Plc;

public sealed class InMemoryPlcProtocolConfigService : IPlcProtocolConfigService
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

    private readonly IPlcTagAddressValidator _addressValidator;
    private readonly object _sync = new();
    private readonly List<PlcPresetDto> _presets;
    private readonly Dictionary<int, LineProtocolAssignmentDto> _assignments = new();
    private readonly Dictionary<int, List<EffectiveTagMappingDto>> _overrides = new();

    public InMemoryPlcProtocolConfigService(IPlcTagAddressValidator addressValidator)
    {
        _addressValidator = addressValidator;
        _presets = BuildSeedPresets();
    }

    public IReadOnlyCollection<PlcPresetDto> GetPresets(string? manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return _presets;
        }

        return _presets
            .Where(p => p.Manufacturer.Equals(manufacturer, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public LineProtocolAssignmentDto? GetAssignment(int lineId)
    {
        lock (_sync)
        {
            _assignments.TryGetValue(lineId, out var assignment);
            return assignment;
        }
    }

    public bool TryUpsertAssignment(int lineId, UpdateLineProtocolAssignmentRequestDto request, out string? error)
    {
        error = null;

        if (lineId <= 0)
        {
            error = "Line id must be greater than 0.";
            return false;
        }

        var preset = FindPreset(request.Manufacturer, request.PresetName, request.PresetVersion);
        if (preset is null)
        {
            error = "Preset not found for the selected manufacturer/version.";
            return false;
        }

        if (request.PollIntervalMs is < 500 or > 60000)
        {
            error = "Poll interval must be between 500 and 60000 milliseconds.";
            return false;
        }

        lock (_sync)
        {
            _assignments[lineId] = new LineProtocolAssignmentDto
            {
                LineId = lineId,
                Manufacturer = preset.Manufacturer,
                PresetName = preset.PresetName,
                PresetVersion = preset.PresetVersion,
                PollIntervalMs = request.PollIntervalMs,
                UpdatedAtUtc = DateTime.UtcNow,
            };
        }

        return true;
    }

    public IReadOnlyCollection<EffectiveTagMappingDto>? GetEffectiveTags(int lineId, out string? error)
    {
        error = null;

        LineProtocolAssignmentDto? assignment;
        List<EffectiveTagMappingDto>? overrideTags;

        lock (_sync)
        {
            _assignments.TryGetValue(lineId, out assignment);
            _overrides.TryGetValue(lineId, out overrideTags);
        }

        if (assignment is null)
        {
            error = "Protocol assignment was not found for this line.";
            return null;
        }

        var preset = FindPreset(assignment.Manufacturer, assignment.PresetName, assignment.PresetVersion);
        if (preset is null)
        {
            error = "Assigned preset was not found.";
            return null;
        }

        var effectiveTags = preset.Tags
            .Select(CloneTag)
            .ToDictionary(t => t.TagKey, StringComparer.OrdinalIgnoreCase);

        if (overrideTags is not null)
        {
            foreach (var overrideTag in overrideTags)
            {
                effectiveTags[overrideTag.TagKey] = CloneTag(overrideTag);
            }
        }

        return effectiveTags.Values
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
            && !request.Manufacturer.Equals("AllenBradley", StringComparison.OrdinalIgnoreCase)
            && !request.Manufacturer.Equals("Siemens", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new TagValidationIssueDto
            {
                Field = "manufacturer",
                Message = "Manufacturer must be AB or Siemens.",
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

        LineProtocolAssignmentDto? assignment;

        lock (_sync)
        {
            _assignments.TryGetValue(lineId, out assignment);
        }

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

        lock (_sync)
        {
            _overrides[lineId] = request.Tags.Select(CloneTag).ToList();
        }

        effectiveTags = GetEffectiveTags(lineId, out error);
        return effectiveTags is not null;
    }

    private PlcPresetDto? FindPreset(string manufacturer, string presetName, int presetVersion)
    {
        return _presets.FirstOrDefault(p =>
            p.Manufacturer.Equals(manufacturer, StringComparison.OrdinalIgnoreCase)
            && p.PresetName.Equals(presetName, StringComparison.OrdinalIgnoreCase)
            && p.PresetVersion == presetVersion);
    }

    private static EffectiveTagMappingDto CloneTag(EffectiveTagMappingDto source)
    {
        return new EffectiveTagMappingDto
        {
            TagKey = source.TagKey,
            PlcAddress = source.PlcAddress,
            DataType = source.DataType,
            Scale = source.Scale,
            IsRequired = source.IsRequired,
        };
    }

    private static List<PlcPresetDto> BuildSeedPresets()
    {
        return
        [
            new PlcPresetDto
            {
                Manufacturer = "AB",
                PresetName = "BasicStatus",
                PresetVersion = 1,
                Description = "Allen-Bradley baseline telemetry preset.",
                Tags =
                [
                    new EffectiveTagMappingDto { TagKey = "status", PlcAddress = "Program:LineData.Status", DataType = "int", Scale = 1.0m, IsRequired = true },
                    new EffectiveTagMappingDto { TagKey = "product", PlcAddress = "Program:LineData.ProductSerial", DataType = "string", Scale = 1.0m, IsRequired = true },
                    new EffectiveTagMappingDto { TagKey = "runtime_seconds", PlcAddress = "Program:LineData.RuntimeSeconds", DataType = "dint", Scale = 1.0m, IsRequired = true },
                    new EffectiveTagMappingDto { TagKey = "total_length", PlcAddress = "Program:LineData.TotalLength", DataType = "real", Scale = 1.0m, IsRequired = true },
                    new EffectiveTagMappingDto { TagKey = "control_mode", PlcAddress = "Program:LineData.ControlMode", DataType = "int", Scale = 1.0m, IsRequired = true },
                ],
            },
            new PlcPresetDto
            {
                Manufacturer = "Siemens",
                PresetName = "BasicStatus",
                PresetVersion = 1,
                Description = "Siemens baseline telemetry preset.",
                Tags =
                [
                    new EffectiveTagMappingDto { TagKey = "status", PlcAddress = "DB12.DBW0", DataType = "int", Scale = 1.0m, IsRequired = true },
                    new EffectiveTagMappingDto { TagKey = "product", PlcAddress = "DB12.DBD4", DataType = "string", Scale = 1.0m, IsRequired = true },
                    new EffectiveTagMappingDto { TagKey = "runtime_seconds", PlcAddress = "DB12.DBD12", DataType = "dint", Scale = 1.0m, IsRequired = true },
                    new EffectiveTagMappingDto { TagKey = "total_length", PlcAddress = "DB12.DBD20", DataType = "real", Scale = 1.0m, IsRequired = true },
                    new EffectiveTagMappingDto { TagKey = "control_mode", PlcAddress = "DB12.DBW24", DataType = "int", Scale = 1.0m, IsRequired = true },
                ],
            },
        ];
    }
}
