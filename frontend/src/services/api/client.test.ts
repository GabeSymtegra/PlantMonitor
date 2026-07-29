import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  AUTH_EXPIRED_EVENT,
  ApiRequestError,
  apiPost,
  setApiAccessToken,
} from "./client";

describe("api client auth handling", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    setApiAccessToken(null);
    vi.restoreAllMocks();
  });

  afterEach(() => {
    setApiAccessToken(null);
    vi.restoreAllMocks();
  });

  it("uses the in-memory auth token when storage is empty", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ ok: true }),
    });

    vi.stubGlobal("fetch", fetchMock);
    setApiAccessToken("live-token");

    await apiPost("/admin/plc/test-connection", {
      driver: "AllenBradley",
      ipAddress: "192.168.100.50",
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/admin/plc/test-connection",
      expect.objectContaining({
        credentials: "include",
        headers: expect.objectContaining({
          Authorization: "Bearer live-token",
        }),
      })
    );
  });

  it("dispatches auth-expired for non-login 401 responses", async () => {
    const eventHandler = vi.fn();
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      statusText: "Unauthorized",
      text: async () => "Unauthorized",
    });

    vi.stubGlobal("fetch", fetchMock);
    window.addEventListener(AUTH_EXPIRED_EVENT, eventHandler);
    setApiAccessToken("expired-token");

    await expect(
      apiPost("/admin/plc/test-connection", {
        driver: "AllenBradley",
        ipAddress: "192.168.100.50",
      })
    ).rejects.toBeInstanceOf(ApiRequestError);

    expect(eventHandler).toHaveBeenCalledTimes(1);

    window.removeEventListener(AUTH_EXPIRED_EVENT, eventHandler);
  });
});