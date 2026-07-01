const API_BASE = "http://localhost:5265/api";

export async function apiGet<T>(endpoint: string): Promise<T> {
  const response = await fetch(`${API_BASE}${endpoint}`);

  if (!response.ok) {
    throw new Error("API request failed");
  }

  return response.json();
}