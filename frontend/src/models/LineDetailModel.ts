import type { LineStatus } from "../types/LineStatus";

export interface SensorDetail {
  zone: string;
  currentSetpoint: number;
  currentActual: number;
  currentPercentDeviation: number;
  overallMeasurementCount: number;
  overallAverageAbsoluteDeviation: number;
  overallMaxPositiveDeviation: number;
  overallMaxNegativeDeviation: number;
  autoMeasurementCount: number;
  autoAverageAbsoluteDeviation: number;
  autoMaxPositiveDeviation: number;
  autoMaxNegativeDeviation: number;
  manualMeasurementCount: number;
  manualAverageAbsoluteDeviation: number;
  manualMaxPositiveDeviation: number;
  manualMaxNegativeDeviation: number;
}

export interface LineDetailModel {
  id: number;
  lineNumber: number;
  lineName: string;
  productId: string;
  recipeId: string;
  machineId: string;
  operatorName: string;
  plcIp: string;
  manufacturer: string;
  status: LineStatus;
  controlMode: "Auto" | "Manual";
  lastUpdated: string;
  startTime: string;
  currentProductionLength: number;
  runtimeSeconds: number;
  autoTimeSeconds: number;
  manualTimeSeconds: number;
  autoPercentage: number;
  manualPercentage: number;
  sensors: SensorDetail[];
}
