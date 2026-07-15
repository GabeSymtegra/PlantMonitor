import { API_ENDPOINTS } from "../constants/api";
import type { DashboardModel } from "../models/DashboardModel";
import type { GaugeDetail, LineDetailModel } from "../models/LineDetailModel";
import type { ProductionLine } from "../types/ProductionLine";
import { LineStatus } from "../types/LineStatus";
import { apiDelete, apiGet, apiPost, apiPut } from "./api/client";

// -----------------------------------------------------------------------------
// Backend DTOs used only by the frontend data layer
// -----------------------------------------------------------------------------

interface RuntimeDashboardLineDto {
  id: number;
  lineNumber: number;
  lineName: string;
  productId: string;
  startTimeUtc?: string;
  status: string;
  controlMode: "Auto" | "Manual";
  totalLength: number;
  runtimeSeconds: number;
  plcIp: string;
  manufacturer: string;
  updatedAtUtc: string;
  percentAutoMode: number;
  percentManualMode: number;
  autoModeVariance?: number;
  manualModeVariance?: number;
  totalVariance?: number;
  bareAverageDeviation?: number;
  hotAverageDeviation?: number;
  coldAverageDeviation?: number;
}

interface RuntimeDashboardSnapshotDto {
  lines: RuntimeDashboardLineDto[];
  lastUpdatedUtc: string;
}

interface RuntimeSensorDetailDto {
  zone: string;
  currentSetpoint: number;
  currentActual: number;
  currentPercentDeviation: number;
  overallMeasurementCount: number;
  overallAverageAbsoluteDeviation: number;
  overallMaxPositiveDeviation: number;
  overallMaxNegativeDeviation: number;
  autoMeasurementCount: number;
  autoAverageAbsoluteDeviation: number;
  autoMaxPositiveDeviation: number;
  autoMaxNegativeDeviation: number;
  manualMeasurementCount: number;
  manualAverageAbsoluteDeviation: number;
  manualMaxPositiveDeviation: number;
  manualMaxNegativeDeviation: number;
}

interface RuntimeLineDetailDto {
  id: number;
  lineNumber: number;
  lineName: string;
  productId: string;
  recipeId: string;
  machineId: string;
  operatorName: string;
  plcIp: string;
  manufacturer: string;
  status: string;
  controlMode: "Auto" | "Manual";
  lastUpdatedUtc: string;
  startTimeUtc: string;
  currentProductionLength: number;
  runtimeSeconds: number;
  autoTimeSeconds: number;
  manualTimeSeconds: number;
  autoPercentage: number;
  manualPercentage: number;
  sensors: RuntimeSensorDetailDto[];
}

interface LineConfigDto {
  id: number;
  lineNumber: number;
  lineName: string;
  productId: string;
  plcIp: string;
  manufacturer: string;
  pollIntervalMs: number;
  isActive: boolean;
  updatedAtUtc: string;
}

interface UpsertLineConfigRequestDto {
  lineNumber: number;
  lineName: string;
  productId: string;
  plcIp: string;
  manufacturer: string;
  pollIntervalMs: number;
  isActive: boolean;
}

// -----------------------------------------------------------------------------
// DTO -> UI model mapping helpers
// -----------------------------------------------------------------------------

function toLineStatus(value: string): LineStatus {
  switch (value) {
    case LineStatus.Running:
      return LineStatus.Running;
    case LineStatus.Stopped:
      return LineStatus.Stopped;
    case LineStatus.Bleedout:
      return LineStatus.Bleedout;
    case LineStatus.Startup:
      return LineStatus.Startup;
    case LineStatus.Faulted:
      return LineStatus.Faulted;
    case LineStatus.Maintenance:
      return LineStatus.Maintenance;
    default:
      return LineStatus.Offline;
  }
}

function formatRuntime(seconds: number): string {
  const safeSeconds = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(safeSeconds / 3600)
    .toString()
    .padStart(2, "0");
  const minutes = Math.floor((safeSeconds % 3600) / 60)
    .toString()
    .padStart(2, "0");
  const secs = (safeSeconds % 60).toString().padStart(2, "0");

  return `${hours}:${minutes}:${secs}`;
}

