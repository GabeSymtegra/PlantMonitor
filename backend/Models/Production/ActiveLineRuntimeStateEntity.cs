namespace backend.Models.Production;

public sealed class ActiveLineRuntimeStateEntity
{
    public int LineId { get; set; }
    public string Status { get; set; } = "Offline";
    public string ControlMode { get; set; } = "Manual";
    public string CurrentProductId { get; set; } = string.Empty;
    public DateTime RunStartTimeUtc { get; set; }
    public DateTime LastTickUtc { get; set; }
    public double ProductionLength { get; set; }
    public bool HasSeenRunningState { get; set; }
    public bool HasPersistedCurrentStop { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
