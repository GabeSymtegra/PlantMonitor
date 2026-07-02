namespace backend.Interfaces.Plc;

public interface IPlcTagAddressValidator
{
    bool IsValidAddress(string manufacturer, string plcAddress, out string message);
}
