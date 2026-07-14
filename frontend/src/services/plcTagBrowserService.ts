import { apiGet, apiPost, apiPut } from "./api/client";
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

export interface TagSlotDefinition {
  logicalKey: string;
  displayName: string;
  isRequired: boolean;
  description?: string | null;
}

export interface LineTagCatalogEntry {
  logicalKey: string;
  displayName: string;
  driver: string;
  plcAddress: string;
  dataType: string;
  unit?: string | null;
  scale: number;
  description?: string | null;
  isEnabled: boolean;
  sortOrder: number;
  readFrequencyMs: number;
  isRequired: boolean;
}

export interface AutoMapTagCatalogRequest {
  driver: PlcDriver;
  ipAddress: string;
}

export interface AutoMapTagCatalogResult {
  driver: string;
  scannedTagCount: number;
  suggestedMappings: LineTagCatalogEntry[];
  missingLogicalKeys: string[];
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

export async function getTagSlots(): Promise<TagSlotDefinition[]> {
  return apiGet<TagSlotDefinition[]>("/admin/plc/tag-slots");
}

export async function getLineTagCatalog(lineId: number): Promise<LineTagCatalogEntry[]> {
  return apiGet<LineTagCatalogEntry[]>(`/admin/lines/${lineId}/tag-catalog`);
}

export async function replaceLineTagCatalog(
  lineId: number,
  driver: PlcDriver,
  tags: LineTagCatalogEntry[]
): Promise<LineTagCatalogEntry[]> {
  return apiPut<LineTagCatalogEntry[], { driver: PlcDriver; tags: LineTagCatalogEntry[] }>(
    `/admin/lines/${lineId}/tag-catalog`,
    {
      driver,
      tags,
    }
  );
}

export async function autoMapTagCatalog(
  request: AutoMapTagCatalogRequest
): Promise<AutoMapTagCatalogResult> {
  return apiPost<AutoMapTagCatalogResult, AutoMapTagCatalogRequest>(
    "/admin/plc/auto-map-tags",
    request
  );
}