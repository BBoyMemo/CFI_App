import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { api, loadSession, saveSession, setUnauthorizedHandler } from '../api/client';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [session, setSession] = useState(() => loadSession());

  const logout = useCallback(() => {
    saveSession(null);
    setSession(null);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(logout);
  }, [logout]);

  // Log out exactly when the token expires, even if the tab just sits open.
  useEffect(() => {
    if (!session) return undefined;
    const ms = new Date(session.expiresAt).getTime() - Date.now();
    const timer = setTimeout(logout, Math.max(ms, 0));
    return () => clearTimeout(timer);
  }, [session, logout]);

  const login = useCallback(async (name, password) => {
    const result = await api.login(name, password);
    const next = { token: result.token, expiresAt: result.expiresAt, user: result.user };
    saveSession(next);
    setSession(next);
  }, []);

  const value = useMemo(
    () => ({
      user: session?.user ?? null,
      isManager: session?.user?.role === 'Manager',
      login,
      logout,
    }),
    [session, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  return useContext(AuthContext);
}
