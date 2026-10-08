import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './locales/en.json';
import pl from './locales/pl.json';
import bg from './locales/bg.json';
import es from './locales/es.json';

export const LANGUAGES = [
  { code: 'en', label: 'English' },
  { code: 'pl', label: 'Polski' },
  { code: 'bg', label: 'Български' },
  { code: 'es', label: 'Español' },
];

const STORAGE_KEY = 'dt.lang';

function savedLanguage() {
  try {
    const code = localStorage.getItem(STORAGE_KEY);
    return LANGUAGES.some((l) => l.code === code) ? code : 'en';
  } catch {
    return 'en';
  }
}

// Error codes from the API look like "user.nameTaken", so keys are flattened to
// "errors.user.nameTaken" and looked up whole, with i18next's key separator turned off.
function flatten(obj, prefix = '', out = {}) {
  for (const [key, value] of Object.entries(obj)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value && typeof value === 'object') flatten(value, path, out);
    else out[path] = value;
  }
  return out;
}

i18n.use(initReactI18next).init({
  resources: Object.fromEntries(
    Object.entries({ en, pl, bg, es }).map(([code, data]) => [code, { translation: flatten(data) }]),
  ),
  lng: savedLanguage(),
  fallbackLng: 'en',
  keySeparator: false,
  nsSeparator: false,
  interpolation: { escapeValue: false },
});

document.documentElement.lang = i18n.language;
i18n.on('languageChanged', (lng) => {
  document.documentElement.lang = lng;
  try {
    localStorage.setItem(STORAGE_KEY, lng);
  } catch {
    // Not persisted; the choice still applies for this session.
  }
});

export default i18n;
