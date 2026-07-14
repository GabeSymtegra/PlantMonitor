export const API_ENDPOINTS = {
  lines: "/lines",
  line: (id: number) => `/lines/${id}`,
  dashboard: "/dashboard",
  lineDetails: (id: number) => `/lines/${id}/details`,
};