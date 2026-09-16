import { useCallback, useEffect, useState } from 'react';

import { describeApiError } from '../api/apiClient';

/**
 * Fetch-on-mount plus a refetch handle, with the loading/error bookkeeping every screen
 * in this app needs written once instead of copy-pasted into each page.
 *
 * The request lives inside the effect and checks a `cancelled` flag before touching
 * state, so a response that arrives after the component (or its deps) moved on is
 * discarded instead of updating dead state.
 */
export function useApiData(fetcher, deps = []) {
  const [state, setState] = useState({ status: 'loading', data: null, error: null });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setState((current) => ({ ...current, status: 'loading' }));

    fetcher()
      .then((response) => {
        if (!cancelled) setState({ status: 'ready', data: response.data, error: null });
      })
      .catch((error) => {
        if (!cancelled) setState({ status: 'error', data: null, error: describeApiError(error) });
      });

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, attempt]);

  const refetch = useCallback(() => setAttempt((current) => current + 1), []);

  return { ...state, refetch };
}
