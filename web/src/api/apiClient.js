import axios from 'axios';

import { API_BASE_URL } from '../config/env';
import { newUuid } from './uuid';

export const CORRELATION_HEADER = 'X-Correlation-Id';

const ACCESS_TOKEN_KEY = 'cfiapp.accessToken';
const REFRESH_TOKEN_KEY = 'cfiapp.refreshToken';

export const tokenStore = {
  getAccessToken: () => localStorage.getItem(ACCESS_TOKEN_KEY),
  getRefreshToken: () => localStorage.getItem(REFRESH_TOKEN_KEY),
  save: (accessToken, refreshToken) => {
    localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
  },
  clear: () => {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
  },
};

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
});

apiClient.interceptors.request.use((config) => {
  config.headers[CORRELATION_HEADER] = newUuid();

  const token = tokenStore.getAccessToken();
  if (token && !config.url?.includes('/auth/login') && !config.url?.includes('/auth/register')) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

/**
 * A single in-flight refresh is shared by every request that hits a 401 at the same
 * moment (e.g. three widgets loading in parallel right as the token expires), so the
 * refresh endpoint is not hammered three times for one expiry.
 */
let refreshPromise = null;

const refreshAccessToken = async () => {
  refreshPromise ??= apiClient
    .post('/api/v1/auth/refresh', { refreshToken: tokenStore.getRefreshToken() })
    .then((response) => {
      tokenStore.save(response.data.accessToken, response.data.refreshToken);
      return response.data.accessToken;
    })
    .finally(() => {
      refreshPromise = null;
    });

  return refreshPromise;
};

/**
 * 401 means the session itself is gone - refresh once, retry once, and only sign out if
 * that fails too. 403 means the session is fine but this action is not allowed, so it is
 * never treated as a sign-out reason; that distinction is the whole reason this app does
 * not log people out at random the way the previous system did.
 */
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const { config, response } = error;

    if (response?.status !== 401 || config.__isRetry || config.url?.includes('/auth/refresh')) {
      return Promise.reject(error);
    }

    if (!tokenStore.getRefreshToken()) {
      tokenStore.clear();
      window.location.href = '/login';
      return Promise.reject(error);
    }

    try {
      await refreshAccessToken();
      return apiClient({ ...config, __isRetry: true });
    } catch (refreshError) {
      tokenStore.clear();
      window.location.href = '/login';
      return Promise.reject(refreshError);
    }
  },
);

/**
 * Normalises every failure into one shape, so screens never have to know whether the
 * server answered with RFC 7807 ProblemDetails, FluentValidation's field-error shape,
 * plain text, or nothing at all.
 */
export const describeApiError = (error) => {
  if (error.response) {
    const problem = error.response.data;
    const fieldErrors = problem?.errors ?? null;

    return {
      kind: 'response',
      status: error.response.status,
      title: problem?.title ?? `HTTP ${error.response.status}`,
      detail: problem?.detail ?? null,
      fieldErrors,
      correlationId: problem?.correlationId ?? error.response.headers?.[CORRELATION_HEADER.toLowerCase()] ?? null,
    };
  }

  if (error.request) {
    return { kind: 'network', status: null, title: null, detail: null, fieldErrors: null, correlationId: null };
  }

  return { kind: 'client', status: null, title: error.message, detail: null, fieldErrors: null, correlationId: null };
};
