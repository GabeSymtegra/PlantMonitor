import type { ProductionLine } from "../types/ProductionLine";

export interface DashboardModel {
  lines: ProductionLine[];
  lastUpdated: Date;
}