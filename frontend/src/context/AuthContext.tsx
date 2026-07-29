import { createContext, useEffect, useMemo, useState, type ReactNode } from "react";

import type { User } from "../models/User";
import {
  AUTH_EXPIRED_EVENT,
  ApiRequestError,
  apiGet,
  apiPost,
} from "../services/api/client";

// -----------------------------------------------------------------------------
// Auth transport and context contracts
// -----------------------------------------------------------------------------

interface LoginResponse {
  username: string;
  role: User["role"];
  mustChangePassword: boolean;
}

interface AuthContextType {
  user: User | null;
  mustChangePassword: boolean;
  authError: string | null;
  initializing: boolean;
  isAuthenticated: boolean;
  isAdmin: boolean;
  canConfigure: boolean;
  login: (
    username: string,
    password: string,
    rememberMe: boolean
  ) => Promise<{ authenticated: boolean; mustChangePassword: boolean }>;
  logout: () => void;
}

const STORAGE_KEY = "plantmonitor-auth-user";

const AuthContext = createContext<AuthContextType | undefined>(undefined);

// These helpers keep storage parsing isolated from the provider logic.
function readStoredUser(): User | null {
  const serialized = localStorage.getItem(STORAGE_KEY);

  if (!serialized) {
    return null;
  }

  try {
    return JSON.parse(serialized) as User;
  } catch {
    localStorage.removeItem(STORAGE_KEY);
    return null;
  }
}

function clearStoredAuth() {
  localStorage.removeItem(STORAGE_KEY);
}

// AuthProvider owns login/logout, session persistence, and automatic reaction
// to expired backend tokens.
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => readStoredUser());
  const [authError, setAuthError] = useState<string | null>(null);
  const [initializing, setInitializing] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function loadSession() {
      try {
        const response = await apiGet<LoginResponse>("/auth/session");
        if (cancelled) {
          return;
        }

        const authenticatedUser: User = {
          username: response.username,
          role: response.role,
          mustChangePassword: response.mustChangePassword,
        };

        setUser(authenticatedUser);
        localStorage.setItem(STORAGE_KEY, JSON.stringify(authenticatedUser));
      } catch {
        if (!cancelled) {
          setUser(null);
          clearStoredAuth();
        }
      } finally {
        if (!cancelled) {
          setInitializing(false);
        }
      }
    }

    void loadSession();

    return () => {
      cancelled = true;
    };
  }, []);

  // The API client emits a global event when a protected request comes back as
  // unauthorized so the UI can clear stale sessions consistently.
  useEffect(() => {
    function handleAuthExpired() {
      setUser(null);
      setAuthError("Your session expired. Sign in again.");
      clearStoredAuth();
    }

    window.addEventListener(AUTH_EXPIRED_EVENT, handleAuthExpired);

    return () => {
      window.removeEventListener(AUTH_EXPIRED_EVENT, handleAuthExpired);
    };
  }, []);

  // ---------------------------------------------------------------------------
  // Auth actions
  // ---------------------------------------------------------------------------

  async function login(
    username: string,
    password: string,
    rememberMe: boolean
  ): Promise<{ authenticated: boolean; mustChangePassword: boolean }> {
    setAuthError(null);

    try {
      const response = await apiPost<LoginResponse, { username: string; password: string; rememberMe: boolean }>(
        "/auth/login",
        {
          username,
          password,
          rememberMe,
        }
      );

      const authenticatedUser: User = {
        username: response.username,
        role: response.role,
        mustChangePassword: response.mustChangePassword,
      };

      // Local storage is used for remembered sessions, session storage for
      // browser-lifetime sessions.
      setUser(authenticatedUser);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(authenticatedUser));

      return { authenticated: true, mustChangePassword: response.mustChangePassword };
    } catch (error) {
      setUser(null);

      if (error instanceof ApiRequestError) {
        if (error.status === 401) {
          setAuthError("Invalid username or password.");
        } else if (error.status === 423) {
          setAuthError("Account temporarily locked due to repeated failures.");
        } else if (error.status === 403) {
          setAuthError("You do not have access to this account.");
        } else if (typeof error.status === "number") {
          setAuthError(`Login failed. Backend returned HTTP ${error.status}.`);
        } else {
          setAuthError("Cannot reach backend server. Start backend and try again.");
        }
      } else if (error instanceof Error) {
        setAuthError("Login failed. Please try again.");
      } else {
        setAuthError("Login failed. Please try again.");
      }

      clearStoredAuth();
      return { authenticated: false, mustChangePassword: false };
    }
  }

  function logout() {
    void apiPost<unknown, object>("/auth/logout", {});
    setUser(null);
    setAuthError(null);
    clearStoredAuth();
  }

  // Derived auth capabilities are exposed once here so callers do not repeat
  // role logic throughout the UI.
  const value = useMemo(
    () => ({
      user,
      mustChangePassword: user?.mustChangePassword ?? false,
      authError,
      initializing,
      isAuthenticated: Boolean(user),
      isAdmin: user?.role === "Admin",
      canConfigure: user?.role === "Admin",
      login,
      logout,
    }),
    [authError, initializing, user]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export { AuthContext };
