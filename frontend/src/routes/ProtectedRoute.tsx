import { Navigate, Outlet, useLocation } from "react-router-dom";

import { useAuth } from "../context/useAuth";
import type { UserRole } from "../models/User";

interface ProtectedRouteProps {
  requiredRoles?: UserRole[];
}

export default function ProtectedRoute({ requiredRoles }: ProtectedRouteProps) {
  const { isAuthenticated, user } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
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

  return <Outlet />;
}
