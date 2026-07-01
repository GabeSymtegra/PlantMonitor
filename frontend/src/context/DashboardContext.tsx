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

import { getDashboard } from "../services/dashboardService";
import type { DashboardModel } from "../models/DashboardModel";

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

export function DashboardProvider({
  children,
}: {
  children: ReactNode;
}) {
  const [dashboard, setDashboard] = useState<DashboardModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [view, setView] = useState<DashboardView>("table");
  const signalRConnectionRef = useRef<HubConnection | null>(null);

  async function refresh(showLoader = true) {
    if (showLoader) {
      setLoading(true);
    }

    try {
      const data = await getDashboard();
      setDashboard(data);
      setError(null);
    } catch {
      setError("Unable to load dashboard data. Check backend connectivity and try again.");
    } finally {
      if (showLoader) {
        setLoading(false);
      }
    }
  }

  useEffect(() => {
    let pollingTimer: ReturnType<typeof setInterval> | undefined;
    let disposed = false;

    async function loadInitialDashboard() {
      await refresh(true);

      pollingTimer = setInterval(() => {
        void refresh(false);
      }, DASHBOARD_POLL_MS);

      const connection = new HubConnectionBuilder()
        .withUrl(SIGNALR_HUB_URL)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      signalRConnectionRef.current = connection;

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
  }, []);

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