import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";

import { getDashboard } from "../services/dashboardService";
import type { DashboardModel } from "../models/DashboardModel";

interface DashboardContextType {
  dashboard: DashboardModel | null;
  loading: boolean;
  refresh: () => Promise<void>;
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

  async function refresh() {
    setLoading(true);

    const data = await getDashboard();

    setDashboard(data);

    setLoading(false);
  }

  useEffect(() => {
    refresh();
  }, []);

  return (
    <DashboardContext.Provider
      value={{
        dashboard,
        loading,
        refresh,
      }}
    >
      {children}
    </DashboardContext.Provider>
  );
}

export function useDashboard() {
  const context = useContext(DashboardContext);

  if (!context) {
    throw new Error(
      "useDashboard must be used inside DashboardProvider"
    );
  }

  return context;
}