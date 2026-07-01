import { createContext, useMemo, useState, type ReactNode } from "react";

import type { User } from "../models/User";
import { apiPost } from "../services/api/client";

interface LoginResponse {
  accessToken: string;
  username: string;
  role: User["role"];
}

interface AuthContextType {
  user: User | null;
  accessToken: string | null;
  isAuthenticated: boolean;
  login: (
    username: string,
    password: string,
    rememberMe: boolean
  ) => Promise<boolean>;
  logout: () => void;
}

const STORAGE_KEY = "plantmonitor-auth-user";
const TOKEN_STORAGE_KEY = "plantmonitor-auth-token";

const AuthContext = createContext<AuthContextType | undefined>(undefined);

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

function readStoredToken(): string | null {
  return (
    localStorage.getItem(TOKEN_STORAGE_KEY) ??
    sessionStorage.getItem(TOKEN_STORAGE_KEY)
  );
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => readStoredUser());
  const [accessToken, setAccessToken] = useState<string | null>(() =>
    readStoredToken()
  );

  async function login(
    username: string,
    password: string,
    rememberMe: boolean
  ): Promise<boolean> {
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
        sessionStorage.removeItem(TOKEN_STORAGE_KEY);
      } else {
        localStorage.removeItem(STORAGE_KEY);
        localStorage.removeItem(TOKEN_STORAGE_KEY);
        sessionStorage.setItem(TOKEN_STORAGE_KEY, response.accessToken);
      }

      return true;
    } catch {
      setUser(null);
      setAccessToken(null);
      localStorage.removeItem(STORAGE_KEY);
      localStorage.removeItem(TOKEN_STORAGE_KEY);
      sessionStorage.removeItem(TOKEN_STORAGE_KEY);
      return false;
    }
  }

  function logout() {
    setUser(null);
    setAccessToken(null);
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    sessionStorage.removeItem(TOKEN_STORAGE_KEY);
  }

  const value = useMemo(
    () => ({
      user,
      accessToken,
      isAuthenticated: Boolean(user),
      login,
      logout,
    }),
    [accessToken, user]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export { AuthContext };
