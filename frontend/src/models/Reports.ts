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