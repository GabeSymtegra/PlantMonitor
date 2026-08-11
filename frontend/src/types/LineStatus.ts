export const LineStatus = {
  Running: "Running",
  Stopped: "Stopped",
  Bleedout: "Bleedout",
  Startup: "Startup",
  Faulted: "Faulted",
  Offline: "Offline",
  Maintenance: "Maintenance",
} as const;

export type LineStatus =
  (typeof LineStatus)[keyof typeof LineStatus];