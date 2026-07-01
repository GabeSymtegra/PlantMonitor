import type { LineStatus } from "./LineStatus";

export interface ProductionLine {
  id: number;

  lineNumber: number;

  product: string;

  status: LineStatus;

  controlMode: "Auto" | "Manual";

  totalLength: number;

  runtime: string;

  plcIp: string;
}