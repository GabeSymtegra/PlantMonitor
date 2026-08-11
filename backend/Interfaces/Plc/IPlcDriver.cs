using backend.DTOs.Plc;

namespace backend.Interfaces.Plc;

public interface IPlcDriver
{
    string DriverName { get; }

    Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, PlcConnectionOptionsDto? options, CancellationToken cancellationToken = default);

    Task<PlcConnectionResult> TestConnectionAsync(string ipAddress, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlcTagBrowseItemDto>> BrowseTagsAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<PlcTagReadResultDto> ReadTagAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        string tagName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlcTagReadResultDto>> ReadTagsAsync(
        string ipAddress,
        PlcConnectionOptionsDto? options,
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default);

    Task<PlcTagWriteResultDto> WriteTagAsync(
        string ipAddress,
        string tagName,
        string value,
        CancellationToken cancellationToken = default);
}