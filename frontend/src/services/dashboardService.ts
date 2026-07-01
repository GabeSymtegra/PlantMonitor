import type { DashboardModel } from "../models/DashboardModel";
import type { ProductionLine } from "../types/ProductionLine";
import { LineStatus } from "../types/LineStatus";

const STORAGE_KEY = "plantmonitor-lines";
const MOCK_START_MS = Date.now();

const defaultLines: ProductionLine[] = [
  {
    id: 1,
    lineNumber: 1,
    product: "PVC Pipe",
    startDateTime: "2026-07-01T06:00:00",
    status: LineStatus.Running,
    timeInStatus: "02:12:14",
    controlMode: "Auto",
    percentAutoMode: 94.2,
    autoVariance: 1.8,
    percentManualMode: 5.8,
    manualVariance: 0.6,
    totalVariance: 2.4,
    totalLength: 15200,
    runtime: "12:43:18",
    plcIp: "192.168.1.101",
    manufacturer: "AB",
    isActive: true,
  },
  {
    id: 2,
    lineNumber: 2,
    product: "ABS Pipe",
    startDateTime: "2026-07-01T07:20:00",
    status: LineStatus.Stopped,
    timeInStatus: "00:18:09",
    controlMode: "Manual",
    percentAutoMode: 58.3,
    autoVariance: 3.2,
    percentManualMode: 41.7,
    manualVariance: 2.8,
    totalVariance: 6.0,
    totalLength: 9840,
    runtime: "08:15:44",
    plcIp: "192.168.1.102",
    manufacturer: "AB",
    isActive: true,
  },
  {
    id: 3,
    lineNumber: 3,
    product: "PEX Tubing",
    startDateTime: "2026-07-01T05:45:00",
    status: LineStatus.Faulted,
    timeInStatus: "00:04:12",
    controlMode: "Auto",
    percentAutoMode: 88.1,
    autoVariance: 4.1,
    percentManualMode: 11.9,
    manualVariance: 1.7,
    totalVariance: 5.8,
    totalLength: 12350,
    runtime: "15:22:09",
    plcIp: "192.168.1.103",
    manufacturer: "Siemens",
    isActive: true,
  },
  {
    id: 4,
    lineNumber: 4,
    product: "HDPE Pipe",
    startDateTime: "2026-07-01T00:00:00",
    status: LineStatus.Offline,
    timeInStatus: "03:02:10",
    controlMode: "Auto",
    percentAutoMode: 0,
    autoVariance: 0,
    percentManualMode: 0,
    manualVariance: 0,
    totalVariance: 0,
    totalLength: 0,
    runtime: "00:00:00",
    plcIp: "192.168.1.104",
    manufacturer: "Siemens",
    isActive: true,
  },
];

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function toFixedNumber(value: number, decimals = 1): number {
  return Number(value.toFixed(decimals));
}

function normalizeLine(line: Partial<ProductionLine>, fallbackId: number): ProductionLine {
  return {
    id: line.id ?? fallbackId,
    lineNumber: line.lineNumber ?? fallbackId,
    product: line.product ?? "Unassigned",
    startDateTime: line.startDateTime ?? new Date().toISOString(),
    status: line.status ?? LineStatus.Offline,
    timeInStatus: line.timeInStatus ?? "00:00:00",
    controlMode: line.controlMode ?? "Auto",
    percentAutoMode: line.percentAutoMode ?? 0,
    autoVariance: line.autoVariance ?? 0,
    percentManualMode: line.percentManualMode ?? 0,
    manualVariance: line.manualVariance ?? 0,
    totalVariance: line.totalVariance ?? 0,
    totalLength: line.totalLength ?? 0,
    runtime: line.runtime ?? "00:00:00",
    plcIp: line.plcIp ?? "0.0.0.0",
    manufacturer: line.manufacturer ?? "AB",
    isActive: line.isActive ?? true,
  };
}

