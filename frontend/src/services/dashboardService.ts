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
    status: LineStatus.Running,
    controlMode: "Auto",
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
    status: LineStatus.Stopped,
    controlMode: "Manual",
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
    status: LineStatus.Faulted,
    controlMode: "Auto",
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
    status: LineStatus.Offline,
    controlMode: "Auto",
    totalLength: 0,
    runtime: "00:00:00",
    plcIp: "192.168.1.104",
    manufacturer: "Siemens",
    isActive: true,
  },
];

function getStoredLines(): ProductionLine[] {
  const serialized = localStorage.getItem(STORAGE_KEY);

  if (!serialized) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(defaultLines));
    return [...defaultLines];
  }

  try {
    const parsed = JSON.parse(serialized) as ProductionLine[];

    if (!Array.isArray(parsed)) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(defaultLines));
      return [...defaultLines];
    }

    return parsed;
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
    if (line.status !== LineStatus.Running) {
      return line;
    }

    const runtimeSeconds = parseRuntime(line.runtime);
    const runtimeIncrement = elapsedSeconds;
    const lengthIncrement = elapsedSeconds * 8;

    return {
      ...line,
      runtime: formatRuntime(runtimeSeconds + runtimeIncrement),
      totalLength: line.totalLength + lengthIncrement,
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