import { apiPost } from "./api/client";

export type PlcDriver = "AllenBradley" | "Siemens";

export interface PlcConnectionRequest {
  driver: PlcDriver;
  ipAddress: string;
}

export interface PlcConnectionResult {
  isConnected: boolean;
  driver: PlcDriver;
  ipAddress: string;
  controllerName?: string | null;
  firmware?: string | null;
  responseTimeMs?: number | null;
  message: string;
}

export async function testPlcConnection(
  request: PlcConnectionRequest
): Promise<PlcConnectionResult> {
  return apiPost<PlcConnectionResult, PlcConnectionRequest>(
    "/admin/plc/test-connection",
    request
  );
}