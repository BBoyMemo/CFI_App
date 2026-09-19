import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from './Button';

/**
 * A destructive action that asks first.
 *
 * The confirmation is inline rather than a browser dialog or a modal: on a phone held in
 * one glove a modal covers the row you were looking at, and `window.confirm` cannot say
 * *which* machine is about to go. Here the question replaces the button in place, so the
 * name of the thing stays on screen next to it.
 *
 * Almost nothing is truly deleted in this app - reference data is switched off, people are
 * disabled, jobs are rejected. A shift drawn up but not yet used is the exception, and it is
 * exactly the case where "did I just do that to the wrong row" matters most, so every one of
 * them comes through here.
 */
export default function ConfirmButton({
  onConfirm,
  question,
  confirmLabel,
  children,
  disabled = false,
  variant = 'secondary',
  className = '',
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);

  if (!asking) {
    return (
      <Button
        type="button"
        variant={variant}
        disabled={disabled}
        onClick={() => setAsking(true)}
        className={className}
      >
        {children}
      </Button>
    );
  }

  return (
    <div className="flex flex-wrap items-center justify-end gap-2">
      <span className="text-sm text-cfi-brown-dark">{question ?? t('common.confirmQuestion')}</span>

      <Button
        type="button"
        variant="danger"
        disabled={disabled}
        onClick={async () => {
          // Closed first: leaving the question up after a successful action reads as if
          // nothing happened, and a second tap would fire it again.
          setAsking(false);
          await onConfirm();
        }}
      >
        {confirmLabel ?? t('common.confirmYes')}
      </Button>

      <Button type="button" variant="secondary" onClick={() => setAsking(false)}>
        {t('common.cancel')}
      </Button>
    </div>
  );
}
