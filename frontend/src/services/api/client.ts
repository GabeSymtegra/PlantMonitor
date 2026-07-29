const API_BASE =
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") ??
  "/api";

// -----------------------------------------------------------------------------
// Auth token state and exported API error type
// -----------------------------------------------------------------------------

export const AUTH_EXPIRED_EVENT = "plantmonitor-auth-expired";

let currentAccessToken: string | null = null;

export class ApiRequestError extends Error {
  public readonly method: string;
  public readonly url: string;
  public readonly status?: number;
  public readonly responseBody?: string;

  constructor(options: {
    method: string;
    url: string;
    message: string;
    status?: number;
    responseBody?: string;
  }) {
    super(options.message);
    this.name = "ApiRequestError";
    this.method = options.method;
    this.url = options.url;
    this.status = options.status;
    this.responseBody = options.responseBody;
  }
}

// -----------------------------------------------------------------------------
// Request construction helpers
// -----------------------------------------------------------------------------

function buildUrl(endpoint: string): string {
  const normalizedEndpoint = endpoint.startsWith("/")
    ? endpoint
    : `/${endpoint}`;

  const baseHasApiSuffix = /\/api$/i.test(API_BASE);
  const endpointHasApiPrefix = /^\/api(\/|$)/i.test(normalizedEndpoint);

  let resolvedEndpoint = normalizedEndpoint;

  if (baseHasApiSuffix && endpointHasApiPrefix) {
    resolvedEndpoint = normalizedEndpoint.replace(/^\/api/i, "");
  }

  if (!baseHasApiSuffix && !endpointHasApiPrefix) {
    resolvedEndpoint = `/api${normalizedEndpoint}`;
  }

  return `${API_BASE}${resolvedEndpoint}`;
}

export function setApiAccessToken(token: string | null) {
  currentAccessToken = token;
}

function buildRequestTrace(
  method: string,
  url: string,
  outcome: string,
  details?: string
): string {
  const lines = [
    `Task 1 complete: prepared ${method} ${url}`,
    `Task 2 complete: sent ${method} ${url}`,
    `Task 3 ${outcome}: ${details ?? `${method} ${url}`}`,
  ];

  return lines.join("\n");
}

function getAuthHeaders(): HeadersInit {
  const token = currentAccessToken;

  if (!token) {
    return {};
  }

  return {
    Authorization: `Bearer ${token}`,
  };
}

// Unauthorized responses outside the login endpoint trigger a global auth
// expiry event so the auth context can clear stale sessions.
function dispatchAuthExpiredIfNeeded(url: string, status: number) {
  if (status !== 401 || url.endsWith("/auth/login")) {
    return;
  }

  currentAccessToken = null;

  if (typeof window !== "undefined") {
    window.dispatchEvent(new CustomEvent(AUTH_EXPIRED_EVENT));
  }
}

// -----------------------------------------------------------------------------
// Shared response and request pipeline
// -----------------------------------------------------------------------------

async function handleResponse<T>(
  response: Response,
  method: string,
  url: string
): Promise<T> {
  if (!response.ok) {
    const errorText = await response.text();
    const statusText = response.statusText || "Request failed";
    const responseDetails = errorText || statusText;

    dispatchAuthExpiredIfNeeded(url, response.status);

    throw new ApiRequestError({
      method,
      url,
      status: response.status,
      responseBody: errorText || undefined,
      message: buildRequestTrace(
        method,
        url,
        `failed: HTTP ${response.status} ${statusText}`,
        responseDetails
      ),
    });
  }

  return response.json() as Promise<T>;
}

async function request<T>(
  method: string,
  endpoint: string,
  body?: unknown
): Promise<T> {
  const url = buildUrl(endpoint);

  try {
    const response = await fetch(url, {
      method,
      credentials: "include",
      headers: {
        ...(body ? { "Content-Type": "application/json" } : {}),
        ...getAuthHeaders(),
      },
      body: body ? JSON.stringify(body) : undefined,
    });

    return handleResponse<T>(response, method, url);
  } catch (error) {
    if (error instanceof ApiRequestError) {
      throw error;
    }

    const details = error instanceof Error ? error.message : "The request did not reach the backend.";

    throw new ApiRequestError({
      method,
      url,
      message: buildRequestTrace(method, url, "failed: no response returned", details),
    });
  }
}

// Public convenience helpers keep API usage readable at the call site while all
// header, auth, and error behavior stays centralized here.
export async function apiGet<T>(endpoint: string): Promise<T> {
  return request<T>("GET", endpoint);
}

export async function apiPost<TResponse, TBody = unknown>(
  endpoint: string,
  body: TBody
): Promise<TResponse> {
  return request<TResponse>("POST", endpoint, body);
}

export async function apiPut<TResponse, TBody = unknown>(
  endpoint: string,
  body: TBody
): Promise<TResponse> {
  return request<TResponse>("PUT", endpoint, body);
}

export async function apiDelete(endpoint: string): Promise<void> {
  const method = "DELETE";
  const url = buildUrl(endpoint);

  try {
    const response = await fetch(url, {
      method,
      credentials: "include",
      headers: {
        ...getAuthHeaders(),
      },
    });

    if (!response.ok) {
      const errorText = await response.text();
      const statusText = response.statusText || "Request failed";
      const responseDetails = errorText || statusText;

      dispatchAuthExpiredIfNeeded(url, response.status);

      throw new ApiRequestError({
        method,
        url,
        status: response.status,
        responseBody: errorText || undefined,
        message: buildRequestTrace(method, url, `failed: HTTP ${response.status} ${statusText}`, responseDetails),
      });
    }
  } catch (error) {
    if (error instanceof ApiRequestError) {
      throw error;
    }

    const details = error instanceof Error ? error.message : "The request did not reach the backend.";

    throw new ApiRequestError({
      method,
      url,
      message: buildRequestTrace(method, url, "failed: no response returned", details),
    });
  }
}