function toProductionLine(line: RuntimeDashboardLineDto): ProductionLine {
  const startDateTime = line.startTimeUtc ?? line.updatedAtUtc;
  const autoVariance = line.autoModeVariance ?? line.bareAverageDeviation ?? 0;
  const manualVariance = line.manualModeVariance ?? line.hotAverageDeviation ?? 0;
  const totalVariance = line.totalVariance ?? line.coldAverageDeviation ?? 0;

  return {
    id: line.id,
    lineNumber: line.lineNumber,
    lineName: line.lineName,
    product: line.productId,
    startDateTime,
    status: toLineStatus(line.status),
    timeInStatus: formatRuntime(line.runtimeSeconds),
    controlMode: line.controlMode,
    percentAutoMode: line.percentAutoMode,
    autoVariance,
    percentManualMode: line.percentManualMode,
    manualVariance,
    totalVariance,
    totalLength: line.totalLength,
    runtime: formatRuntime(line.runtimeSeconds),
    plcIp: line.plcIp,
    manufacturer:
      line.manufacturer === "Siemens" ? "Siemens" : "AllenBradley",
    isActive: line.status !== "Offline",
  };
}

// Configured lines are used as a fallback so the dashboard can still show a
// known line definition even when runtime data is not available yet.
function toConfiguredLine(line: LineConfigDto): ProductionLine {
  return {
    id: line.id,
    lineNumber: line.lineNumber,
    lineName: line.lineName,
    product: line.productId,
    startDateTime: "??",
    status: LineStatus.Offline,
    timeInStatus: "??",
    controlMode: "Manual",
    percentAutoMode: Number.NaN,
    autoVariance: Number.NaN,
    percentManualMode: Number.NaN,
    manualVariance: Number.NaN,
    totalVariance: Number.NaN,
    totalLength: Number.NaN,
    runtime: "??",
    plcIp: line.plcIp,
    manufacturer: line.manufacturer === "Siemens" ? "Siemens" : "AllenBradley",
    isActive: line.isActive,
  };
}

export function toConfiguredFallbackLine(line: ProductionLine): ProductionLine {
  return {
    ...line,
    startDateTime: "??",
    status: LineStatus.Offline,
    timeInStatus: "??",
    controlMode: line.controlMode ?? "Manual",
    percentAutoMode: Number.NaN,
    autoVariance: Number.NaN,
    percentManualMode: Number.NaN,
    manualVariance: Number.NaN,
    totalVariance: Number.NaN,
    totalLength: Number.NaN,
    runtime: "??",
  };
}

function toGaugeDetail(sensor: RuntimeSensorDetailDto): GaugeDetail {
  return {
    zone: sensor.zone,
    currentSetpoint: sensor.currentSetpoint,
    currentActual: sensor.currentActual,
    currentPercentDeviation: sensor.currentPercentDeviation,
    overallMeasurementCount: sensor.overallMeasurementCount,
    overallAverageAbsoluteDeviation: sensor.overallAverageAbsoluteDeviation,
    overallMaxPositiveDeviation: sensor.overallMaxPositiveDeviation,
    overallMaxNegativeDeviation: sensor.overallMaxNegativeDeviation,
    autoMeasurementCount: sensor.autoMeasurementCount,
    autoAverageAbsoluteDeviation: sensor.autoAverageAbsoluteDeviation,
    autoMaxPositiveDeviation: sensor.autoMaxPositiveDeviation,
    autoMaxNegativeDeviation: sensor.autoMaxNegativeDeviation,
    manualMeasurementCount: sensor.manualMeasurementCount,
    manualAverageAbsoluteDeviation: sensor.manualAverageAbsoluteDeviation,
    manualMaxPositiveDeviation: sensor.manualMaxPositiveDeviation,
    manualMaxNegativeDeviation: sensor.manualMaxNegativeDeviation,
  };
}

// -----------------------------------------------------------------------------
// CRUD and runtime query functions
// -----------------------------------------------------------------------------

