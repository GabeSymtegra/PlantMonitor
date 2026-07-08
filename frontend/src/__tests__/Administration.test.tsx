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

import Administration from "../pages/Administration";
import { testPlcConnection } from "../services/plcConnectionService";

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
});