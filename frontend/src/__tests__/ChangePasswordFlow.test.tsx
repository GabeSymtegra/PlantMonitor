import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { AuthProvider } from "../context/AuthContext";
import ChangePassword from "../pages/ChangePassword";
import ProtectedRoute from "../routes/ProtectedRoute";

describe("ChangePassword authenticated flow", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("refreshes auth session and reaches protected destination after password change", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        headers: new Headers(),
        text: async () => JSON.stringify({
          username: "test",
          role: "Admin",
          mustChangePassword: true,
        }),
      })
      .mockResolvedValueOnce({
        ok: true,
        status: 204,
        headers: new Headers(),
        text: async () => "",
      })
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        headers: new Headers(),
        text: async () => JSON.stringify({
          username: "test",
          role: "Admin",
          mustChangePassword: false,
        }),
      });

    vi.stubGlobal("fetch", fetchMock);

    render(
      <AuthProvider>
        <MemoryRouter
          initialEntries={[
            {
              pathname: "/change-password",
              state: { from: "/settings" },
            },
          ]}
        >
          <Routes>
            <Route element={<ProtectedRoute />}>
              <Route path="/change-password" element={<ChangePassword />} />
              <Route path="/settings" element={<div>settings page</div>} />
            </Route>
            <Route path="/login" element={<div>login page</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    );

    await screen.findByRole("heading", { name: /change password/i });

    fireEvent.change(screen.getByLabelText("Current Password"), {
      target: { value: "current-password" },
    });
    fireEvent.change(screen.getByLabelText("New Password"), {
      target: { value: "new-password-123" },
    });
    fireEvent.change(screen.getByLabelText("Confirm New Password"), {
      target: { value: "new-password-123" },
    });

    fireEvent.click(screen.getByRole("button", { name: /save password/i }));

    await waitFor(() => {
      expect(screen.getByText("settings page")).toBeInTheDocument();
    });

    await waitFor(() => {
      expect(screen.queryByRole("heading", { name: /change password/i })).not.toBeInTheDocument();
    });
  });
});
