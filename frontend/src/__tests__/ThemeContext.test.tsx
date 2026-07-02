import { render, screen, waitFor } from "@testing-library/react";

import { AppThemeProvider } from "../context/ThemeContext";
import { useThemeMode } from "../context/useThemeMode";

function ThemeProbe() {
  const { mode, appearance } = useThemeMode();

  return (
    <>
      <div data-testid="mode">{mode}</div>
      <div data-testid="header">{appearance.headerColor}</div>
      <div data-testid="background">{appearance.backgroundColor}</div>
      <div data-testid="table">{appearance.tableColor}</div>
    </>
  );
}

describe("ThemeContext", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it("forces Spec Ops colors when dark mode is active", async () => {
    localStorage.setItem("plantmonitor-theme-mode", "dark");
    localStorage.setItem(
      "plantmonitor-appearance-settings",
      JSON.stringify({
        monitorName: "Plant",
        headerColor: "#1565C0",
        backgroundColor: "#F4F6F8",
        tableColor: "#E3F2FD",
        textSize: "medium",
        boldText: false,
      })
    );

    render(
      <AppThemeProvider>
        <ThemeProbe />
      </AppThemeProvider>
    );

    expect(screen.getByTestId("mode")).toHaveTextContent("dark");

    await waitFor(() => {
      expect(screen.getByTestId("header")).toHaveTextContent("#0B1F35");
      expect(screen.getByTestId("background")).toHaveTextContent("#121820");
      expect(screen.getByTestId("table")).toHaveTextContent("#223247");
    });
  });

  it("forces Classic Plant colors when light mode is active", async () => {
    localStorage.setItem("plantmonitor-theme-mode", "light");
    localStorage.setItem(
      "plantmonitor-appearance-settings",
      JSON.stringify({
        monitorName: "Plant",
        headerColor: "#0B1F35",
        backgroundColor: "#121820",
        tableColor: "#223247",
        textSize: "medium",
        boldText: false,
      })
    );

    render(
      <AppThemeProvider>
        <ThemeProbe />
      </AppThemeProvider>
    );

    expect(screen.getByTestId("mode")).toHaveTextContent("light");

    await waitFor(() => {
      expect(screen.getByTestId("header")).toHaveTextContent("#1565C0");
      expect(screen.getByTestId("background")).toHaveTextContent("#F4F6F8");
      expect(screen.getByTestId("table")).toHaveTextContent("#E3F2FD");
    });
  });
});
