import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";

import { getDashboard } from "../services/dashboardService";
import type { DashboardModel } from "../models/DashboardModel";

export type DashboardView = "table" | "cards";

interface DashboardContextType {
  dashboard: DashboardModel | null;
  loading: boolean;
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

  const [view, setView] = useState<DashboardView>("table");

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
        view,
        setView,
      }}
    >
      {children}
    </DashboardContext.Provider>
  );
}

export function useDashboard() {
  const context = useContext(DashboardContext);

  if (!context) {
    throw new Error("useDashboard must be used inside DashboardProvider");
  }

  return context;
}