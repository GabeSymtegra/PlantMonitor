import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../services/dashboardService", () => ({
  addLine: vi.fn(),
  getAllLines: vi.fn().mockResolvedValue([]),
  updateLine: vi.fn(),
}));

vi.mock("../services/plcConnectionService", () => ({
  testPlcConnection: vi.fn().mockResolvedValue({
    isConnected: true,
    driver: "AllenBradley",
    ipAddress: "192.168.1.105",
    controllerName: "Test Controller",
    firmware: "v1.2.3",
    responseTimeMs: 42,
    message: "Connected to test PLC.",
  }),
}));

vi.mock("../services/plcTagBrowserService", () => ({
  autoMapTagCatalog: vi.fn().mockResolvedValue({
    driver: "AllenBradley",
    scannedTagCount: 1,
    suggestedMappings: [
      {
        logicalKey: "production_length",
        displayName: "Production Length",
        driver: "AllenBradley",
        plcAddress: "Line.ProductionLength",
        dataType: "real",
        unit: "ft",
        scale: 1,
        description: "Length",
        isEnabled: true,
        sortOrder: 0,
        readFrequencyMs: 1000,
        isRequired: true,
      },
    ],
    missingLogicalKeys: [],
  }),
  browsePlcTags: vi.fn().mockResolvedValue([]),
  getCommissioningReadiness: vi.fn(),
  getLineTagCatalog: vi.fn().mockResolvedValue([]),
  getTagSlots: vi.fn().mockResolvedValue([
    {
      logicalKey: "production_length",
      displayName: "Production Length",
      isRequired: true,
      description: "Length",
    },
  ]),
  readPlcTag: vi.fn().mockResolvedValue({
    name: "Test_Motor_val",
    dataType: "int",
    value: "6000",
    lastReadUtc: "2026-07-08T17:30:18.2126248Z",
    canRead: true,
    canWrite: false,
    error: null,
  }),
  replaceLineTagCatalog: vi.fn(),
}));

import Administration from "../pages/Administration";
import { testPlcConnection } from "../services/plcConnectionService";
import {
  autoMapTagCatalog,
  browsePlcTags,
  getTagSlots,
  readPlcTag,
} from "../services/plcTagBrowserService";