function getStoredLines(): ProductionLine[] {
  const serialized = localStorage.getItem(STORAGE_KEY);

  if (!serialized) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(defaultLines));
    return [...defaultLines];
  }

  try {
    const parsed = JSON.parse(serialized) as Partial<ProductionLine>[];

    if (!Array.isArray(parsed)) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(defaultLines));
      return [...defaultLines];
    }

    const normalizedLines = parsed.map((line, index) =>
      normalizeLine(line, index + 1)
    );

    setStoredLines(normalizedLines);

    return normalizedLines;
  } catch {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(defaultLines));
    return [...defaultLines];
  }
}

function setStoredLines(lines: ProductionLine[]) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(lines));
}

function parseRuntime(runtime: string): number {
  const parts = runtime.split(":").map((part) => Number(part));

  if (parts.length !== 3 || parts.some((part) => Number.isNaN(part))) {
    return 0;
  }

  return parts[0] * 3600 + parts[1] * 60 + parts[2];
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

function applyMockTelemetry(lines: ProductionLine[]): ProductionLine[] {
  const elapsedSeconds = Math.floor((Date.now() - MOCK_START_MS) / 2);

  return lines.map((line) => {
    const timeInStatusSeconds = parseRuntime(line.timeInStatus);
    const timeInStatusIncrement = line.status === LineStatus.Running ? elapsedSeconds : 0;
    const runtimeSeconds = parseRuntime(line.runtime);
    const runtimeIncrement = line.status === LineStatus.Running ? elapsedSeconds : 0;
    const lengthIncrement = line.status === LineStatus.Running ? elapsedSeconds * 8 : 0;

    const controlModePulse = Math.sin((Date.now() + line.id * 3000) / 12000);

    const percentAutoMode =
      line.controlMode === "Auto"
        ? clamp(90 + controlModePulse * 6, 75, 100)
        : clamp(35 + controlModePulse * 8, 0, 60);

    const percentManualMode = clamp(100 - percentAutoMode, 0, 100);
    const autoVariance = clamp(1.5 + Math.abs(controlModePulse) * 2.4, 0, 10);
    const manualVariance = clamp(0.9 + Math.abs(controlModePulse) * 2.1, 0, 10);
    const totalVariance = clamp(autoVariance + manualVariance, 0, 20);

    return {
      ...line,
      timeInStatus: formatRuntime(timeInStatusSeconds + timeInStatusIncrement),
      runtime: formatRuntime(runtimeSeconds + runtimeIncrement),
      totalLength: line.totalLength + lengthIncrement,
      percentAutoMode: toFixedNumber(percentAutoMode, 1),
      autoVariance: toFixedNumber(autoVariance, 2),
      percentManualMode: toFixedNumber(percentManualMode, 1),
      manualVariance: toFixedNumber(manualVariance, 2),
      totalVariance: toFixedNumber(totalVariance, 2),
    };
  });
}

export async function getAllLines(): Promise<ProductionLine[]> {
  return getStoredLines();
}

export async function addLine(
  line: Omit<ProductionLine, "id">
): Promise<ProductionLine> {
  const lines = getStoredLines();
  const nextId = lines.length ? Math.max(...lines.map((item) => item.id)) + 1 : 1;

  const createdLine: ProductionLine = {
    ...line,
    id: nextId,
  };

  setStoredLines([...lines, createdLine]);

  return createdLine;
}

export async function updateLine(
  id: number,
  updates: Partial<ProductionLine>
): Promise<ProductionLine> {
  const lines = getStoredLines();
  const existingLine = lines.find((line) => line.id === id);

  if (!existingLine) {
    throw new Error("Line not found");
  }

  const updatedLine: ProductionLine = {
    ...existingLine,
    ...updates,
    id,
  };

  setStoredLines(lines.map((line) => (line.id === id ? updatedLine : line)));

  return updatedLine;
}

export async function getDashboard(): Promise<DashboardModel> {
  const activeLines = getStoredLines().filter((line) => line.isActive);
  const lines = applyMockTelemetry(activeLines);

  return {
    lines,
    lastUpdated: new Date(),
  };
}