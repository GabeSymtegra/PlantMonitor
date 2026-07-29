import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

import ChangePassword from "../pages/ChangePassword";
import { apiPost } from "../services/api/client";

vi.mock("../services/api/client", async () => {
  const actual = await vi.importActual<typeof import("../services/api/client")>("../services/api/client");

  return {
    ...actual,
    apiPost: vi.fn(),
  };
});

vi.mock("../context/useAuth", () => ({
  useAuth: () => ({
    user: {
      username: "test",
      role: "Admin",
      mustChangePassword: true,
    },
  }),
}));

describe("ChangePassword", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("submits successfully when change-password returns 204 and redirects", async () => {
    const apiPostMock = vi.mocked(apiPost);
    apiPostMock.mockResolvedValue(undefined);

    render(
      <MemoryRouter
        initialEntries={[
          {
            pathname: "/change-password",
            state: { from: "/status-board" },
          },
        ]}
      >
        <Routes>
          <Route path="/change-password" element={<ChangePassword />} />
          <Route path="/status-board" element={<div>status board page</div>} />
        </Routes>
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText("Current Password"), {
      target: { value: "current-password" },
    });
    fireEvent.change(screen.getByLabelText("New Password"), {
      target: { value: "new-password-123" },
    });
    fireEvent.change(screen.getByLabelText("Confirm New Password"), {
      target: { value: "new-password-123" },
    });

    fireEvent.click(screen.getByRole("button", { name: "Save Password" }));

    await waitFor(() => {
      expect(apiPostMock).toHaveBeenCalledWith("/auth/change-password", {
        currentPassword: "current-password",
        newPassword: "new-password-123",
      });
    });

    await waitFor(() => {
      expect(screen.getByText("status board page")).toBeInTheDocument();
    });
  });
});
