import { createContext, useMemo, useState, type ReactNode } from "react";

import type { User } from "../models/User";

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  login: (username: string, password: string) => boolean;
  logout: () => void;
}

const STORAGE_KEY = "plantmonitor-auth-user";

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

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => readStoredUser());

  function login(username: string, password: string): boolean {
    if (username === "admin" && password === "admin") {
      const authenticatedUser: User = { username: "admin" };
      setUser(authenticatedUser);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(authenticatedUser));
      return true;
    }

    return false;
  }

  function logout() {
    setUser(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  const value = useMemo(
    () => ({
      user,
      isAuthenticated: Boolean(user),
      login,
      logout,
    }),
    [user]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export { AuthContext };
