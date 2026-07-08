import { createContext, useEffect, useMemo, useState, type ReactNode } from "react";

import type { User } from "../models/User";
import {
  AUTH_EXPIRED_EVENT,
  ApiRequestError,
  apiPost,
  setApiAccessToken,
} from "../services/api/client";

interface LoginResponse {
  accessToken: string;
  username: string;
  role: User["role"];
}

interface AuthContextType {
  user: User | null;
  accessToken: string | null;
  authError: string | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  canConfigure: boolean;
  login: (
    username: string,
    password: string,
    rememberMe: boolean
  ) => Promise<boolean>;
  logout: () => void;
}

const STORAGE_KEY = "plantmonitor-auth-user";
const SESSION_USER_STORAGE_KEY = "plantmonitor-auth-user-session";
const TOKEN_STORAGE_KEY = "plantmonitor-auth-token";

const AuthContext = createContext<AuthContextType | undefined>(undefined);

function readStoredUser(): User | null {
  const serialized =
    localStorage.getItem(STORAGE_KEY) ??
    sessionStorage.getItem(SESSION_USER_STORAGE_KEY);

  if (!serialized) {
    return null;
  }

  try {
    return JSON.parse(serialized) as User;
  } catch {
    localStorage.removeItem(STORAGE_KEY);
    sessionStorage.removeItem(SESSION_USER_STORAGE_KEY);
    return null;
  }
}

function readStoredToken(): string | null {
  return (
    localStorage.getItem(TOKEN_STORAGE_KEY) ??
    sessionStorage.getItem(TOKEN_STORAGE_KEY)
  );
}

function clearStoredAuth() {
  localStorage.removeItem(STORAGE_KEY);
  localStorage.removeItem(TOKEN_STORAGE_KEY);
  sessionStorage.removeItem(SESSION_USER_STORAGE_KEY);
  sessionStorage.removeItem(TOKEN_STORAGE_KEY);
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => readStoredUser());
  const [accessToken, setAccessToken] = useState<string | null>(() =>
    readStoredToken()
  );
  const [authError, setAuthError] = useState<string | null>(null);

  useEffect(() => {
    setApiAccessToken(accessToken);
  }, [accessToken]);

  useEffect(() => {
    function handleAuthExpired() {
      setUser(null);
      setAccessToken(null);
      setAuthError("Your session expired. Sign in again.");
      clearStoredAuth();
    }

    window.addEventListener(AUTH_EXPIRED_EVENT, handleAuthExpired);

    return () => {
      window.removeEventListener(AUTH_EXPIRED_EVENT, handleAuthExpired);
    };
  }, []);

  async function login(
    username: string,
    password: string,
    rememberMe: boolean
  ): Promise<boolean> {
    setAuthError(null);

    try {
      const response = await apiPost<LoginResponse, { username: string; password: string }>(
        "/auth/login",
        {
          username,
          password,
        }
      );

      const authenticatedUser: User = {
        username: response.username,
        role: response.role,
      };

      setUser(authenticatedUser);
      setAccessToken(response.accessToken);

      if (rememberMe) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(authenticatedUser));
        localStorage.setItem(TOKEN_STORAGE_KEY, response.accessToken);
        sessionStorage.removeItem(SESSION_USER_STORAGE_KEY);
        sessionStorage.removeItem(TOKEN_STORAGE_KEY);
      } else {
        localStorage.removeItem(STORAGE_KEY);
        localStorage.removeItem(TOKEN_STORAGE_KEY);
        sessionStorage.setItem(
          SESSION_USER_STORAGE_KEY,
          JSON.stringify(authenticatedUser)
        );
        sessionStorage.setItem(TOKEN_STORAGE_KEY, response.accessToken);
      }

      return true;
    } catch (error) {
      setUser(null);
      setAccessToken(null);

      if (error instanceof ApiRequestError) {
        if (error.status === 401) {
          setAuthError("Invalid username or password.");
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
      return false;
    }
  }

  function logout() {
    setUser(null);
    setAccessToken(null);
    setAuthError(null);
    clearStoredAuth();
  }

  const value = useMemo(
    () => ({
      user,
      accessToken,
      authError,
      isAuthenticated: Boolean(user && accessToken),
      isAdmin: user?.role === "Admin",
      canConfigure: user?.role === "Admin",
      login,
      logout,
    }),
    [accessToken, authError, user]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export { AuthContext };
