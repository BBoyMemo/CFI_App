import React, {createContext, useCallback, useContext, useEffect, useMemo, useState} from 'react';
import {
  api,
  currentToken,
  loadStored,
  saveSession,
  setUnauthorizedHandler,
} from './api';

const AuthContext = createContext(null);

export function AuthProvider({children}) {
  const [ready, setReady] = useState(false);
  const [session, setSession] = useState(null);

  const logout = useCallback(() => {
    saveSession(null);
    setSession(null);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(logout);
  }, [logout]);

  // Takes a login-shaped answer ({token, expiresAt, user}), e.g. after a rename.
  const startSession = useCallback(async result => {
    const next = {token: result.token, expiresAt: result.expiresAt, user: result.user};
    await saveSession(next);
    setSession(next);
  }, []);

  // Renews the stored session in the background. A 401 logs out (in api.js); being offline
  // keeps the stored session as it is.
  const renew = useCallback(
    async token => {
      try {
        const result = await api.renew();
        if (currentToken() === token) {
          await startSession(result);
        }
      } catch {}
    },
    [startSession],
  );

  useEffect(() => {
    loadStored().then(stored => {
      setSession(stored.session);
      setReady(true);
      if (stored.session) {
        renew(stored.session.token);
      }
    });
  }, [renew]);

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
