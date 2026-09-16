import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { uploadPhoto } from '../../api/endpoints';
import { describeApiError } from '../../api/apiClient';
import ErrorBanner from '../../components/ui/ErrorBanner';

/**
 * Uploads happen as soon as a photo is picked, before the form around it is submitted -
 * the report or closure form only ever carries the media ids it already has, matching
 * how the API expects photos (see CreateWorkOrderRequest.PhotoAssetIds).
 */
export default function PhotoUploader({ assetIds, onChange }) {
  const { t } = useTranslation();
  const inputRef = useRef(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState(null);
  const [previews, setPreviews] = useState([]);

  const handleFiles = async (event) => {
    const files = Array.from(event.target.files ?? []);
    event.target.value = '';
    if (files.length === 0) return;

    setUploading(true);
    setError(null);

    try {
      const uploaded = [];

      for (const file of files) {
        const response = await uploadPhoto(file);
        uploaded.push({ id: response.data.id, name: file.name });
      }

      setPreviews((current) => [...current, ...uploaded]);
      onChange([...assetIds, ...uploaded.map((x) => x.id)]);
    } catch (uploadError) {
      setError(describeApiError(uploadError));
    } finally {
      setUploading(false);
    }
  };

  const removeAt = (index) => {
    const target = previews[index];
    setPreviews((current) => current.filter((_, i) => i !== index));
    onChange(assetIds.filter((id) => id !== target.id));
  };

  return (
    <div>
      <div className="flex flex-wrap gap-2">
        {previews.map((photo, index) => (
          <span
            key={photo.id}
            className="flex items-center gap-1 rounded border border-cfi-rule bg-cfi-sunk px-2 py-1 text-xs"
          >
            {photo.name}
            <button
              type="button"
              onClick={() => removeAt(index)}
              className="font-bold text-cfi-red"
              aria-label={t('common.close')}
            >
              ×
            </button>
          </span>
        ))}
      </div>

      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        multiple
        capture="environment"
        onChange={handleFiles}
        className="hidden"
      />

      <button
        type="button"
        disabled={uploading}
        onClick={() => inputRef.current?.click()}
        className="mt-2 min-h-11 rounded border border-cfi-rule bg-white px-4 text-sm font-semibold text-cfi-brown-dark disabled:opacity-50"
      >
        {uploading ? t('common.uploading') : t('common.addPhoto')}
      </button>

      <ErrorBanner error={error} />
    </div>
  );
}
