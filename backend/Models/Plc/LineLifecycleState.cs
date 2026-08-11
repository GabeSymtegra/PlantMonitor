namespace backend.Models.Plc;

public static class LineLifecycleState
{
    public const string Draft = "Draft";
    public const string Commissioning = "Commissioning";
    public const string Active = "Active";
    public const string CommissioningFailed = "CommissioningFailed";
    public const string Disabled = "Disabled";

    public static bool IsKnown(string? value)
    {
        return value is Draft
            or Commissioning
            or Active
            or CommissioningFailed
            or Disabled;
    }
}