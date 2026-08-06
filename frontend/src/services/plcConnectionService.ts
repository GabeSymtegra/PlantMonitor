import { apiPost } from "./api/client";

export type PlcDriver = "AllenBradley" | "Siemens";
export type PlcProcessorType =
  | "ControlLogix"
  | "CompactLogix"
  | "Micro800"
  | "S7-1217C"
  | "S7-1200"
  | "S7-1500";

export interface PlcConnectionRequest {
  driver: PlcDriver;
  ipAddress: string;
  options?: PlcConnectionOptions;
}

export interface PlcConnectionOptions {
  routePath: string;
  processorType: PlcProcessorType;
  rack?: number;
  slot?: number;
  connectionTimeoutMs: number;
  readTimeoutMs: number;
  retryCount: number;
  retryDelayMs: number;
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