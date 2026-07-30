import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  AUTH_EXPIRED_EVENT,
  ApiRequestError,
  apiGet,
  apiPost,
} from "./client";

describe("api client auth handling", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("sends cookie credentials without an Authorization header", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers(),
      text: async () => JSON.stringify({ ok: true }),
    });

    vi.stubGlobal("fetch", fetchMock);

    await apiPost("/admin/plc/test-connection", {
      driver: "AllenBradley",
      ipAddress: "192.168.100.50",
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/admin/plc/test-connection",
      expect.objectContaining({
        credentials: "include",
        headers: expect.not.objectContaining({
          Authorization: expect.any(String),
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
      headers: new Headers(),
      text: async () => "Unauthorized",
    });

    vi.stubGlobal("fetch", fetchMock);
    window.addEventListener(AUTH_EXPIRED_EVENT, eventHandler);

    await expect(
      apiPost("/admin/plc/test-connection", {
        driver: "AllenBradley",
        ipAddress: "192.168.100.50",
      })
    ).rejects.toBeInstanceOf(ApiRequestError);

    expect(eventHandler).toHaveBeenCalledTimes(1);

    window.removeEventListener(AUTH_EXPIRED_EVENT, eventHandler);
  });

  it("resolves undefined for successful 204 responses", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 204,
      headers: new Headers(),
      text: async () => "",
    });

    vi.stubGlobal("fetch", fetchMock);

    await expect(apiPost<void, object>("/auth/logout", {})).resolves.toBeUndefined();
  });

  it("resolves undefined for successful responses with content-length zero", async () => {
    const jsonSpy = vi.fn();
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ "Content-Length": "0" }),
      text: async () => "",
      json: jsonSpy,
    });

    vi.stubGlobal("fetch", fetchMock);

    await expect(apiPost<void, object>("/auth/logout", {})).resolves.toBeUndefined();
    expect(jsonSpy).not.toHaveBeenCalled();
  });

  it("deserializes JSON response payloads normally", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers(),
      text: async () => JSON.stringify({ authenticated: true }),
    });

    vi.stubGlobal("fetch", fetchMock);

    await expect(apiGet<{ authenticated: boolean }>("/auth/session")).resolves.toEqual({
      authenticated: true,
    });
  });
});