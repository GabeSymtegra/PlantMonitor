import AppRoutes from "./routes/AppRoutes";
import { AuthProvider } from "./context/AuthContext";
import { DashboardProvider } from "./context/DashboardContext";

export default function App() {
  return (
    <AuthProvider>
      <DashboardProvider>
        <AppRoutes />
      </DashboardProvider>
    </AuthProvider>
  );
}