import AppRoutes from "./routes/AppRoutes";
import { AuthProvider } from "./context/AuthContext";
import { DashboardProvider } from "./context/DashboardContext";
import { AppThemeProvider } from "./context/ThemeContext";

export default function App() {
  return (
    <AppThemeProvider>
      <AuthProvider>
        <DashboardProvider>
          <AppRoutes />
        </DashboardProvider>
      </AuthProvider>
    </AppThemeProvider>
  );
}