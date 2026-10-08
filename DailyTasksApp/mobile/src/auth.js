import React, {createContext, useCallback, useContext, useEffect, useMemo, useState} from 'react';
import {api, loadStored, saveServer, saveSession, setUnauthorizedHandler} from './api';

const AuthContext = createContext(null);

export function AuthProvider({children}) {
  const [ready, setReady] = useState(false);
  const [server, setServer] = useState('');
  const [session, setSession] = useState(null);

  useEffect(() => {
    loadStored().then(stored => {
      setServer(stored.server);
      setSession(stored.session);
      setReady(true);
    });
  }, []);

  const logout = useCallback(() => {
    saveSession(null);
    setSession(null);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(logout);
  }, [logout]);

  useEffect(() => {
    if (!session) {
      return undefined;
    }
    const ms = new Date(session.expiresAt).getTime() - Date.now();
    const timer = setTimeout(logout, Math.max(ms, 0));
    return () => clearTimeout(timer);
  }, [session, logout]);

  const login = useCallback(async (serverAddress, name, password) => {
    await saveServer(serverAddress);
    setServer(serverAddress);
    const result = await api.login(name, password);
    const next = {token: result.token, expiresAt: result.expiresAt, user: result.user};
    await saveSession(next);
    setSession(next);
  }, []);

  const value = useMemo(
    () => ({
      ready,
      server,
      user: session?.user ?? null,
      isManager: session?.user?.role === 'Manager',
      login,
      logout,
    }),
    [ready, server, session, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  return useContext(AuthContext);
}
