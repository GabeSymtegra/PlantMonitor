import AppRoutes from "./routes/AppRoutes";
import { DashboardProvider } from "./context/DashboardContext";

export default function App() {
  return (
    <DashboardProvider>
      <AppRoutes />
    </DashboardProvider>
  );
}