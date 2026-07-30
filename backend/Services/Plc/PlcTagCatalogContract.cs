using backend.DTOs.Plc;
using System.Globalization;
using System.Text.RegularExpressions;

namespace backend.Services.Plc;

public static class PlcTagCatalogContract
{
    private static readonly HashSet<string> AllowedCatalogDataTypes =
    [
        "bool",
        "int",
        "dint",
        "real",
        "string",
        "sint",
        "lint",
        "lreal",
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
        "sint",
        "lint",
    ];

    private static readonly HashSet<string> ProductIdDataTypes =
    [
        "string",
        "int",
        "dint",
        "sint",
        "lint",
    ];

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

    public static readonly IReadOnlyList<TagSlotDefinitionDto> RequiredTagSlots =
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

    public static string AllowedCatalogTypeListForMessages => "bool, int, dint, real, string, sint, lint, lreal";

    public static string NormalizeDataType(string rawDataType)
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

    public static bool IsAllowedCatalogDataType(string? rawDataType)
    {
        var normalized = NormalizeDataType(rawDataType ?? string.Empty);
        return AllowedCatalogDataTypes.Contains(normalized);
    }

    public static bool IsAutoMappableDataType(string? rawDataType)
    {
        return IsAllowedCatalogDataType(rawDataType);
    }

    public static bool IsNumericLogicalKey(string logicalKey)
    {
        return NumericLogicalKeys.Contains(logicalKey);
    }

    public static bool IsLogicalKeyCompatible(string logicalKey, string? rawDataType)
    {
        var normalizedDataType = NormalizeDataType(rawDataType ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedDataType) || normalizedDataType == "unknown")
        {
            return false;
        }

        if (IsNumericLogicalKey(logicalKey))
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

    public static bool TryConvertToExpectedRuntimeValue(string logicalKey, string rawValue, out string error)
    {
        error = string.Empty;
        var trimmed = rawValue.Trim();

        if (IsNumericLogicalKey(logicalKey))
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

    public static bool IsTextOrCodeType(string? rawDataType)
    {
        return TextOrCodeDataTypes.Contains(NormalizeDataType(rawDataType ?? string.Empty));
    }

    public static bool IsProductIdType(string? rawDataType)
    {
        return ProductIdDataTypes.Contains(NormalizeDataType(rawDataType ?? string.Empty));
    }

    public static bool IsNumericType(string? rawDataType)
    {
        return NumericDataTypes.Contains(NormalizeDataType(rawDataType ?? string.Empty));
    }

    public static IReadOnlyCollection<string> GetAliases(string logicalKey)
    {
        return logicalKey switch
        {
            "line_id" => ["line_id", "lineid"],
            "product_id" => ["product_id", "productid"],
            "control_mode" => ["control_status", "controlstatus", "control_mode", "controlmode"],
            "machine_state" => ["machine_state", "machinestate"],
            "production_length" => ["production_length", "productionlength", "total_length", "totallength", "line_length", "linelength", "length_count", "lengthcounter"],
            "bare_setpoint" => ["bare_od_sp", "bareodsp", "bare_setpoint", "bareodsetpoint", "baretarget"],
            "bare_actual" => ["bare_od_act", "bareodact", "bare_actual", "bareodactual", "baremeasured", "bareact"],
            "hot_setpoint" => ["hot_od_sp", "hotodsp", "hot_setpoint", "hotodsetpoint", "hottarget"],
            "hot_actual" => ["hot_od_act", "hotodact", "hot_actual", "hotodactual", "hotmeasured", "hotact"],
            "cold_setpoint" => ["cold_od_sp", "coldodsp", "cold_setpoint", "coldodsetpoint", "coldtarget"],
            "cold_actual" => ["cold_od_act", "coldodact", "cold_actual", "coldodactual", "coldmeasured", "coldact"],
            _ => [logicalKey],
        };
    }

    public static string NormalizeTagName(string tagName)
    {
        return Regex.Replace(tagName.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
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
}
