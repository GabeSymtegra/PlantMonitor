import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  refreshDashboardMock: vi.fn().mockResolvedValue(undefined),
  addLineMock: vi.fn(),
  deleteLineMock: vi.fn().mockResolvedValue(undefined),
  getAllLinesMock: vi.fn(),
  updateLineMock: vi.fn().mockResolvedValue(undefined),
  testPlcConnectionMock: vi.fn(),
  autoMapTagCatalogMock: vi.fn(),
  browsePlcTagsMock: vi.fn(),
  getTagSlotsMock: vi.fn(),
  getLineTagCatalogMock: vi.fn(),
  getLineProtocolAssignmentMock: vi.fn(),
  replaceLineTagCatalogMock: vi.fn().mockResolvedValue([]),
  upsertLineProtocolAssignmentMock: vi.fn().mockResolvedValue({}),
  getCommissioningReadinessMock: vi.fn(),
  activateCommissionedLineMock: vi.fn().mockResolvedValue({ lineLifecycleState: "Active" }),
  readPlcTagMock: vi.fn(),
}));

vi.mock("../context/useDashboard", () => ({
  useDashboard: () => ({
    refresh: mocks.refreshDashboardMock,
  }),
}));

vi.mock("../services/dashboardService", () => ({
  addLine: mocks.addLineMock,
  deleteLine: mocks.deleteLineMock,
  getAllLines: mocks.getAllLinesMock,
  updateLine: mocks.updateLineMock,
}));

vi.mock("../services/plcConnectionService", () => ({
  testPlcConnection: mocks.testPlcConnectionMock,
}));

vi.mock("../services/plcTagBrowserService", () => ({
  activateCommissionedLine: mocks.activateCommissionedLineMock,
  autoMapTagCatalog: mocks.autoMapTagCatalogMock,
  browsePlcTags: mocks.browsePlcTagsMock,
  getCommissioningReadiness: mocks.getCommissioningReadinessMock,
  getLineProtocolAssignment: mocks.getLineProtocolAssignmentMock,
  getLineTagCatalog: mocks.getLineTagCatalogMock,
  getTagSlots: mocks.getTagSlotsMock,
  readPlcTag: mocks.readPlcTagMock,
  replaceLineTagCatalog: mocks.replaceLineTagCatalogMock,
  upsertLineProtocolAssignment: mocks.upsertLineProtocolAssignmentMock,
}));

import Administration from "../pages/Administration";

const canonicalSlots = [
  "line_id",
  "product_id",
  "control_mode",
  "machine_state",
  "production_length",
  "bare_setpoint",
  "bare_actual",
  "hot_setpoint",
  "hot_actual",
  "cold_setpoint",
  "cold_actual",
];

function buildLine(id: number) {
  return {
    id,
    lineNumber: id,
    lineName: `Line ${id}`,
    product: "123-456-78-9",
    recipeId: "RCP-100",
    machineId: "MX-100",
    operatorName: "operator-100",
    startDateTime: new Date().toISOString(),
    status: "Offline",
    timeInStatus: "00:00:00",
    controlMode: "Auto",
    percentAutoMode: 100,
    autoVariance: 0,
    percentManualMode: 0,
    manualVariance: 0,
    totalVariance: 0,
    totalLength: 0,
    runtime: "00:00:00",
    plcIp: "192.168.1.105",
    manufacturer: "AllenBradley",
    isActive: false,
    lineLifecycleState: "Draft",
  };
}

