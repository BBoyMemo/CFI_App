import { useTranslation } from 'react-i18next';
import Icon from './Icon';

// Small 🌐 chip next to translated text; clicking switches between translation and original.
export default function TranslationToggle({ showOriginal, onToggle, className = '' }) {
  const { t } = useTranslation();
  return (
    <button
      type="button"
      onClick={(e) => {
        e.stopPropagation();
        onToggle();
      }}
      className={`inline-flex items-center gap-1 rounded-full bg-cream px-2 py-0.5 text-xs font-medium text-muted hover:text-brown ${className}`}
    >
      <Icon name="globe" size={12} />
      {showOriginal ? t('translation.showTranslation') : `${t('translation.translated')} · ${t('translation.showOriginal')}`}
    </button>
  );
}
