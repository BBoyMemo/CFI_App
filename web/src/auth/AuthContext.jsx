import { useCallback, useEffect, useState } from 'react';

import { getMe, login as loginRequest, logout as logoutRequest } from '../api/endpoints';
import { tokenStore } from '../api/apiClient';
import { changeLanguage } from '../i18n';
import { AuthContext } from './authContext';

export function AuthProvider({ children }) {
  const [me, setMe] = useState(null);
  // No token means nothing to restore, so the initial state is already "not loading" -
  // the effect below then only ever has a reason to call setLoading when it is actually
  // about to fetch something, never as a synchronous no-op on mount.
  const [loading, setLoading] = useState(() => Boolean(tokenStore.getAccessToken()));

  // Plain function, not useCallback: applyProfileResponse/applyProfileFailure only ever
  // read closed-over setters, so a fresh reference per render costs nothing, and it lets
  // both the mount effect and login() share one implementation without the effect calling
  // out to a memoized function (oxlint's set-state-in-effect check flags that shape even
  // though these setState calls only ever run after an await has already resolved).
  const applyProfileResponse = (response) => {
    setMe(response.data);

    // The account's own saved language wins over whatever the browser had picked
    // locally, so a person's choice follows them from device to device.
    if (response.data.preferredLanguage) {
      changeLanguage(response.data.preferredLanguage);
    }
  };

  const applyProfileFailure = () => {
    setMe(null);
    tokenStore.clear();
  };

  const loadProfile = useCallback(async () => {
    try {
      applyProfileResponse(await getMe());
    } catch (error) {
      applyProfileFailure();
      throw error;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!tokenStore.getAccessToken()) return undefined;

    let cancelled = false;

    getMe()
      .then((response) => {
        if (!cancelled) applyProfileResponse(response);
      })
      .catch(() => {
        if (!cancelled) applyProfileFailure();
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const login = useCallback(
    async (email, password) => {
      const response = await loginRequest({ email, password, deviceId: null });
      tokenStore.save(response.data.accessToken, response.data.refreshToken);
      await loadProfile();
    },
    [loadProfile],
  );

  const logout = useCallback(async () => {
    const refreshToken = tokenStore.getRefreshToken();
    tokenStore.clear();
    setMe(null);

    if (refreshToken) {
      // Best effort - the user is signed out locally regardless of whether this reaches
      // the server, so a flaky connection on the way out never traps someone in a session
      // they meant to leave.
      try {
        await logoutRequest(refreshToken);
      } catch {
        /* already signed out locally */
      }
    }
  }, []);

  const hasPermission = useCallback(
    (permission) => Boolean(me?.permissions?.includes(permission)),
    [me],
  );

  return (
    <AuthContext.Provider value={{ me, loading, login, logout, refreshMe: loadProfile, hasPermission }}>
      {children}
    </AuthContext.Provider>
  );
}
