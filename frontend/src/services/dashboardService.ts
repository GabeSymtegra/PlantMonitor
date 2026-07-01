import type { ProductionLine } from "../types/ProductionLine";
import { LineStatus } from "../types/LineStatus";

export async function getProductionLines(): Promise<ProductionLine[]> {
  return [
    {
      id: 1,
      lineNumber: 1,
      product: "PVC Pipe",
      status: LineStatus.Running,
      controlMode: "Auto",
      totalLength: 15423,
      runtime: "04:12:33",
      plcIp: "192.168.1.10",
    },

    {
      id: 2,
      lineNumber: 2,
      product: "PEX Tubing",
      status: LineStatus.Stopped,
      controlMode: "Manual",
      totalLength: 8422,
      runtime: "01:22:17",
      plcIp: "192.168.1.11",
    },

    {
      id: 3,
      lineNumber: 3,
      product: "ABS Pipe",
      status: LineStatus.Faulted,
      controlMode: "Auto",
      totalLength: 2241,
      runtime: "00:18:44",
      plcIp: "192.168.1.12",
    },

    {
      id: 4,
      lineNumber: 4,
      product: "CPVC Pipe",
      status: LineStatus.Offline,
      controlMode: "Auto",
      totalLength: 0,
      runtime: "00:00:00",
      plcIp: "192.168.1.13",
    },

    {
      id: 5,
      lineNumber: 5,
      product: "PVC Conduit",
      status: LineStatus.Maintenance,
      controlMode: "Manual",
      totalLength: 623,
      runtime: "00:12:02",
      plcIp: "192.168.1.14",
    },
  ];
}