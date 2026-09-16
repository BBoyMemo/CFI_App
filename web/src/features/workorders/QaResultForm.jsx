import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';

export default function QaResultForm({ onSubmit, submitting, error }) {
  const { t } = useTranslation();
  const [result, setResult] = useState('Pass');
  const [note, setNote] = useState('');

  const handleSubmit = (event) => {
    event.preventDefault();
    // The API deserialises enums by their numeric ordinal, not by name - see api/enums.js.
    onSubmit({ result: result === 'Pass' ? 1 : 2, note: note || null });
  };

  const noteMissing = result === 'Fail' && note.trim().length === 0;

  return (
    <Card className="border-cfi-yellow-dark/40 bg-cfi-yellow/10">
      <h2 className="mb-4 font-semibold text-cfi-brown-dark">{t('workorder.qa.title')}</h2>

      <form onSubmit={handleSubmit} className="flex flex-col gap-3">
        <div className="flex gap-4 text-sm">
          <label className="flex items-center gap-2">
            <input type="radio" checked={result === 'Pass'} onChange={() => setResult('Pass')} />
            {t('workorder.qa.pass')}
          </label>
          <label className="flex items-center gap-2">
            <input type="radio" checked={result === 'Fail'} onChange={() => setResult('Fail')} />
            {t('workorder.qa.fail')}
          </label>
        </div>

        <label className="flex flex-col gap-1 text-sm">
          {t('workorder.qa.note')}
          <textarea
            rows={2}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            className="rounded border border-cfi-rule bg-white p-3"
          />
          {result === 'Fail' && <span className="text-xs text-cfi-muted">{t('workorder.qa.noteRequiredOnFail')}</span>}
        </label>

        <ErrorBanner error={error} />

        <Button type="submit" disabled={submitting || noteMissing} variant={result === 'Fail' ? 'danger' : 'primary'}>
          {t('workorder.qa.submit')}
        </Button>
      </form>
    </Card>
  );
}
