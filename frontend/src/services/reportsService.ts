import { apiGet } from "./api/client";
import type {
  CompletedRunReportRow,
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