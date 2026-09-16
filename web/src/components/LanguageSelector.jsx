import { useTranslation } from 'react-i18next';

import { SUPPORTED_LANGUAGES, changeLanguage } from '../i18n';

/**
 * Available to every role, on every screen. Workers on the floor speak English, Polish,
 * Bulgarian and Spanish, and a manager may need to read a screen in a worker's language
 * while standing next to them.
 */
export default function LanguageSelector() {
  const { t, i18n } = useTranslation();

  return (
    <select
      aria-label={t('language.label')}
      value={i18n.resolvedLanguage}
      onChange={(event) => changeLanguage(event.target.value)}
      className="min-h-11 shrink-0 rounded border border-cfi-rule bg-cfi-surface px-2 text-sm font-semibold text-cfi-ink"
    >
      {SUPPORTED_LANGUAGES.map((language) => (
        <option key={language.code} value={language.code}>
          {language.short}
        </option>
      ))}
    </select>
  );
}
