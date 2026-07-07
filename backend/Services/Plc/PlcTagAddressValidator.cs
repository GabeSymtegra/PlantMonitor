using System.Text.RegularExpressions;
using backend.Interfaces.Plc;

namespace backend.Services.Plc;

public sealed class PlcTagAddressValidator : IPlcTagAddressValidator
{
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

        if (normalizedManufacturer.Equals("AB", StringComparison.OrdinalIgnoreCase)
            || normalizedManufacturer.Equals("AllenBradley", StringComparison.OrdinalIgnoreCase))
        {
            var valid = AllenBradleyRegex.IsMatch(normalizedAddress);
            message = valid
                ? string.Empty
                : "AB addresses must match Program:Scope.Tag or Tag.SubTag format.";
            return valid;
        }

        if (normalizedManufacturer.Equals("Siemens", StringComparison.OrdinalIgnoreCase))
        {
            var valid = SiemensDbRegex.IsMatch(normalizedAddress) || SiemensAreaRegex.IsMatch(normalizedAddress);
            message = valid
                ? string.Empty
                : "Siemens addresses must match DBx.DBW0/DBD0/DBX0.0 or M/I/Q area format.";
            return valid;
        }

        message = "Unsupported manufacturer. Use AllenBradley or Siemens.";
        return false;
    }
}
