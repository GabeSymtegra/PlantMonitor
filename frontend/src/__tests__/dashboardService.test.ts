import { beforeEach, describe, expect, it, vi } from "vitest";

import { getDashboard } from "../services/dashboardService";
import * as apiClient from "../services/api/client";

vi.mock("../services/api/client", () => ({
  apiDelete: vi.fn(),
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
}));

describe("dashboardService", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("uses configured metadata to fill dashboard lines when runtime data is sparse", async () => {
    vi.mocked(apiClient.apiGet).mockImplementation(async (url: string) => {
      if (url === "/lines") {
        return [
          {
            id: 7,
            lineNumber: 7,
            lineName: "Line 7",
            productId: "PRODUCT-7",
            recipeId: "RCP-100",
            machineId: "MX-100",
            operatorName: "operator-100",
            plcIp: "192.168.1.50",
            manufacturer: "AllenBradley",
            pollIntervalMs: 2000,
            isActive: true,
            lineLifecycleState: "Active",
            updatedAtUtc: "2024-01-01T00:00:00Z",
          },
        ];
      }

      if (url === "/dashboard") {
        return {
          lines: [
            {
              id: 7,
              lineNumber: 7,
              lineName: "Line 7",
              productId: "PRODUCT-7",
              startTimeUtc: "2024-01-01T00:00:00Z",
              status: "Running",
              controlMode: "Auto",
              totalLength: 120,
              runtimeSeconds: 3600,
              plcIp: "192.168.1.50",
              manufacturer: "AllenBradley",
              updatedAtUtc: "2024-01-01T00:00:00Z",
              percentAutoMode: 100,
              percentManualMode: 0,
            },
          ],
          lastUpdatedUtc: "2024-01-01T00:00:00Z",
        };
      }

      return null;
    });

    const dashboard = await getDashboard();
    const line = dashboard.lines.find((candidate) => candidate.id === 7);

    expect(line?.recipeId).toBe("RCP-100");
    expect(line?.machineId).toBe("MX-100");
    expect(line?.operatorName).toBe("operator-100");
  });
});
