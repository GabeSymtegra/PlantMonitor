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
const SIGNALR_HUB_URL =
  import.meta.env.VITE_SIGNALR_HUB_URL ?? "http://localhost:5265/hubs/lines";

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
  const { isAuthenticated, accessToken } = useAuth();
  const authToken = accessToken;
  const [dashboard, setDashboard] = useState<DashboardModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [view, setView] = useState<DashboardView>("table");
  const signalRConnectionRef = useRef<HubConnection | null>(null);

  // Polling is the baseline transport. SignalR only accelerates refreshes.
  async function refresh(showLoader = true) {
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
      if (showLoader) {
        setLoading(false);
      }
    }
  }

  useEffect(() => {
    let pollingTimer: ReturnType<typeof setInterval> | undefined;
    let disposed = false;

    if (!isAuthenticated || !authToken) {
      setDashboard(null);
      setError(null);
      setLoading(false);
      return;
    }

    const token: string = authToken;

    async function loadInitialDashboard() {
      await refresh(true);

      // Periodic polling keeps the dashboard useful even if SignalR is down.
      pollingTimer = setInterval(() => {
        void refresh(false);
      }, DASHBOARD_POLL_MS);

      const connection = new HubConnectionBuilder()
        .withUrl(SIGNALR_HUB_URL, {
          accessTokenFactory: () => token,
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      signalRConnectionRef.current = connection;

      // The backend emits broad refresh signals rather than fine-grained row
      // patches, so the frontend always re-fetches the latest snapshot.
      connection.on("LineUpdated", () => {
        if (!disposed) {
          void refresh(false);
        }
      });

      connection.on("SnapshotRefreshRequired", () => {
        if (!disposed) {
          void refresh(false);
        }
      });

      connection.onreconnected(() => {
        if (!disposed) {
          void refresh(false);
        }
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

      if (
        signalRConnectionRef.current &&
        signalRConnectionRef.current.state !== HubConnectionState.Disconnected
      ) {
        void signalRConnectionRef.current.stop();
      }
    };
  }, [authToken, isAuthenticated]);

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