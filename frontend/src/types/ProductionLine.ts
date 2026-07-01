import type { LineStatus } from "./LineStatus";

export type PlcManufacturer = "AB" | "Siemens";

export interface ProductionLine {
  id: number;

  lineNumber: number;

  product: string;

  status: LineStatus;

  controlMode: "Auto" | "Manual";

  totalLength: number;

  runtime: string;

  plcIp: string;

  manufacturer: PlcManufacturer;

  isActive: boolean;
}