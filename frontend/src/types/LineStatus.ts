export const LineStatus = {
  Running: "Running",
  Stopped: "Stopped",
  Faulted: "Faulted",
  Offline: "Offline",
  Maintenance: "Maintenance",
} as const;

export type LineStatus =
  (typeof LineStatus)[keyof typeof LineStatus];