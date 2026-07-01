import type { DashboardModel } from "../models/DashboardModel";
import type { ProductionLine } from "../types/ProductionLine";
import { LineStatus } from "../types/LineStatus";

const mockLines: ProductionLine[] = [
  {
    id: 1,
    lineNumber: 1,
    product: "PVC Pipe",
    status: LineStatus.Running,
    controlMode: "Auto",
    totalLength: 15200,
    runtime: "12:43:18",
    plcIp: "192.168.1.101",
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
  },
];

export async function getDashboard(): Promise<DashboardModel> {
  return {
    lines: mockLines,
    lastUpdated: new Date(),
  };
}