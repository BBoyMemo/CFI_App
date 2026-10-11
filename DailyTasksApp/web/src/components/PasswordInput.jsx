import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import Icon from './Icon';

// Password field with an eye button to show what was typed. Used for every password on the site.
export default function PasswordInput({ id, value, onChange, autoComplete, autoFocus, className = '' }) {
  const { t } = useTranslation();
  const [show, setShow] = useState(false);
  return (
    <div className="relative">
      <input
        id={id}
        type={show ? 'text' : 'password'}
        className={`input pr-12 ${className}`}
        value={value}
        maxLength={128}
        autoComplete={autoComplete}
        autoCapitalize="none"
        autoCorrect="off"
        spellCheck={false}
        autoFocus={autoFocus}
        onChange={(e) => onChange(e.target.value)}
      />
      <button
        type="button"
        onClick={() => setShow((v) => !v)}
        className={`absolute inset-y-0 right-1 flex w-10 items-center justify-center rounded-lg ${
          show ? 'text-brown' : 'text-muted'
        } hover:text-brown`}
        aria-label={show ? t('login.hidePassword') : t('login.showPassword')}
        title={show ? t('login.hidePassword') : t('login.showPassword')}
      >
        <Icon name={show ? 'eyeOff' : 'eye'} size={20} />
      </button>
    </div>
  );
}
