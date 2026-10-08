import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import Icon from './Icon';

// Bottom sheet on phones, centred dialog on wider screens.
export default function Modal({ title, onClose, children, footer }) {
  const { t } = useTranslation();

  useEffect(() => {
    const onKey = (e) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      window.removeEventListener('keydown', onKey);
      document.body.style.overflow = overflow;
    };
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-40 flex items-end justify-center bg-ink/40 sm:items-center sm:p-4"
      onMouseDown={(e) => e.target === e.currentTarget && onClose()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="flex max-h-[92vh] w-full flex-col rounded-t-3xl bg-cream shadow-xl sm:max-w-lg sm:rounded-3xl"
      >
        <div className="flex items-center justify-between px-5 pt-4 pb-2">
          <h2 className="text-lg font-bold text-brown">{title}</h2>
          <button type="button" className="icon-btn" onClick={onClose} aria-label={t('common.close')}>
            <Icon name="close" />
          </button>
        </div>
        <div className="overflow-y-auto px-5 pb-4">{children}</div>
        {footer && (
          <div className="flex gap-2 border-t border-cream-dark px-5 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}

export function ConfirmModal({ message, confirmLabel, onConfirm, onClose, busy, error }) {
  const { t } = useTranslation();
  return (
    <Modal
      title={message}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn-ghost flex-1" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button type="button" className="btn-danger flex-1" onClick={onConfirm} disabled={busy}>
            <Icon name="trash" size={18} />
            {confirmLabel ?? t('common.delete')}
          </button>
        </>
      }
    >
      {error && <p className="text-sm text-danger">{error}</p>}
    </Modal>
  );
}
