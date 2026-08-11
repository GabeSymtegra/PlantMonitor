using backend.DTOs.System;

namespace backend.Interfaces;

public interface IConnectivityDiagnosticsService
{
    HostConnectivitySnapshotDto BuildSnapshot(string scheme, int port);
}
