import type { LineStatus } from "./LineStatus";

export type PlcManufacturer = "AllenBradley" | "Siemens";
export type LineLifecycleState =
  | "Draft"
  | "Commissioning"
  | "Active"
  | "CommissioningFailed"
  | "Disabled";

export interface ProductionLine {
  id: number;

  lineNumber: number;

  lineName: string;

  product: string;

  recipeId: string;

  machineId: string;

  operatorName: string;

  startDateTime: string;

  status: LineStatus;

  timeInStatus: string;

  controlMode: "Auto" | "Manual";

  percentAutoMode: number;

  autoVariance: number;

  percentManualMode: number;

  manualVariance: number;

  totalVariance: number;

  totalLength: number;

  runtime: string;

  plcIp: string;

  manufacturer: PlcManufacturer;

  isActive: boolean;

  lineLifecycleState: LineLifecycleState;
}