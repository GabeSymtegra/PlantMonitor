import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import { AuthContext } from "../context/AuthContext";
import type { User } from "../models/User";
import ProtectedRoute from "../routes/ProtectedRoute";

function renderWithAuth(
  path: string,
  options: {
    user: User | null;
    accessToken: string | null;
    requiredRoles?: User["role"][];
  }
) {
  return render(
    <AuthContext.Provider
      value={{
        user: options.user,
        accessToken: options.accessToken,
        authError: null,
        isAuthenticated: Boolean(options.user && options.accessToken),
        isAdmin: options.user?.role === "Admin",
        canConfigure: options.user?.role === "Admin",
        login: async () => true,
        logout: () => undefined,
      }}
    >
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route path="/" element={<div>dashboard page</div>} />
            <Route path="/status-board" element={<div>status board page</div>} />
          </Route>
          <Route element={<ProtectedRoute requiredRoles={options.requiredRoles} />}>
            <Route path="/settings" element={<div>settings page</div>} />
          </Route>
          <Route path="/login" element={<div>login page</div>} />
          <Route path="/forbidden" element={<div>forbidden page</div>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>
  );
}

describe("ProtectedRoute", () => {
  it("redirects unauthenticated users to login", () => {
    renderWithAuth("/settings", {
      user: null,
      accessToken: null,
    });

    expect(screen.getByText("login page")).toBeInTheDocument();
  });

  it("redirects authenticated non-admin users to forbidden when role required", () => {
    renderWithAuth("/settings", {
      user: { username: "operator", role: "Operator" },
      accessToken: "token",
      requiredRoles: ["Admin"],
    });

    expect(screen.getByText("forbidden page")).toBeInTheDocument();
  });

  it("allows authenticated admin users through", () => {
    renderWithAuth("/settings", {
      user: { username: "test", role: "Admin" },
      accessToken: "token",
      requiredRoles: ["Admin"],
    });

    expect(screen.getByText("settings page")).toBeInTheDocument();
  });

  it("redirects operator users to status board for non-status-board routes", () => {
    renderWithAuth("/", {
      user: { username: "operator", role: "Operator" },
      accessToken: "token",
    });

    expect(screen.getByText("status board page")).toBeInTheDocument();
  });

  it("allows operator users on status board route", () => {
    renderWithAuth("/status-board", {
      user: { username: "operator", role: "Operator" },
      accessToken: "token",
    });

    expect(screen.getByText("status board page")).toBeInTheDocument();
  });
});
