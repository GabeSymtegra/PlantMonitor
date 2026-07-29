using System.Text.RegularExpressions;
using backend.Interfaces.Plc;

namespace backend.Services.Plc;

public sealed class PlcTagAddressValidator : IPlcTagAddressValidator
{
    private const string AllenBradleyManufacturer = "AllenBradley";
    private const string AllenBradleyInvalidMessage = "AB addresses must match Program:Scope.Tag or Tag.SubTag format.";
    private const string UnsupportedManufacturerMessage = "Unsupported manufacturer. Use AllenBradley.";

    private static readonly Regex AllenBradleyRegex = new(
        @"^(Program:[A-Za-z_][\w]*\.)?[A-Za-z_][\w]*(\.[A-Za-z_][\w]*|\[\d+\])*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SiemensDbRegex = new(
        @"^DB\d+\.DB(X|B|W|D)\d+(\.\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SiemensAreaRegex = new(
        @"^[MIQ]\d+(\.\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public bool IsValidAddress(string manufacturer, string plcAddress, out string message)
    {
        var normalizedManufacturer = manufacturer.Trim();
        var normalizedAddress = plcAddress.Trim();

        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            message = "PLC address is required.";
            return false;
        }

        if (IsAllenBradley(normalizedManufacturer))
        {
            return Validate(normalizedAddress, AllenBradleyRegex, AllenBradleyInvalidMessage, out message);
        }

        message = UnsupportedManufacturerMessage;
        return false;
    }

    private static bool IsAllenBradley(string manufacturer)
    {
        return manufacturer.Equals("AB", StringComparison.OrdinalIgnoreCase)
            || manufacturer.Equals(AllenBradleyManufacturer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Validate(string plcAddress, Regex pattern, string invalidMessage, out string message)
    {
        var valid = pattern.IsMatch(plcAddress);
        message = valid ? string.Empty : invalidMessage;
        return valid;
    }
}
