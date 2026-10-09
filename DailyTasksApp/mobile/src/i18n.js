import AsyncStorage from '@react-native-async-storage/async-storage';
import i18n from 'i18next';
import {initReactI18next} from 'react-i18next';
// Shared with the web app (see metro.config.js).
import en from '../../web/src/i18n/locales/en.json';
import pl from '../../web/src/i18n/locales/pl.json';
import bg from '../../web/src/i18n/locales/bg.json';
import fil from '../../web/src/i18n/locales/fil.json';

export const LANGUAGES = [
  {code: 'en', label: 'EN'},
  {code: 'pl', label: 'PL'},
  {code: 'bg', label: 'BG'},
  {code: 'fil', label: 'FIL'},
];

const STORAGE_KEY = 'dt.lang';

// API error codes contain dots ("user.nameTaken"), so keys are flattened and looked up whole.
function flatten(obj, prefix = '', out = {}) {
  for (const [key, value] of Object.entries(obj)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value && typeof value === 'object') {
      flatten(value, path, out);
    } else {
      out[path] = value;
    }
  }
  return out;
}

i18n.use(initReactI18next).init({
  resources: Object.fromEntries(
    Object.entries({en, pl, bg, fil}).map(([code, data]) => [code, {translation: flatten(data)}]),
  ),
  lng: 'en',
  fallbackLng: 'en',
  keySeparator: false,
  nsSeparator: false,
  interpolation: {escapeValue: false},
  compatibilityJSON: 'v4',
});

AsyncStorage.getItem(STORAGE_KEY)
  .then(code => code && LANGUAGES.some(l => l.code === code) && i18n.changeLanguage(code))
  .catch(() => {});

export function setLanguage(code) {
  i18n.changeLanguage(code);
  AsyncStorage.setItem(STORAGE_KEY, code).catch(() => {});
}

export default i18n;
