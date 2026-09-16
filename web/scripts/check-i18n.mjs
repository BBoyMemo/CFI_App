/**
 * Fails the build when a translation key exists in one language but not another.
 *
 * The factory floor runs in four languages. A missing key does not crash the app -
 * it silently shows an English string, or a raw key, to someone who cannot read it.
 * That class of bug never surfaces in review, so CI has to catch it.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const localesDir = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'i18n', 'locales');
const referenceLanguage = 'en';

const flatten = (value, prefix = '') =>
  Object.entries(value).flatMap(([key, child]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    return child !== null && typeof child === 'object' ? flatten(child, path) : [path];
  });

const languages = readdirSync(localesDir)
  .filter((name) => name.endsWith('.json'))
  .map((name) => name.replace(/\.json$/, ''));

if (!languages.includes(referenceLanguage)) {
  console.error(`Reference language "${referenceLanguage}.json" is missing from ${localesDir}`);
  process.exit(1);
}

const keysByLanguage = new Map(
  languages.map((language) => [
    language,
    new Set(flatten(JSON.parse(readFileSync(join(localesDir, `${language}.json`), 'utf8')))),
  ]),
);

const referenceKeys = keysByLanguage.get(referenceLanguage);
let failed = false;

for (const language of languages) {
  if (language === referenceLanguage) continue;

  const keys = keysByLanguage.get(language);
  const missing = [...referenceKeys].filter((key) => !keys.has(key));
  const extra = [...keys].filter((key) => !referenceKeys.has(key));

  if (missing.length > 0) {
    failed = true;
    console.error(`[${language}] missing ${missing.length} key(s): ${missing.join(', ')}`);
  }

  if (extra.length > 0) {
    failed = true;
    console.error(`[${language}] has ${extra.length} key(s) not in ${referenceLanguage}: ${extra.join(', ')}`);
  }
}

if (failed) process.exit(1);

console.log(
  `Translations complete: ${referenceKeys.size} keys x ${languages.length} languages (${languages.join(', ')})`,
);