describe("Administration", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("defaults product serial for a new line", async () => {
    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    expect(screen.getByLabelText(/product serial/i)).toHaveValue("000-000-00-0");
  });

  it("enables Save Line only after a successful PLC connection test", async () => {
    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    const saveButton = screen.getByRole("button", { name: /add line/i });
    expect(saveButton).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/line name/i), {
      target: { value: "Main Extruder" },
    });

    fireEvent.change(screen.getByLabelText(/product serial/i), {
      target: { value: "123-456-78-9" },
    });

    fireEvent.change(screen.getByLabelText(/plc ip address/i), {
      target: { value: "192.168.1.105" },
    });

    const testButton = screen.getByRole("button", { name: /test connection/i });
    expect(testButton).toBeEnabled();

    fireEvent.click(testButton);

    await waitFor(() => {
      expect(testPlcConnection).toHaveBeenCalledWith({
        driver: "AllenBradley",
        ipAddress: "192.168.1.105",
      });
    });

    await waitFor(() => {
      expect(screen.getByText(/plc connection verified successfully/i)).toBeInTheDocument();
    });

    expect(screen.getByRole("button", { name: /add line/i })).toBeEnabled();
  });

  it("shows isConnected in the frontend error trace when plc test fails", async () => {
    vi.mocked(testPlcConnection).mockResolvedValueOnce({
      isConnected: false,
      driver: "AllenBradley",
      ipAddress: "192.168.100.50",
      controllerName: "CompactLogix / ControlLogix controller",
      firmware: null,
      responseTimeMs: 121,
      message: "Connected transport, but browse handshake failed.",
    });

    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/line name/i), {
      target: { value: "Main Extruder" },
    });

    fireEvent.change(screen.getByLabelText(/plc ip address/i), {
      target: { value: "192.168.100.50" },
    });

    fireEvent.click(screen.getByRole("button", { name: /test connection/i }));

    await waitFor(() => {
      expect(screen.getByText(/task 4 result: isconnected = false/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/is connected: false/i)).toBeInTheDocument();
  });

  it("keeps the tag browser hidden until tags are discovered", async () => {
    vi.mocked(browsePlcTags).mockResolvedValueOnce([]);

    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/line name/i), {
      target: { value: "Main Extruder" },
    });

    fireEvent.change(screen.getByLabelText(/plc ip address/i), {
      target: { value: "192.168.100.50" },
    });

    fireEvent.click(screen.getByRole("button", { name: /test connection/i }));

    await waitFor(() => {
      expect(browsePlcTags).toHaveBeenCalledWith({
        driver: "AllenBradley",
        ipAddress: "192.168.100.50",
      });
    });

    expect(screen.queryByText(/plc tag browser/i)).not.toBeInTheDocument();
  });

  it("reveals the tag browser and reads a selected tag after discovery", async () => {
    vi.mocked(browsePlcTags).mockResolvedValueOnce([
      {
        name: "Test_Motor_val",
        dataType: "int",
        isFolder: false,
        parentPath: null,
        canRead: true,
        canWrite: false,
      },
    ]);

    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/line name/i), {
      target: { value: "Main Extruder" },
    });

    fireEvent.change(screen.getByLabelText(/plc ip address/i), {
      target: { value: "192.168.100.50" },
    });

    fireEvent.click(screen.getByRole("button", { name: /test connection/i }));

    await waitFor(() => {
      expect(screen.getByText(/plc tag browser/i)).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /test_motor_val/i }));

    await waitFor(() => {
      expect(readPlcTag).toHaveBeenCalledWith({
        driver: "AllenBradley",
        ipAddress: "192.168.100.50",
        tagName: "Test_Motor_val",
      });
    });

    expect(screen.getByDisplayValue("6000")).toBeInTheDocument();
  });

  it("shows Siemens fallback messaging when configured tags are returned", async () => {
    vi.mocked(testPlcConnection).mockResolvedValueOnce({
      isConnected: true,
      driver: "Siemens",
      ipAddress: "192.168.100.1",
      controllerName: "Siemens S7-1200 / S7-1500 CPU",
      firmware: null,
      responseTimeMs: 61,
      message: "Connected to Siemens PLC.",
    });

    vi.mocked(browsePlcTags).mockResolvedValueOnce([
      {
        name: "Machine.Status",
        dataType: "int",
        isFolder: false,
        parentPath: "Machine",
        canRead: true,
        canWrite: false,
      },
    ]);

    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    fireEvent.mouseDown(screen.getByLabelText(/plc driver/i));
    fireEvent.click(screen.getByRole("option", { name: "Siemens" }));

    fireEvent.change(screen.getByLabelText(/line name/i), {
      target: { value: "Secondary Line" },
    });

    fireEvent.change(screen.getByLabelText(/plc ip address/i), {
      target: { value: "192.168.100.1" },
    });

    fireEvent.click(screen.getByRole("button", { name: /test connection/i }));

    await waitFor(() => {
      expect(screen.getByText(/showing available configured siemens tags/i)).toBeInTheDocument();
    });
  });

  it("renders structured tag metadata and read guidance when direct value read is unavailable", async () => {
    vi.mocked(browsePlcTags).mockResolvedValueOnce([
      {
        name: "Map:anlg_in",
        dataType: "type-4201",
        isFolder: false,
        parentPath: null,
        canRead: true,
        canWrite: false,
      },
    ]);

    vi.mocked(readPlcTag).mockResolvedValueOnce({
      name: "Map:anlg_in",
      dataType: "type-4201",
      value: "{\"kind\":\"metadata\",\"tag\":\"Map:anlg_in\",\"note\":\"The symbol was discovered successfully, but this top-level address is not directly readable as a scalar value.\"}",
      lastReadUtc: "2026-07-08T18:02:03.8786863Z",
      canRead: false,
      canWrite: false,
      error: "Direct read for this discovered symbol returned ErrorNotFound. This is usually a controller object or structured tag that requires member expansion.",
    });

    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/line name/i), {
      target: { value: "Main Extruder" },
    });

    fireEvent.change(screen.getByLabelText(/plc ip address/i), {
      target: { value: "192.168.100.50" },
    });

    fireEvent.click(screen.getByRole("button", { name: /test connection/i }));

    await waitFor(() => {
      expect(screen.getByText(/plc tag browser/i)).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /map:anlg_in/i }));

    await waitFor(() => {
      expect(screen.getByDisplayValue(/"kind": "metadata"/i)).toBeInTheDocument();
    });

    expect(screen.getByDisplayValue(/metadata\/diagnostic payload returned because a direct value read was not available/i)).toBeInTheDocument();
    expect(screen.getByText(/direct read for this discovered symbol returned errornotfound/i)).toBeInTheDocument();
  });

  it("auto-maps on first successful connection attempt and shows suggested assignments", async () => {
    vi.mocked(browsePlcTags).mockResolvedValueOnce([
      {
        name: "Line.ProductionLength",
        dataType: "real",
        isFolder: false,
        parentPath: "Line",
        canRead: true,
        canWrite: false,
      },
    ]);

    render(
      <MemoryRouter>
        <Administration />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/line name/i), {
      target: { value: "Main Extruder" },
    });

    fireEvent.change(screen.getByLabelText(/plc ip address/i), {
      target: { value: "192.168.1.105" },
    });

    fireEvent.click(screen.getByRole("button", { name: /test connection/i }));

    await waitFor(() => {
      expect(testPlcConnection).toHaveBeenCalledWith({
        driver: "AllenBradley",
        ipAddress: "192.168.1.105",
      });
    });

    await waitFor(() => {
      expect(browsePlcTags).toHaveBeenCalledWith({
        driver: "AllenBradley",
        ipAddress: "192.168.1.105",
      });
    });

    await waitFor(() => {
      expect(autoMapTagCatalog).toHaveBeenCalledWith({
        driver: "AllenBradley",
        ipAddress: "192.168.1.105",
      });
    });

    expect(getTagSlots).toHaveBeenCalled();

    await waitFor(() => {
      expect(screen.getByText(/auto-map completed\. review the suggested assignments/i)).toBeInTheDocument();
    });
  });
});