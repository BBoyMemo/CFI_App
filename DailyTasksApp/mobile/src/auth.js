import React, {createContext, useCallback, useContext, useEffect, useMemo, useState} from 'react';
import {api, loadStored, saveSession, setUnauthorizedHandler} from './api';

const AuthContext = createContext(null);

export function AuthProvider({children}) {
  const [ready, setReady] = useState(false);
  const [session, setSession] = useState(null);

  useEffect(() => {
    loadStored().then(stored => {
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

  // Takes a login-shaped answer ({token, expiresAt, user}), e.g. after a rename.
  const startSession = useCallback(async result => {
    const next = {token: result.token, expiresAt: result.expiresAt, user: result.user};
    await saveSession(next);
    setSession(next);
  }, []);

  const login = useCallback(
    async (name, password) => startSession(await api.login(name, password)),
    [startSession],
  );

  // Changes to the signed-in user that need no new token (e.g. the password was changed).
  const updateUser = useCallback(patch => {
    setSession(current => {
      if (!current) {
        return current;
      }
      const next = {...current, user: {...current.user, ...patch}};
      saveSession(next);
      return next;
    });
  }, []);

  const value = useMemo(
    () => ({
      ready,
      user: session?.user ?? null,
      isManager: session?.user?.role === 'Manager',
      login,
      logout,
      startSession,
      updateUser,
    }),
    [ready, session, login, logout, startSession, updateUser],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  return useContext(AuthContext);
}
