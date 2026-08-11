import {
  createContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";

import {
  getAllLines,
  getDashboard,
  toConfiguredFallbackLine,
} from "../services/dashboardService";
import type { DashboardModel } from "../models/DashboardModel";
import { useAuth } from "./useAuth";

export type DashboardView = "table" | "cards";
const DASHBOARD_POLL_MS = 10000;
const MIN_SIGNALR_REFRESH_MS = 1500;
const SIGNALR_HUB_URL =
  import.meta.env.VITE_SIGNALR_HUB_URL ?? "/hubs/lines";

interface DashboardContextType {
  dashboard: DashboardModel | null;
  loading: boolean;
  error: string | null;
  refresh: () => Promise<void>;

  view: DashboardView;
  setView: (view: DashboardView) => void;
}

const DashboardContext = createContext<DashboardContextType | undefined>(
  undefined
);

// The dashboard context owns both the polling fallback path and the SignalR
// realtime subscription used by the operational views.
export function DashboardProvider({
  children,
}: {
  children: ReactNode;
}) {
  const { isAuthenticated } = useAuth();
  const [dashboard, setDashboard] = useState<DashboardModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [view, setView] = useState<DashboardView>("table");
  const signalRConnectionRef = useRef<HubConnection | null>(null);
  const lastRefreshAtRef = useRef(0);
  const inFlightRefreshRef = useRef<Promise<void> | null>(null);
  const deferredRefreshTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Polling is the baseline transport. SignalR only accelerates refreshes.
  async function refresh(showLoader = true) {
    if (inFlightRefreshRef.current) {
      return inFlightRefreshRef.current;
    }

    const run = (async () => {
    if (showLoader) {
      setLoading(true);
    }

    try {
      const data = await getDashboard();
      setDashboard(data);
      setError(null);
    } catch {
      try {
        // If runtime calls fail, fall back to configured active lines instead
        // of blanking the entire UI.
        const configuredLines = (await getAllLines()).filter((line) => line.isActive);

        setDashboard({
          lines: configuredLines.map(toConfiguredFallbackLine),
          lastUpdated: new Date(),
        });
      } catch {
        setDashboard({
          lines: [],
          lastUpdated: new Date(),
        });
      }

      setError("Unable to load live runtime data. Showing configured active lines.");
    } finally {
      lastRefreshAtRef.current = Date.now();
      if (showLoader) {
        setLoading(false);
      }
    }
    })();

    inFlightRefreshRef.current = run;

    try {
      await run;
    } finally {
      inFlightRefreshRef.current = null;
    }
  }

  useEffect(() => {
    let pollingTimer: ReturnType<typeof setInterval> | undefined;
    let disposed = false;

    function requestRealtimeRefresh() {
      if (disposed) {
        return;
      }

      const elapsedMs = Date.now() - lastRefreshAtRef.current;
      if (elapsedMs >= MIN_SIGNALR_REFRESH_MS && !inFlightRefreshRef.current) {
        void refresh(false);
        return;
      }

      if (deferredRefreshTimerRef.current) {
        return;
      }

      const remainingMs = Math.max(100, MIN_SIGNALR_REFRESH_MS - elapsedMs);
      deferredRefreshTimerRef.current = setTimeout(() => {
        deferredRefreshTimerRef.current = null;
        if (!disposed) {
          void refresh(false);
        }
      }, remainingMs);
    }

    if (!isAuthenticated) {
      setDashboard(null);
      setError(null);
      setLoading(false);
      return;
    }

    async function loadInitialDashboard() {
      await refresh(true);

      // Periodic polling keeps the dashboard useful even if SignalR is down.
      pollingTimer = setInterval(() => {
        void refresh(false);
      }, DASHBOARD_POLL_MS);

      const connection = new HubConnectionBuilder()
        .withUrl(SIGNALR_HUB_URL, { withCredentials: true })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      signalRConnectionRef.current = connection;

      // The backend emits broad refresh signals rather than fine-grained row
      // patches, so the frontend always re-fetches the latest snapshot.
      connection.on("LineUpdated", () => {
        requestRealtimeRefresh();
      });

      connection.on("SnapshotRefreshRequired", () => {
        requestRealtimeRefresh();
      });

      connection.onreconnected(() => {
        requestRealtimeRefresh();
      });

      try {
        await connection.start();
      } catch {
        // Polling fallback remains active when hub is temporarily unavailable.
      }
    }

    void loadInitialDashboard();

    return () => {
      disposed = true;

      if (pollingTimer) {
        clearInterval(pollingTimer);
      }

      if (deferredRefreshTimerRef.current) {
        clearTimeout(deferredRefreshTimerRef.current);
        deferredRefreshTimerRef.current = null;
      }

      if (
        signalRConnectionRef.current &&
        signalRConnectionRef.current.state !== HubConnectionState.Disconnected
      ) {
        void signalRConnectionRef.current.stop();
      }
    };
  }, [isAuthenticated]);

  return (
    <DashboardContext.Provider
      value={{
        dashboard,
        loading,
        error,
        refresh,
        view,
        setView,
      }}
    >
      {children}
    </DashboardContext.Provider>
  );
}

export { DashboardContext };