import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import PhotoUploader from './PhotoUploader';

const emptyForm = {
  rootCause: '',
  correctiveAction: '',
  ableToRepair: true,
  unableToRepairReason: '',
  contractorRequired: false,
  contractorUsed: '',
  downtimeHours: '',
  downtimeMinutes: '',
  toolsAndPartsAccounted: true,
  missingItemsNote: '',
  postDeodorisationIntervention: false,
};

/**
 * Mirrors the paper Factory Equipment Fault Reporting Log field for field - the gates
 * (a reason when a repair fails, a contractor name when one was used, a note when
 * something is missing) match the CK_WorkOrderClosure_* constraints the database itself
 * enforces, so a submission that would be rejected server-side is caught here first.
 */
export default function CloseJobForm({ onSubmit, submitting, error }) {
  const { t } = useTranslation();
  const [form, setForm] = useState(emptyForm);
  const [photoAssetIds, setPhotoAssetIds] = useState([]);

  const set = (field) => (event) => {
    const value = event.target.type === 'checkbox' ? event.target.checked : event.target.value;
    setForm((current) => ({ ...current, [field]: value }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();

    onSubmit({
      rootCause: form.rootCause,
      correctiveAction: form.correctiveAction,
      ableToRepair: form.ableToRepair,
      unableToRepairReason: form.ableToRepair ? null : form.unableToRepairReason,
      contractorRequired: form.contractorRequired,
      contractorUsed: form.contractorRequired ? form.contractorUsed : null,
      downtimeMinutes: (Number(form.downtimeHours) || 0) * 60 + (Number(form.downtimeMinutes) || 0),
      toolsAndPartsAccounted: form.toolsAndPartsAccounted,
      missingItemsNote: form.toolsAndPartsAccounted ? null : form.missingItemsNote,
      postDeodorisationIntervention: form.postDeodorisationIntervention,
      // The costing boxes are off the form - the site does not price repairs here. The
      // columns stay on the closure record, so switching them back on is a UI change.
      partsRequired: null,
      partsPrice: null,
      labourCostPerHour: null,
      poNumber: null,
      photoAssetIds,
    });
  };

  return (
    <Card>
      <h2 className="mb-4 font-semibold text-cfi-brown-dark">{t('workorder.closeForm.title')}</h2>

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <label className="flex flex-col gap-1 text-sm">
          {t('workorder.closeForm.rootCause')}
          <textarea
            required
            rows={2}
            value={form.rootCause}
            onChange={set('rootCause')}
            className="rounded border border-cfi-rule bg-white p-3"
          />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          {t('workorder.closeForm.correctiveAction')}
          <textarea
            required
            rows={2}
            value={form.correctiveAction}
            onChange={set('correctiveAction')}
            className="rounded border border-cfi-rule bg-white p-3"
          />
        </label>

        <fieldset className="rounded border border-cfi-rule p-3">
          <legend className="px-1 text-sm font-medium">{t('workorder.closeForm.ableToRepair')}</legend>
          <div className="flex gap-4 text-sm">
            <label className="flex items-center gap-2">
              <input type="radio" checked={form.ableToRepair} onChange={() => setForm((c) => ({ ...c, ableToRepair: true }))} />
              {t('common.yes')}
            </label>
            <label className="flex items-center gap-2">
              <input type="radio" checked={!form.ableToRepair} onChange={() => setForm((c) => ({ ...c, ableToRepair: false }))} />
              {t('common.no')}
            </label>
          </div>
          {!form.ableToRepair && (
            <input
              required
              value={form.unableToRepairReason}
              onChange={set('unableToRepairReason')}
              placeholder={t('workorder.closeForm.unableToRepairReason')}
              className="mt-2 min-h-11 w-full rounded border border-cfi-rule bg-white px-3"
            />
          )}
        </fieldset>

        <fieldset className="rounded border border-cfi-rule p-3">
          <legend className="px-1 text-sm font-medium">{t('workorder.closeForm.contractorRequired')}</legend>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.contractorRequired} onChange={set('contractorRequired')} />
            {t('common.yes')}
          </label>
          {form.contractorRequired && (
            <input
              required
              value={form.contractorUsed}
              onChange={set('contractorUsed')}
              placeholder={t('workorder.closeForm.contractorUsed')}
              className="mt-2 min-h-11 w-full rounded border border-cfi-rule bg-white px-3"
            />
          )}
        </fieldset>

        {/* Asked as 01:15 rather than 75. Downtime on the floor is talked about in
            hours, and converting in your head at the end of a repair is how a number
            that goes to an auditor gets typed in wrong. */}
        <fieldset>
          <legend className="text-sm">{t('workorder.closeForm.downtime')}</legend>
          <div className="mt-1 flex items-center gap-2">
            <input
              type="number"
              min="0"
              inputMode="numeric"
              aria-label={t('workorder.closeForm.downtimeHours')}
              value={form.downtimeHours}
              onChange={set('downtimeHours')}
              placeholder="00"
              className="min-h-11 w-20 rounded border border-cfi-rule bg-white px-3 text-center"
            />
            <span className="text-lg font-bold text-cfi-brown-dark">:</span>
            <input
              type="number"
              min="0"
              max="59"
              inputMode="numeric"
              aria-label={t('workorder.closeForm.downtimeMinutes')}
              value={form.downtimeMinutes}
              onChange={set('downtimeMinutes')}
              placeholder="00"
              className="min-h-11 w-20 rounded border border-cfi-rule bg-white px-3 text-center"
            />
            <span className="text-sm text-cfi-muted">{t('workorder.closeForm.downtimeHint')}</span>
          </div>
        </fieldset>

        <fieldset className="rounded border border-cfi-rule p-3">
          <legend className="px-1 text-sm font-medium">{t('workorder.closeForm.toolsAndPartsAccounted')}</legend>
          <div className="flex gap-4 text-sm">
            <label className="flex items-center gap-2">
              <input
                type="radio"
                checked={form.toolsAndPartsAccounted}
                onChange={() => setForm((c) => ({ ...c, toolsAndPartsAccounted: true }))}
              />
              {t('common.yes')}
            </label>
            <label className="flex items-center gap-2">
              <input
                type="radio"
                checked={!form.toolsAndPartsAccounted}
                onChange={() => setForm((c) => ({ ...c, toolsAndPartsAccounted: false }))}
              />
              {t('common.no')}
            </label>
          </div>
          {!form.toolsAndPartsAccounted && (
            <input
              required
              value={form.missingItemsNote}
              onChange={set('missingItemsNote')}
              placeholder={t('workorder.closeForm.missingItemsNote')}
              className="mt-2 min-h-11 w-full rounded border border-cfi-rule bg-white px-3"
            />
          )}
        </fieldset>

        <fieldset className="rounded border border-cfi-yellow-dark/40 bg-cfi-yellow/10 p-3">
          <legend className="px-1 text-sm font-medium">{t('workorder.closeForm.postDeodorisation')}</legend>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.postDeodorisationIntervention}
              onChange={set('postDeodorisationIntervention')}
            />
            {t('common.yes')}
          </label>
          {form.postDeodorisationIntervention && (
            <p className="mt-1 text-xs text-cfi-yellow-dark">{t('workorder.closeForm.postDeodorisationHint')}</p>
          )}
        </fieldset>

        <div>
          <span className="text-sm">
            {t('common.photos')} <span className="text-cfi-muted">({t('common.optional')})</span>
          </span>
          <PhotoUploader assetIds={photoAssetIds} onChange={setPhotoAssetIds} />
        </div>

        <ErrorBanner error={error} />

        <Button type="submit" disabled={submitting}>
          {t('workorder.closeForm.submit')}
        </Button>
      </form>
    </Card>
  );
}
