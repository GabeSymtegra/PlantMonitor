import { Navigate, Outlet, useLocation } from "react-router-dom";

import { useAuth } from "../context/useAuth";
import type { UserRole } from "../models/User";

interface ProtectedRouteProps {
  requiredRoles?: UserRole[];
}

export default function ProtectedRoute({ requiredRoles }: ProtectedRouteProps) {
  const { isAuthenticated, user, initializing, mustChangePassword } = useAuth();
  const location = useLocation();

  if (initializing) {
    return null;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (mustChangePassword && location.pathname !== "/change-password") {
    return <Navigate to="/change-password" replace state={{ from: location.pathname }} />;
  }

  if (requiredRoles && requiredRoles.length > 0) {
    const hasRequiredRole = user ? requiredRoles.includes(user.role) : false;

    if (!hasRequiredRole) {
      return (
        <Navigate
          to="/forbidden"
          replace
          state={{
            from: location.pathname,
            requiredRoles,
          }}
        />
      );
    }
  }

  if (
    user?.role === "Operator"
    && location.pathname !== "/status-board"
    && location.pathname !== "/forbidden"
    && location.pathname !== "/change-password"
  ) {
    return <Navigate to="/status-board" replace state={{ from: location.pathname }} />;
  }

  return <Outlet />;
}
