import { apiPost } from "./api/client";
import type { PlcDriver } from "./plcConnectionService";

export interface PlcTagBrowseRequest {
  driver: PlcDriver;
  ipAddress: string;
  search?: string;
}

export interface PlcTagBrowseItem {
  name: string;
  dataType: string;
  isFolder: boolean;
  parentPath?: string | null;
  canRead?: boolean | null;
  canWrite?: boolean | null;
}

export interface PlcTagReadRequest {
  driver: PlcDriver;
  ipAddress: string;
  tagName: string;
}

export interface PlcTagReadResult {
  name: string;
  dataType: string;
  value?: string | null;
  lastReadUtc: string;
  canRead?: boolean | null;
  canWrite?: boolean | null;
  error?: string | null;
}

export async function browsePlcTags(
  request: PlcTagBrowseRequest
): Promise<PlcTagBrowseItem[]> {
  return apiPost<PlcTagBrowseItem[], PlcTagBrowseRequest>(
    "/admin/plc/browse-tags",
    request
  );
}

export async function readPlcTag(
  request: PlcTagReadRequest
): Promise<PlcTagReadResult> {
  return apiPost<PlcTagReadResult, PlcTagReadRequest>(
    "/admin/plc/read-tag",
    request
  );
}