import { apiDelete, apiGet } from "./api/client";
import type {
  CompletedRunReportDetail,
  CompletedRunReportRow,
  CompletedRunZoneStat,
  RuntimeEventReportRow,
} from "../models/Reports";

interface CompletedRunDto {
  id: string;
  lineId: number;
  lineNumber: number;
  lineName: string;
  productId: string;
  finalStatus: string;
  startTimeUtc: string;
  endTimeUtc: string;
  runtimeSeconds: number;
  productionLength: number;
  autoPercentage: number;
  manualPercentage: number;
  recipeId: string;
  machineId: string;
  operatorName: string;
  autoTimeSeconds: number;
  manualTimeSeconds: number;
  zoneStats: CompletedRunZoneStatDto[];
}

interface CompletedRunZoneStatDto {
  zone: string;
  segment: string;
  averageAbsoluteDeviation: number;
  maxPositiveDeviation: number;
  maxNegativeDeviation: number;
  currentDeviation: number;
}

interface RuntimeEventDto {
  id: string;
  lineId: number;
  lineNumber: number;
  lineName: string;
  eventType: string;
  previousValue: string;
  currentValue: string;
  occurredAtUtc: string;
}

export async function getCompletedRunReports(
  lineId?: number,
  take = 200
): Promise<CompletedRunReportRow[]> {
  const query = new URLSearchParams();
  query.set("take", String(take));

  if (typeof lineId === "number") {
    query.set("lineId", String(lineId));
  }

  const rows = await apiGet<CompletedRunDto[]>(`/production-runs?${query.toString()}`);

  return rows.map((row) => ({
    id: row.id,
    lineId: row.lineId,
    lineNumber: row.lineNumber,
    lineName: row.lineName,
    productId: row.productId,
    finalStatus: row.finalStatus,
    startTimeUtc: row.startTimeUtc,
    endTimeUtc: row.endTimeUtc,
    runtimeSeconds: row.runtimeSeconds,
    productionLength: row.productionLength,
    autoPercentage: row.autoPercentage,
    manualPercentage: row.manualPercentage,
  }));
}

function toCompletedRunZoneStat(row: CompletedRunZoneStatDto): CompletedRunZoneStat {
  return {
    zone: row.zone,
    segment: row.segment,
    averageAbsoluteDeviation: row.averageAbsoluteDeviation,
    maxPositiveDeviation: row.maxPositiveDeviation,
    maxNegativeDeviation: row.maxNegativeDeviation,
    currentDeviation: row.currentDeviation,
  };
}

export async function getCompletedRunReport(
  runId: string
): Promise<CompletedRunReportDetail | null> {
  try {
    const row = await apiGet<CompletedRunDto>(`/production-runs/${runId}`);

    return {
      id: row.id,
      lineId: row.lineId,
      lineNumber: row.lineNumber,
      lineName: row.lineName,
      productId: row.productId,
      finalStatus: row.finalStatus,
      startTimeUtc: row.startTimeUtc,
      endTimeUtc: row.endTimeUtc,
      runtimeSeconds: row.runtimeSeconds,
      productionLength: row.productionLength,
      autoPercentage: row.autoPercentage,
      manualPercentage: row.manualPercentage,
      recipeId: row.recipeId,
      machineId: row.machineId,
      operatorName: row.operatorName,
      autoTimeSeconds: row.autoTimeSeconds,
      manualTimeSeconds: row.manualTimeSeconds,
      zoneStats: row.zoneStats.map(toCompletedRunZoneStat),
    };
  } catch {
    return null;
  }
}

export async function deleteCompletedRunReport(runId: string): Promise<void> {
  await apiDelete(`/production-runs/${runId}`);
}

export async function getRuntimeEventReports(
  lineId?: number,
  take = 400
): Promise<RuntimeEventReportRow[]> {
  const query = new URLSearchParams();
  query.set("take", String(take));

  if (typeof lineId === "number") {
    query.set("lineId", String(lineId));
  }

  const rows = await apiGet<RuntimeEventDto[]>(`/reports/events?${query.toString()}`);

  return rows.map((row) => ({
    id: row.id,
    lineId: row.lineId,
    lineNumber: row.lineNumber,
    lineName: row.lineName,
    eventType: row.eventType,
    previousValue: row.previousValue,
    currentValue: row.currentValue,
    occurredAtUtc: row.occurredAtUtc,
  }));
}