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
  const [areaIds, setAreaIds] = useState([]);
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  // Areas are picked one unit at a time, but the selection carries across units - somebody
  // can work the Filling Room in Unit 1 and the Yard weighbridge on the same week.
  const { data: areasPage } = useApiData(
    () => (unitId ? getAreas({ unitId, pageSize: 100 }) : empty),
    [unitId],
  );

  const areas = useMemo(() => areasPage?.items ?? [], [areasPage]);

  const toggleArea = (id) =>
    setAreaIds((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]));

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      // No department is chosen here: the role already says which side of the site
      // somebody is on, and asking twice only creates a way to get it wrong.
      await approveUser(user.id, {
        roleId: Number(roleId),
        occupationId: null,
        areaIds,
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

      <select value={unitId} onChange={(event) => setUnitId(event.target.value)} className={controlClass}>
        <option value="">{t('workorder.selectUnit')}</option>
        {units.map((unit) => (
          <option key={unit.id} value={unit.id}>
            {unit.name}
          </option>
        ))}
      </select>

      {unitId && (
        <div className="flex flex-wrap gap-2">
          {areas.map((area) => (
            <label
              key={area.id}
              className={`flex items-center gap-1 rounded border px-2 py-1 text-sm ${
                areaIds.includes(area.id) ? 'border-cfi-yellow-dark bg-cfi-yellow/20' : 'border-cfi-rule'
              }`}
            >
              <input type="checkbox" checked={areaIds.includes(area.id)} onChange={() => toggleArea(area.id)} />
              {area.name}
            </label>
          ))}
        </div>
      )}

      {areaIds.length > 0 && (
        <p className="text-sm text-cfi-muted">{t('team.areasChosen', { count: areaIds.length })}</p>
      )}

      <ErrorBanner error={error} />

      <Button type="submit" disabled={submitting || !roleId} className="w-fit">
        {t('team.approve')}
      </Button>
    </form>
  );
}
