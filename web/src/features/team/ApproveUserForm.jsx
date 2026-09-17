import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from '../../components/ui/Button';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { approveUser, getAreas } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm';

const empty = Promise.resolve({ data: { items: [] } });

export default function ApproveUserForm({ user, roles, units, onApproved }) {
  const { t } = useTranslation();
  const [roleId, setRoleId] = useState('');
  const [unitId, setUnitId] = useState('');
  const [areaId, setAreaId] = useState('');
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const { data: areasPage } = useApiData(
    () => (unitId ? getAreas({ unitId, pageSize: 100 }) : empty),
    [unitId],
  );

  // Only rooms with people stationed in them - the Boiler House and the P Tanks Room are
  // real, active places, but nobody stands in either of them, so they never belong on a
  // "works in" form.
  const areas = useMemo(
    () => (areasPage?.items ?? []).filter((area) => area.isWorkArea),
    [areasPage],
  );

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      // No department is chosen here: the role already says which side of the site
      // somebody is on, and asking twice only creates a way to get it wrong.
      //
      // One room, not several: the person stands in one place, and the report form reads
      // that single room straight off this to skip asking them again.
      await approveUser(user.id, {
        roleId: Number(roleId),
        occupationId: null,
        areaIds: areaId ? [Number(areaId)] : [],
      });
      onApproved();
    } catch (submitError) {
      setError(describeApiError(submitError));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="mt-3 flex flex-col gap-3 border-t border-cfi-rule pt-3">
      <select
        required
        value={roleId}
        onChange={(event) => setRoleId(event.target.value)}
        className={controlClass}
      >
        <option value="" disabled>
          {t('team.selectRole')}
        </option>
        {roles.map((role) => (
          <option key={role.id} value={role.id}>
            {role.name}
          </option>
        ))}
      </select>

      <p className="text-sm text-cfi-muted">{t('team.worksIn')}</p>

      <select
        value={unitId}
        onChange={(event) => {
          setUnitId(event.target.value);
          setAreaId('');
        }}
        className={controlClass}
      >
        <option value="">{t('workorder.selectUnit')}</option>
        {units.map((unit) => (
          <option key={unit.id} value={unit.id}>
            {unit.name}
          </option>
        ))}
      </select>

      {/* One room, not several - the report form hides both the unit and the room the
          moment it knows exactly where somebody stands, and it can only know that when
          there is exactly one answer here. */}
      {unitId && (
        <select value={areaId} onChange={(event) => setAreaId(event.target.value)} className={controlClass}>
          <option value="">{t('workorder.selectArea')}</option>
          {areas.map((area) => (
            <option key={area.id} value={area.id}>
              {area.name}
            </option>
          ))}
        </select>
      )}

      <ErrorBanner error={error} />

      <Button type="submit" disabled={submitting || !roleId} className="w-fit">
        {t('team.approve')}
      </Button>
    </form>
  );
}
