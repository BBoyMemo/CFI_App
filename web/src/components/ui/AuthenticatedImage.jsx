import { useEffect, useState } from 'react';

import { apiClient } from '../../api/apiClient';

/**
 * A plain <img src="/api/v1/media/1"> cannot work here: the browser never attaches the
 * Authorization header to an image request, only apiClient's axios instance does. So the
 * bytes are fetched as a blob through apiClient and handed to the <img> as an object URL
 * instead - the one extra step Bearer-token auth requires for anything binary.
 */
export default function AuthenticatedImage({ mediaId, alt, className }) {
  const [objectUrl, setObjectUrl] = useState(null);

  useEffect(() => {
    let cancelled = false;
    let currentUrl = null;

    apiClient
      .get(`/api/v1/media/${mediaId}`, { responseType: 'blob' })
      .then((response) => {
        if (cancelled) return;
        currentUrl = URL.createObjectURL(response.data);
        setObjectUrl(currentUrl);
      })
      .catch(() => {
        /* a missing thumbnail is not worth an error banner - it just does not render */
      });

    return () => {
      cancelled = true;
      if (currentUrl) URL.revokeObjectURL(currentUrl);
    };
  }, [mediaId]);

  if (!objectUrl) {
    return <div className={`animate-pulse bg-cfi-sunk ${className}`} aria-label={alt} />;
  }

  return <img src={objectUrl} alt={alt} className={className} />;
}
