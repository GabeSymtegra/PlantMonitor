const API_BASE =
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") ??
  "http://localhost:5265/api";

const TOKEN_STORAGE_KEY = "plantmonitor-auth-token";
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

function buildUrl(endpoint: string): string {
  return `${API_BASE}${endpoint}`;
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
  const token =
    currentAccessToken ??
    localStorage.getItem(TOKEN_STORAGE_KEY) ??
    sessionStorage.getItem(TOKEN_STORAGE_KEY);

  if (!token) {
    return {};
  }

  return {
    Authorization: `Bearer ${token}`,
  };
}

function dispatchAuthExpiredIfNeeded(url: string, status: number) {
  if (status !== 401 || url.endsWith("/auth/login")) {
    return;
  }

  currentAccessToken = null;

  if (typeof window !== "undefined") {
    window.dispatchEvent(new CustomEvent(AUTH_EXPIRED_EVENT));
  }
}

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

export async function apiGet<T>(endpoint: string): Promise<T> {
  const method = "GET";
  const url = buildUrl(endpoint);

  try {
    const response = await fetch(url, {
      headers: {
        ...getAuthHeaders(),
      },
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
      message: buildRequestTrace(
        method,
        url,
        "failed: no response returned",
        details
      ),
    });
  }
}

export async function apiPost<TResponse, TBody = unknown>(
  endpoint: string,
  body: TBody
): Promise<TResponse> {
  const method = "POST";
  const url = buildUrl(endpoint);

  try {
    const response = await fetch(url, {
      method,
      headers: {
        "Content-Type": "application/json",
        ...getAuthHeaders(),
      },
      body: JSON.stringify(body),
    });

    return handleResponse<TResponse>(response, method, url);
  } catch (error) {
    if (error instanceof ApiRequestError) {
      throw error;
    }

    const details = error instanceof Error ? error.message : "The request did not reach the backend.";

    throw new ApiRequestError({
      method,
      url,
      message: buildRequestTrace(
        method,
        url,
        "failed: no response returned",
        details
      ),
    });
  }
}