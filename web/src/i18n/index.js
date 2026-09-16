import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import en from './locales/en.json';
import pl from './locales/pl.json';
import bg from './locales/bg.json';
import es from './locales/es.json';

/**
 * Every role gets a language selector, not just operators. The chosen language is
 * stored per user on the server from Phase 2 onwards; until there is a logged in user,
 * it falls back to this browser-local value so a refresh does not reset it.
 */
export const SUPPORTED_LANGUAGES = [
  // The flag and the code, the way the approved prototypes show them: on a shared tablet
  // somebody picks their language by recognising it, not by reading a sentence.
  { code: 'en', label: 'English', short: '🇬🇧 EN' },
  { code: 'pl', label: 'Polski', short: '🇵🇱 PL' },
  { code: 'bg', label: 'Български', short: '🇧🇬 BG' },
  { code: 'es', label: 'Español', short: '🇪🇸 ES' },
];

const STORAGE_KEY = 'cfiapp.language';

const readStoredLanguage = () => {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return SUPPORTED_LANGUAGES.some((language) => language.code === stored) ? stored : null;
  } catch {
    // Private browsing and locked-down devices throw here; English is a safe default.
    return null;
  }
};

i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    pl: { translation: pl },
    bg: { translation: bg },
    es: { translation: es },
  },
  lng: readStoredLanguage() ?? 'en',
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
});

export const changeLanguage = (code) => {
  i18n.changeLanguage(code);
  try {
    localStorage.setItem(STORAGE_KEY, code);
  } catch {
    // Not being able to remember the choice is not a reason to block the change.
  }
};

export default i18n;