export async function getAllLines(): Promise<ProductionLine[]> {
  const dto = await apiGet<LineConfigDto[]>(API_ENDPOINTS.lines);
  return dto.map(toConfiguredLine);
}

export async function addLine(
  line: Omit<ProductionLine, "id">
): Promise<ProductionLine> {
  const request: UpsertLineConfigRequestDto = {
    lineNumber: line.lineNumber,
    lineName: line.lineName,
    productId: line.product,
    plcIp: line.plcIp,
    manufacturer: line.manufacturer,
    pollIntervalMs: 2000,
    isActive: line.isActive,
  };

  const created = await apiPost<LineConfigDto, UpsertLineConfigRequestDto>(API_ENDPOINTS.lines, request);
  return toConfiguredLine(created);
}

export async function updateLine(
  id: number,
  updates: Partial<ProductionLine>
): Promise<ProductionLine> {
  const current = await getAllLines();
  const existingLine = current.find((line) => line.id === id);

  if (!existingLine) {
    throw new Error("Line not found");
  }

  const request: UpsertLineConfigRequestDto = {
    lineNumber: updates.lineNumber ?? existingLine.lineNumber,
    lineName: updates.lineName ?? existingLine.lineName,
    productId: updates.product ?? existingLine.product,
    plcIp: updates.plcIp ?? existingLine.plcIp,
    manufacturer: updates.manufacturer ?? existingLine.manufacturer,
    pollIntervalMs: 2000,
    isActive: updates.isActive ?? existingLine.isActive,
  };

  const updated = await apiPut<LineConfigDto, UpsertLineConfigRequestDto>(API_ENDPOINTS.line(id), request);
  return toConfiguredLine(updated);
}

export async function deleteLine(id: number): Promise<void> {
  await apiDelete(API_ENDPOINTS.line(id));
}

export async function getDashboard(): Promise<DashboardModel> {
  const dto = await apiGet<RuntimeDashboardSnapshotDto>(API_ENDPOINTS.dashboard);
  const runtimeLines = dto.lines.map(toProductionLine);
  const runtimeById = new Map(runtimeLines.map((line) => [line.id, line]));

  // Active configured lines still appear in the UI even if they have not yet
  // produced a live runtime snapshot.
  const configuredActiveLines = (await getAllLines()).filter((line) => line.isActive);

  for (const configuredLine of configuredActiveLines) {
    if (!runtimeById.has(configuredLine.id)) {
      runtimeLines.push(toConfiguredFallbackLine(configuredLine));
    }
  }

  runtimeLines.sort((left, right) => left.lineNumber - right.lineNumber);

  return {
    lines: runtimeLines,
    lastUpdated: new Date(dto.lastUpdatedUtc),
  };
}

export async function getLineById(id: number): Promise<ProductionLine | null> {
  const dashboard = await getDashboard();
  return dashboard.lines.find((line) => line.id === id) ?? null;
}

export async function getLineDetail(id: number): Promise<LineDetailModel | null> {
  try {
    const dto = await apiGet<RuntimeLineDetailDto>(API_ENDPOINTS.lineDetails(id));

    return {
      id: dto.id,
      lineNumber: dto.lineNumber,
      lineName: dto.lineName,
      productId: dto.productId,
      recipeId: dto.recipeId,
      machineId: dto.machineId,
      operatorName: dto.operatorName,
      plcIp: dto.plcIp,
      manufacturer: dto.manufacturer,
      status: toLineStatus(dto.status),
      controlMode: dto.controlMode,
      lastUpdated: dto.lastUpdatedUtc,
      startTime: dto.startTimeUtc,
      currentProductionLength: dto.currentProductionLength,
      runtimeSeconds: dto.runtimeSeconds,
      autoTimeSeconds: dto.autoTimeSeconds,
      manualTimeSeconds: dto.manualTimeSeconds,
      autoPercentage: dto.autoPercentage,
      manualPercentage: dto.manualPercentage,
      gauges: dto.sensors.map(toGaugeDetail),
    };
  } catch {
    return null;
  }
}
