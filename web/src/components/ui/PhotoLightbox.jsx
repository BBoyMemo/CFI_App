import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';

import AuthenticatedImage from './AuthenticatedImage';

/**
 * The full-size view a thumbnail opens into.
 *
 * A photo on a fault report is evidence, not decoration - a 96px thumbnail cannot show a
 * hairline crack or a part number. Full screen, dark backdrop, tap anywhere outside the
 * photo (or Escape) to close: the same one-handed gesture a phone's own photo viewer uses.
 */
export default function PhotoLightbox({ mediaId, onClose }) {
  const { t } = useTranslation();

  useEffect(() => {
    const onKeyDown = (event) => {
      if (event.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/90 p-4"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
    >
      <button
        type="button"
        onClick={onClose}
        aria-label={t('common.close')}
        className="absolute right-4 top-4 flex h-11 w-11 items-center justify-center rounded-full bg-white/10 text-2xl leading-none text-white hover:bg-white/20"
      >
        ×
      </button>

      {/* Stops the tap that is meant to zoom into the photo from also closing it. */}
      <div onClick={(event) => event.stopPropagation()}>
        <AuthenticatedImage
          mediaId={mediaId}
          alt={t('common.photos')}
          // A fixed box, not just a cap: while loading there is no image yet to size the
          // pulsing placeholder, so it needs an explicit size to be visible at all - and
          // once loaded, that same box is what gives object-contain something to fit the
          // photo inside without cropping or stretching it.
          className="h-[70vh] w-[85vw] rounded object-contain"
        />
      </div>
    </div>
  );
}
