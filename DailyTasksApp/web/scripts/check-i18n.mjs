// Fails when a locale is missing a key that en.json has, or has one en.json does not.
import { readFileSync } from 'node:fs';

const load = (code) => JSON.parse(readFileSync(new URL(`../src/i18n/locales/${code}.json`, import.meta.url)));
const keys = (obj, prefix = '') =>
  Object.entries(obj).flatMap(([k, v]) => (v && typeof v === 'object' ? keys(v, `${prefix}${k}.`) : [`${prefix}${k}`]));

const reference = new Set(keys(load('en')));
let failed = false;
for (const code of ['pl', 'bg', 'es']) {
  const actual = new Set(keys(load(code)));
  const missing = [...reference].filter((k) => !actual.has(k));
  const extra = [...actual].filter((k) => !reference.has(k));
  if (missing.length || extra.length) {
    failed = true;
    console.error(`${code}: missing [${missing.join(', ')}] extra [${extra.join(', ')}]`);
  }
}
if (failed) process.exit(1);
console.log(`i18n OK: ${reference.size} keys in every locale.`);
