export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  skip: number;
  take: number;
}

export interface CompletedRunReportRow {
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

export interface CompletedRunZoneStat {
  zone: string;
  segment: string;
  averageAbsoluteDeviation: number;
  maxPositiveDeviation: number;
  maxNegativeDeviation: number;
  currentDeviation: number;
}

export interface CompletedRunReportDetail extends CompletedRunReportRow {
  recipeId: string;
  machineId: string;
  operatorName: string;
  autoTimeSeconds: number;
  manualTimeSeconds: number;
  zoneStats: CompletedRunZoneStat[];
}

export interface RuntimeEventReportRow {
  id: string;
  lineId: number;
  lineNumber: number;
  lineName: string;
  eventType: string;
  previousValue: string;
  currentValue: string;
  occurredAtUtc: string;
}