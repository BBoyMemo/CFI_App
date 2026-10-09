// Free-text fields come with translations from the server: { language: { field: text } }.
// The reader sees their own language when a translation exists, otherwise the original.

export function original(item, field) {
  return item[field];
}

export function localized(item, field, language, showOriginal = false) {
  const source = original(item, field);
  if (showOriginal || !source) {
    return source;
  }
  return item.translations?.[language]?.[field] ?? source;
}

// True when, for this reader, at least one of the fields reads differently from the original.
export function hasTranslation(item, fields, language) {
  return fields.some(field => {
    const source = original(item, field);
    const translated = item.translations?.[language]?.[field];
    return Boolean(source && translated && translated !== source);
  });
}
