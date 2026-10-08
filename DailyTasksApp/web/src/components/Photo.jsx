import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { fetchPhotoUrl } from '../api/client';
import Icon from './Icon';

// Optional single-photo input with a preview. Opens the camera directly on phones.
export function PhotoPicker({ file, onChange, error }) {
  const { t } = useTranslation();
  const input = useRef(null);
  const [preview, setPreview] = useState(null);

  useEffect(() => {
    if (!file) {
      setPreview(null);
      return undefined;
    }
    const url = URL.createObjectURL(file);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);

  return (
    <div>
      <input
        ref={input}
        type="file"
        accept="image/*"
        capture="environment"
        className="hidden"
        onChange={(e) => {
          onChange(e.target.files?.[0] ?? null);
          e.target.value = '';
        }}
      />
      {preview ? (
        <div className="relative inline-block">
          <img src={preview} alt="" className="h-28 w-28 rounded-xl object-cover ring-1 ring-cream-dark" />
          <button
            type="button"
            onClick={() => onChange(null)}
            aria-label={t('photo.remove')}
            className="absolute -top-2 -right-2 flex h-7 w-7 items-center justify-center rounded-full bg-danger text-white shadow"
          >
            <Icon name="close" size={16} />
          </button>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => input.current?.click()}
          className="flex h-28 w-28 flex-col items-center justify-center gap-1 rounded-xl border-2 border-dashed border-cream-dark bg-white text-muted hover:border-yellow hover:text-brown"
        >
          <Icon name="camera" size={26} />
          <span className="text-xs">{t('photo.add')}</span>
        </button>
      )}
      {error && <p className="mt-1 text-sm text-danger">{error}</p>}
    </div>
  );
}

// Thumbnail of a stored photo; tap to see it full screen. Loaded with the bearer token.
export function StoredPhoto({ path }) {
  const { t } = useTranslation();
  const [url, setUrl] = useState(null);
  const [failed, setFailed] = useState(false);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    let revoked = false;
    let objectUrl = null;
    fetchPhotoUrl(path)
      .then((u) => {
        objectUrl = u;
        if (revoked) URL.revokeObjectURL(u);
        else setUrl(u);
      })
      .catch(() => !revoked && setFailed(true));
    return () => {
      revoked = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [path]);

  if (failed) {
    return (
      <span className="inline-flex items-center gap-1 text-xs text-muted">
        <Icon name="alert" size={14} />
        {t('errors.photo.notFound')}
      </span>
    );
  }
  if (!url) return <div className="h-16 w-16 animate-pulse rounded-lg bg-cream-dark" />;

  return (
    <>
      <button type="button" onClick={() => setOpen(true)} aria-label={t('photo.view')}>
        <img src={url} alt="" className="h-16 w-16 rounded-lg object-cover ring-1 ring-cream-dark" />
      </button>
      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/85 p-4" onClick={() => setOpen(false)}>
          <img src={url} alt="" className="max-h-full max-w-full rounded-lg" />
          <button
            type="button"
            className="absolute top-4 right-4 flex h-10 w-10 items-center justify-center rounded-full bg-white/90 text-ink"
            aria-label={t('common.close')}
          >
            <Icon name="close" />
          </button>
        </div>
      )}
    </>
  );
}

// Several photos for one record: what is already stored plus newly picked files.
// `stored` = [{ id, path }], `added` = File objects. The parent decides what happens on save.
export function MultiPhotoPicker({ stored, added, onRemoveStored, onAdd, onRemoveAdded, max }) {
  const { t } = useTranslation();
  const input = useRef(null);
  const room = max - stored.length - added.length;

  return (
    <div className="flex flex-wrap gap-2">
      {stored.map((p) => (
        <div key={p.id} className="relative">
          <StoredPhoto path={p.path} />
          <RemoveButton label={t('photo.remove')} onClick={() => onRemoveStored(p.id)} />
        </div>
      ))}
      {added.map((file, i) => (
        <div key={`${file.name}-${i}`} className="relative">
          <LocalPreview file={file} />
          <RemoveButton label={t('photo.remove')} onClick={() => onRemoveAdded(i)} />
        </div>
      ))}
      {room > 0 && (
        <>
          <input
            ref={input}
            type="file"
            accept="image/*"
            multiple
            className="hidden"
            onChange={(e) => {
              const files = Array.from(e.target.files ?? []).slice(0, room);
              if (files.length) onAdd(files);
              e.target.value = '';
            }}
          />
          <button
            type="button"
            onClick={() => input.current?.click()}
            className="flex h-16 w-16 flex-col items-center justify-center gap-0.5 rounded-lg border-2 border-dashed border-cream-dark bg-white text-muted hover:border-yellow hover:text-brown"
            aria-label={t('photo.add')}
            title={t('photo.add')}
          >
            <Icon name="camera" size={20} />
            <span className="text-[10px]">{stored.length + added.length}/{max}</span>
          </button>
        </>
      )}
    </div>
  );
}

function LocalPreview({ file }) {
  const [url, setUrl] = useState(null);
  useEffect(() => {
    const u = URL.createObjectURL(file);
    setUrl(u);
    return () => URL.revokeObjectURL(u);
  }, [file]);
  return url ? <img src={url} alt="" className="h-16 w-16 rounded-lg object-cover ring-1 ring-cream-dark" /> : null;
}

function RemoveButton({ label, onClick }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      className="absolute -top-1.5 -right-1.5 flex h-6 w-6 items-center justify-center rounded-full bg-danger text-white shadow"
    >
      <Icon name="close" size={14} />
    </button>
  );
}
