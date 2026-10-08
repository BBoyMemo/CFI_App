import { useTranslation } from 'react-i18next';
import { LANGUAGES } from '../i18n';
import Icon from './Icon';

export default function LanguageSelect({ dark = false }) {
  const { t, i18n } = useTranslation();
  return (
    <label
      className={`relative flex h-9 items-center gap-1 rounded-lg px-2 text-sm ${
        dark ? 'text-cream/80 hover:bg-cream/10' : 'text-muted hover:bg-cream-dark'
      }`}
      title={t('nav.language')}
    >
      <Icon name="globe" size={18} />
      <span className="font-semibold uppercase">{i18n.language}</span>
      <select
        value={i18n.language}
        onChange={(e) => i18n.changeLanguage(e.target.value)}
        className="absolute inset-0 cursor-pointer opacity-0"
        aria-label={t('nav.language')}
      >
        {LANGUAGES.map((l) => (
          <option key={l.code} value={l.code}>
            {l.label}
          </option>
        ))}
      </select>
    </label>
  );
}