describe("Administration", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    mocks.getAllLinesMock.mockResolvedValue([]);
    mocks.addLineMock.mockResolvedValue(buildLine(101));

    mocks.testPlcConnectionMock.mockResolvedValue({
      isConnected: true,
      driver: "AllenBradley",
      ipAddress: "192.168.1.105",
      message: "Connected to test PLC.",
      controllerName: "Test Controller",
      responseTimeMs: 20,
    });

    mocks.browsePlcTagsMock.mockResolvedValue([
      {
        name: "Line.ProductionLength",
        dataType: "real",
        isFolder: false,
        parentPath: "Line",
        canRead: true,
        canWrite: false,
      },
    ]);

    mocks.getTagSlotsMock.mockResolvedValue(
      canonicalSlots.map((logicalKey) => ({
        logicalKey,
        displayName: logicalKey,
        isRequired: true,
        description: logicalKey,
      }))
    );

    mocks.autoMapTagCatalogMock.mockResolvedValue({
      driver: "AllenBradley",
      scannedTagCount: canonicalSlots.length,
      suggestedMappings: canonicalSlots.map((logicalKey, index) => ({
        logicalKey,
        displayName: logicalKey,
        driver: "AllenBradley",
        plcAddress: `Line.${logicalKey}`,
        dataType: logicalKey === "product_id" ? "string" : "real",
        unit: null,
        scale: 1,
        description: logicalKey,
        isEnabled: true,
        sortOrder: index,
        readFrequencyMs: 1000,
        isRequired: true,
      })),
      suggestionDetails: canonicalSlots.map((logicalKey) => ({
        logicalKey,
        plcAddress: `Line.${logicalKey}`,
        confidence: 90,
        reason: "Exact key match",
      })),
      missingLogicalKeys: [],
    });

    mocks.getLineTagCatalogMock.mockResolvedValue([]);
    mocks.getLineProtocolAssignmentMock.mockResolvedValue({
      lineId: 101,
      manufacturer: "AllenBradley",
      presetName: "BasicStatus",
      presetVersion: 1,
      pollIntervalMs: 2000,
      routePath: "1,0",
      processorType: "ControlLogix",
      connectionTimeoutMs: 3000,
      readTimeoutMs: 3000,
      retryCount: 1,
      retryDelayMs: 250,
      updatedAtUtc: new Date().toISOString(),
    });

    mocks.getCommissioningReadinessMock.mockResolvedValue({
      lineId: 101,
      manufacturer: "AllenBradley",
      presetName: "BasicStatus",
      presetVersion: 1,
      isReady: true,
      requiredTagCount: canonicalSlots.length,
      mappedRequiredTagCount: canonicalSlots.length,
      missingRequiredTagKeys: [],
      issues: [],
      checkedAtUtc: new Date().toISOString(),
    });
  });

  it("renders commissioning status bar", async () => {
    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    expect(await screen.findByText(/commissioning progress/i)).toBeInTheDocument();
    expect(screen.getByText(/step 1 of 5: test connection/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/commissioning progress/i)).toBeInTheDocument();
  });

  it("enforces guided new-line sequence before enabling Add Line", async () => {
    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/line name/i), {
      target: { value: "Main Extruder" },
    });

    fireEvent.change(screen.getByLabelText(/product serial/i), {
      target: { value: "123-456-78-9" },
    });

    fireEvent.change(screen.getByLabelText(/plc ip address/i), {
      target: { value: "192.168.1.105" },
    });

    const addLineButton = screen.getByRole("button", { name: /add line/i });
    const autoPopulateButton = screen.getByRole("button", { name: /auto populate/i });
    const saveTagAssignmentsButton = screen.getByRole("button", { name: /save tag assignments/i });

    expect(addLineButton).toBeDisabled();
    expect(autoPopulateButton).toBeDisabled();

    fireEvent.click(screen.getByRole("button", { name: /test connection/i }));

    await waitFor(() => {
      expect(mocks.testPlcConnectionMock).toHaveBeenCalledWith(
        expect.objectContaining({
          driver: "AllenBradley",
          ipAddress: "192.168.1.105",
        })
      );
    });

    await waitFor(() => {
      expect(autoPopulateButton).toBeEnabled();
    });

    fireEvent.click(autoPopulateButton);

    await waitFor(() => {
      expect(mocks.autoMapTagCatalogMock).toHaveBeenCalledWith(
        expect.objectContaining({
          driver: "AllenBradley",
          ipAddress: "192.168.1.105",
        })
      );
    });

    fireEvent.click(saveTagAssignmentsButton);

    await waitFor(() => {
      expect(screen.getByText(/tag assignments staged/i)).toBeInTheDocument();
    });

    expect(addLineButton).toBeEnabled();
  });

  it("uses a styled confirmation dialog before deleting", async () => {
    const line = buildLine(222);
    line.lineLifecycleState = "Active";
    line.isActive = true;
    line.lineName = "Active Delete Line";
    mocks.getAllLinesMock
      .mockResolvedValueOnce([line])
      .mockResolvedValueOnce([])
      .mockResolvedValue([]);

    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    fireEvent.click(await screen.findByRole("button", { name: /line 222 - active delete line/i }));

    fireEvent.click(await screen.findByRole("button", { name: /^delete line$/i }));

    expect(await screen.findByText(/delete line configuration/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /^delete line$/i }));

    await waitFor(() => {
      expect(mocks.deleteLineMock).toHaveBeenCalledWith(222);
    });

    await waitFor(() => {
      expect(mocks.refreshDashboardMock).toHaveBeenCalled();
    });
  });
});
