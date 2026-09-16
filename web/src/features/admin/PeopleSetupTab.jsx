import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ConfirmButton from '../../components/ui/ConfirmButton';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import {
  activateDepartment, activateOccupation, deactivateDepartment, deactivateOccupation,
  getDepartments, getOccupations, upsertDepartment, upsertOccupation,
} from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm';

/**
 * Departments and occupations - the two lists the site was always meant to manage itself
 * (notes 01, §5: "fabrika büyüdükçe kod değişikliği gerekmeden yönetilebilecek").
 *
 * A department is who somebody answers to and drives what their manager can see, so it is
 * switched off rather than deleted: a work order from two years ago still has to be able
 * to say which department raised it.
 *
 * An occupation is what somebody does - Packer, FLT Driver - which is a different question
 * from the role that decides what they may press.
 */
function EditableList({ titleKey, hintKey, source, create, activate, deactivate, emptyKey }) {
  const { t } = useTranslation();

  const [name, setName] = useState('');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const rows = source.data?.items ?? [];

  const runAction = useCallback(async (action, reset) => {
    setError(null);
    setBusy(true);
    try {
      await action();
      source.refetch();
      if (reset) reset();
    } catch (actionError) {
      setError(describeApiError(actionError));
    } finally {
      setBusy(false);
    }
  }, [source]);

  return (
    <section>
      <h2 className="mb-1 text-lg font-semibold text-cfi-brown-dark">{t(titleKey)}</h2>
      <p className="mb-3 text-sm text-cfi-muted">{t(hintKey)}</p>

      <Card>
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            runAction(() => create({ name }), () => setName(''));
          }}
        >
          <label className="flex flex-1 flex-col gap-1 text-sm">
            {t('admin.name')}
            <input required value={name} onChange={(e) => setName(e.target.value)} className={controlClass} />
          </label>
          <Button type="submit" disabled={busy || !name.trim()}>{t('admin.add')}</Button>
        </form>
      </Card>

      <ErrorBanner error={error} />

      <div className="mt-3">
        <AsyncSection {...source} isEmpty={rows.length === 0} emptyKey={emptyKey}>
          <div className="flex flex-col gap-2">
            {rows.map((row) => (
              <Card key={row.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                <span className="font-medium text-cfi-ink">{row.name}</span>

                <div className="flex items-center gap-2">
                  <Badge tone={row.isActive ? 'bg-cfi-green/15 text-cfi-green-dark' : 'bg-cfi-sunk text-cfi-muted'}>
                    {row.isActive ? t('admin.active') : t('admin.inactive')}
                  </Badge>
                  {row.isActive ? (
                    <ConfirmButton
                      disabled={busy}
                      onConfirm={() => runAction(() => deactivate(row.id))}
                      confirmLabel={t('admin.deactivate')}
                    >
                      {t('admin.deactivate')}
                    </ConfirmButton>
                  ) : (
                    <Button
                      variant="secondary"
                      disabled={busy}
                      onClick={() => runAction(() => activate(row.id))}
                    >
                      {t('admin.activate')}
                    </Button>
                  )}
                </div>
              </Card>
            ))}
          </div>
        </AsyncSection>
      </div>
    </section>
  );
}

export default function PeopleSetupTab() {
  const departments = useApiData(() => getDepartments({ pageSize: 100 }));
  const occupations = useApiData(() => getOccupations({ pageSize: 100 }));

  return (
    <div className="flex flex-col gap-8">
      <EditableList
        titleKey="admin.departmentsTitle"
        hintKey="admin.departmentsHint"
        source={departments}
        create={upsertDepartment}
        activate={activateDepartment}
        deactivate={deactivateDepartment}
        emptyKey="admin.noDepartments"
      />

      <EditableList
        titleKey="admin.occupationsTitle"
        hintKey="admin.occupationsHint"
        source={occupations}
        create={upsertOccupation}
        activate={activateOccupation}
        deactivate={deactivateOccupation}
        emptyKey="admin.noOccupations"
      />
    </div>
  );
}
