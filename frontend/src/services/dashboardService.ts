import type { DashboardModel } from "../models/DashboardModel";
import type { LineDetailModel } from "../models/LineDetailModel";
import type { ProductionLine } from "../types/ProductionLine";
import { LineStatus } from "../types/LineStatus";

const STORAGE_KEY = "plantmonitor-lines";
const MOCK_START_MS = Date.now();

const defaultLines: ProductionLine[] = [
  {
    id: 1,
    lineNumber: 1,
    lineName: "North Extruder",
    product: "401-883-52-1",
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
    manufacturer: "AllenBradley",
    isActive: true,
  },
  {
    id: 2,
    lineNumber: 2,
    lineName: "South Extruder",
    product: "993-102-75-4",
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
    manufacturer: "AllenBradley",
    isActive: true,
  },
  {
    id: 3,
    lineNumber: 3,
    lineName: "Main Puller",
    product: "550-214-87-2",
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
    lineName: "Reserve Line",
    product: "120-997-61-8",
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
  const normalizedLineNumber = line.lineNumber ?? fallbackId;
  const normalizedManufacturer =
    typeof line.manufacturer === "string" && line.manufacturer.trim().toLowerCase() === "ab"
      ? "AllenBradley"
      : line.manufacturer ?? "AllenBradley";

  return {
    id: line.id ?? fallbackId,
    lineNumber: normalizedLineNumber,
    lineName: line.lineName?.trim() || `Line ${normalizedLineNumber}`,
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
    manufacturer: normalizedManufacturer,
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

export async function getLineById(id: number): Promise<ProductionLine | null> {
  const lines = applyMockTelemetry(getStoredLines());
  return lines.find((line) => line.id === id) ?? null;
}

function buildDiameterSensors(line: ProductionLine): LineDetailModel["diameterSensors"] {
  return Array.from({ length: 6 }, (_, index) => {
    const sensorId = index + 1;
    const pulse = Math.sin((Date.now() + line.id * 1400 + sensorId * 600) / 6500);
    const deviationMm = Number((pulse * 0.6).toFixed(3));
    const absoluteDeviation = Math.abs(deviationMm);

    let status: "Normal" | "Warning" | "Fault" = "Normal";

    if (absoluteDeviation > 0.5) {
      status = "Fault";
    } else if (absoluteDeviation > 0.25) {
      status = "Warning";
    }

    return {
      id: `DIA-${line.id}-${sensorId}`,
      label: `Diameter Sensor ${sensorId}`,
      diameterMm: Number((45 + sensorId * 0.4 + pulse * 0.2).toFixed(3)),
      status,
      deviationMm,
    };
  });
}

export async function getLineDetail(id: number): Promise<LineDetailModel | null> {
  const line = await getLineById(id);

  if (!line) {
    return null;
  }

  const phase = (Date.now() + line.id * 1000) / 5000;

  return {
    line,
    pressuresPsi: {
      extruder: Number((168 + Math.sin(phase) * 7).toFixed(1)),
      dieHead: Number((142 + Math.cos(phase * 0.9) * 6).toFixed(1)),
      cooling: Number((78 + Math.sin(phase * 1.2) * 4).toFixed(1)),
    },
    temperaturesC: {
      zone1: Number((188 + Math.sin(phase * 0.8) * 3).toFixed(1)),
      zone2: Number((194 + Math.cos(phase * 0.7) * 3).toFixed(1)),
      zone3: Number((201 + Math.sin(phase * 0.6) * 2.5).toFixed(1)),
      die: Number((206 + Math.cos(phase * 0.65) * 2).toFixed(1)),
    },
    motorSpeedsRpm: {
      puller: Number((1200 + Math.sin(phase) * 65).toFixed(0)),
      cutter: Number((980 + Math.cos(phase * 1.1) * 55).toFixed(0)),
    },
    diameterSensors: buildDiameterSensors(line),
    updatedAt: new Date().toISOString(),
  };
}