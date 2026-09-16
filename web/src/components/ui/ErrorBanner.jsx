import { useTranslation } from 'react-i18next';

/**
 * One shape for every failure the API can hand back - a ProblemDetails title/detail, a
 * FluentValidation field-error list, or a plain network failure - so a form never shows
 * "something went wrong" when the server already said exactly what was wrong.
 */
export default function ErrorBanner({ error }) {
  const { t } = useTranslation();

  if (!error) return null;

  if (error.kind === 'network') {
    return (
      <div className="rounded border border-cfi-red/40 bg-cfi-red/10 p-3 text-sm text-cfi-red">
        {t('status.errorTitle')}
      </div>
    );
  }

  return (
    <div className="rounded border border-cfi-red/40 bg-cfi-red/10 p-3 text-sm text-cfi-red">
      <p className="font-semibold">{error.title}</p>
      {error.detail && <p className="mt-1">{error.detail}</p>}

      {error.fieldErrors && (
        <ul className="mt-2 list-inside list-disc space-y-0.5">
          {Object.entries(error.fieldErrors).map(([field, messages]) => (
            <li key={field}>{Array.isArray(messages) ? messages.join(' ') : messages}</li>
          ))}
        </ul>
      )}

      {error.correlationId && (
        <p className="mt-2 font-mono text-xs text-cfi-red/70">ID: {error.correlationId}</p>
      )}
    </div>
  );
}
