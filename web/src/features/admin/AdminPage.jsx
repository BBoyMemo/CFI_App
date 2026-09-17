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
  activateArea, activateEquipment, activateLine, activateShiftType, activateUnit,
  deactivateArea, deactivateEquipment, deactivateLine, deactivateShiftType, deactivateUnit,
  getAreas, getEquipment, getLines, getShiftTypes, getUnits,
  updateArea, upsertArea, upsertEquipment, upsertLine, upsertShiftType, upsertUnit,
} from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import AttendanceRulesTab from './AttendanceRulesTab';
import PeopleSetupTab from './PeopleSetupTab';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm';

const tabClass = (active) =>
  `min-h-11 rounded px-3 text-sm font-semibold transition-colors ${
    active ? 'bg-cfi-yellow text-cfi-brown-dark' : 'bg-cfi-sunk text-cfi-brown-dark hover:bg-cfi-rule'
  }`;

const TABS = ['units', 'areas', 'lines', 'equipment', 'shiftTypes', 'people', 'attendanceRules'];

export default function AdminPage() {
  const { t } = useTranslation();

  const [tab, setTab] = useState('units');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  // Units are needed on more than one tab (areas and machines both hang off them), so
  // they are fetched once here rather than per tab.
  const units = useApiData(() => getUnits({ pageSize: 100 }));
  const [unitFilter, setUnitFilter] = useState('');

  const areas = useApiData(
    () => getAreas({ pageSize: 200, ...(unitFilter ? { unitId: unitFilter } : {}) }),
    [unitFilter],
  );

  const lines = useApiData(
    () => getLines({ pageSize: 200, ...(unitFilter ? { unitId: unitFilter } : {}) }),
    [unitFilter],
  );

  const equipment = useApiData(
    () => getEquipment({ pageSize: 200, ...(unitFilter ? { unitId: unitFilter } : {}) }),
    [unitFilter],
  );

  const shiftTypes = useApiData(() => getShiftTypes({ pageSize: 50 }));

  const [draft, setDraft] = useState({});
  const set = (key) => (event) => setDraft((current) => ({ ...current, [key]: event.target.value }));

  const runAction = useCallback(async (action, source) => {
    setError(null);
    setBusy(true);
    try {
      await action();
      source.refetch();
      setDraft({});
    } catch (actionError) {
      setError(describeApiError(actionError));
    } finally {
      setBusy(false);
    }
  }, []);

  /** Nothing is ever deleted here - a work order from years ago still has to name its place. */
  const toggleRow = (row, activate, deactivate, source) =>
    runAction(() => (row.isActive ? deactivate(row.id) : activate(row.id)), source);

  const rowActions = (row, activate, deactivate, source) => (
    <div className="flex items-center gap-2">
      <Badge tone={row.isActive ? 'bg-cfi-green/15 text-cfi-green-dark' : 'bg-cfi-sunk text-cfi-muted'}>
        {row.isActive ? t('admin.active') : t('admin.inactive')}
      </Badge>

      {/* Switching something back on needs no question - only taking it away does. */}
      {row.isActive ? (
        <ConfirmButton
          disabled={busy}
          onConfirm={() => toggleRow(row, activate, deactivate, source)}
          confirmLabel={t('admin.deactivate')}
        >
          {t('admin.deactivate')}
        </ConfirmButton>
      ) : (
        <Button
          variant="secondary"
          disabled={busy}
          onClick={() => toggleRow(row, activate, deactivate, source)}
        >
          {t('admin.activate')}
        </Button>
      )}
    </div>
  );

  const unitOptions = units.data?.items ?? [];

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.admin')}</h1>

      <div className="mb-4 flex flex-wrap gap-2">
        {TABS.map((name) => (
          <button key={name} type="button" onClick={() => setTab(name)} className={tabClass(tab === name)}>
            {t(`admin.tab_${name}`)}
          </button>
        ))}
      </div>

      <ErrorBanner error={error} />

      {tab === 'people' && <PeopleSetupTab />}

      {tab === 'attendanceRules' && <AttendanceRulesTab />}

      {(tab === 'areas' || tab === 'equipment') && (
        <label className="mb-4 flex max-w-xs flex-col gap-1 text-sm">
          {t('workorder.unit')}
          <select value={unitFilter} onChange={(event) => setUnitFilter(event.target.value)} className={controlClass}>
            <option value="">{t('admin.allUnits')}</option>
            {unitOptions.map((unit) => (
              <option key={unit.id} value={unit.id}>{unit.name}</option>
            ))}
          </select>
        </label>
      )}

      {tab === 'units' && (
        <>
          <Card>
            <form
              className="flex flex-wrap items-end gap-3"
              onSubmit={(event) => {
                event.preventDefault();
                runAction(() => upsertUnit({
                  name: draft.name,
                  code: draft.code,
                  displayOrder: Number(draft.displayOrder || 0),
                }), units);
              }}
            >
              <label className="flex flex-1 flex-col gap-1 text-sm">
                {t('admin.name')}
                <input required value={draft.name ?? ''} onChange={set('name')} className={controlClass} />
              </label>
              <label className="flex w-32 flex-col gap-1 text-sm">
                {t('admin.code')}
                <input required value={draft.code ?? ''} onChange={set('code')} className={controlClass} />
              </label>
              <label className="flex w-24 flex-col gap-1 text-sm">
                {t('admin.order')}
                <input type="number" value={draft.displayOrder ?? ''} onChange={set('displayOrder')} className={controlClass} />
              </label>
              <Button type="submit" disabled={busy}>{t('admin.add')}</Button>
            </form>
          </Card>

          <div className="mt-4">
            <AsyncSection {...units} isEmpty={unitOptions.length === 0}>
              <div className="flex flex-col gap-2">
                {unitOptions.map((unit) => (
                  <Card key={unit.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                    <span className="font-medium text-cfi-ink">
                      {unit.name} <span className="font-mono text-xs text-cfi-muted">{unit.code}</span>
                    </span>
                    {rowActions(unit, activateUnit, deactivateUnit, units)}
                  </Card>
                ))}
              </div>
            </AsyncSection>
          </div>
        </>
      )}

      {tab === 'areas' && (
        <>
          <Card>
            <form
              className="flex flex-wrap items-end gap-3"
              onSubmit={(event) => {
                event.preventDefault();
                runAction(() => upsertArea({
                  unitId: Number(draft.unitId),
                  name: draft.name,
                  code: draft.code || null,
                  displayOrder: Number(draft.displayOrder || 0),
                  isWorkArea: draft.isWorkArea ?? true,
                }), areas);
              }}
            >
              <label className="flex w-40 flex-col gap-1 text-sm">
                {t('workorder.unit')}
                <select required value={draft.unitId ?? ''} onChange={set('unitId')} className={controlClass}>
                  <option value="" disabled>{t('workorder.selectUnit')}</option>
                  {unitOptions.map((unit) => (
                    <option key={unit.id} value={unit.id}>{unit.name}</option>
                  ))}
                </select>
              </label>
              <label className="flex flex-1 flex-col gap-1 text-sm">
                {t('admin.name')}
                <input required value={draft.name ?? ''} onChange={set('name')} className={controlClass} />
              </label>
              <label className="flex w-28 flex-col gap-1 text-sm">
                {t('admin.code')}
                <input value={draft.code ?? ''} onChange={set('code')} className={controlClass} />
              </label>
              <label className="flex w-24 flex-col gap-1 text-sm">
                {t('admin.order')}
                <input type="number" value={draft.displayOrder ?? ''} onChange={set('displayOrder')} className={controlClass} />
              </label>
              {/* Checked by default - a plant or utility room nobody is stationed in is
                  the exception, not the rule. */}
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={draft.isWorkArea ?? true}
                  onChange={(event) =>
                    setDraft((current) => ({ ...current, isWorkArea: event.target.checked }))
                  }
                />
                {t('admin.isWorkArea')}
              </label>
              <Button type="submit" disabled={busy}>{t('admin.add')}</Button>
            </form>
          </Card>

          <div className="mt-4">
            <AsyncSection {...areas} isEmpty={(areas.data?.items ?? []).length === 0}>
              <div className="flex flex-col gap-2">
                {(areas.data?.items ?? []).map((area) => (
                  <Card key={area.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                    <span className="font-medium text-cfi-ink">
                      {area.name}
                      <span className="ml-2 text-xs text-cfi-muted">{area.unitName}</span>
                      {!area.isWorkArea && (
                        <span className="ml-2">
                          <Badge>{t('admin.notWorkArea')}</Badge>
                        </span>
                      )}
                    </span>
                    <div className="flex items-center gap-2">
                      {/* Reversible metadata, not a lifecycle change like activate/deactivate -
                          a plain toggle needs no confirmation. */}
                      <Button
                        variant="secondary"
                        disabled={busy}
                        onClick={() => runAction(() => updateArea(area.id, {
                          unitId: area.unitId, name: area.name, code: area.code,
                          displayOrder: area.displayOrder, isWorkArea: !area.isWorkArea,
                        }), areas)}
                      >
                        {area.isWorkArea ? t('admin.markNotWorkArea') : t('admin.markWorkArea')}
                      </Button>
                      {rowActions(area, activateArea, deactivateArea, areas)}
                    </div>
                  </Card>
                ))}
              </div>
            </AsyncSection>
          </div>
        </>
      )}

      {tab === 'lines' && (
        <>
          <Card>
            <form
              className="flex flex-wrap items-end gap-3"
              onSubmit={(event) => {
                event.preventDefault();
                runAction(() => upsertLine({
                  unitId: Number(draft.unitId),
                  areaId: draft.areaId ? Number(draft.areaId) : null,
                  name: draft.name,
                  displayOrder: Number(draft.displayOrder || 0),
                }), lines);
              }}
            >
              <label className="flex w-40 flex-col gap-1 text-sm">
                {t('workorder.unit')}
                <select required value={draft.unitId ?? ''} onChange={set('unitId')} className={controlClass}>
                  <option value="" disabled>{t('workorder.selectUnit')}</option>
                  {unitOptions.map((unit) => (
                    <option key={unit.id} value={unit.id}>{unit.name}</option>
                  ))}
                </select>
              </label>

              <label className="flex w-48 flex-col gap-1 text-sm">
                {t('workorder.area')}
                <select value={draft.areaId ?? ''} onChange={set('areaId')} className={controlClass}>
                  <option value="">{t('workorder.selectArea')}</option>
                  {(areas.data?.items ?? [])
                    .filter((area) => String(area.unitId) === String(draft.unitId))
                    .map((area) => (
                      <option key={area.id} value={area.id}>{area.name}</option>
                    ))}
                </select>
              </label>

              <label className="flex flex-1 flex-col gap-1 text-sm">
                {t('admin.name')}
                <input required value={draft.name ?? ''} onChange={set('name')} className={controlClass} />
              </label>
              <label className="flex w-24 flex-col gap-1 text-sm">
                {t('admin.order')}
                <input type="number" value={draft.displayOrder ?? ''} onChange={set('displayOrder')} className={controlClass} />
              </label>
              <Button type="submit" disabled={busy}>{t('admin.add')}</Button>
            </form>
          </Card>

          <div className="mt-4">
            <AsyncSection {...lines} isEmpty={(lines.data?.items ?? []).length === 0}>
              <div className="flex flex-col gap-2">
                {(lines.data?.items ?? []).map((line) => (
                  <Card key={line.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                    <span className="font-medium text-cfi-ink">
                      {line.name}
                      <span className="ml-2 text-xs text-cfi-muted">
                        {(areas.data?.items ?? []).find((area) => area.id === line.areaId)?.name}
                      </span>
                    </span>
                    {rowActions(line, activateLine, deactivateLine, lines)}
                  </Card>
                ))}
              </div>
            </AsyncSection>
          </div>
        </>
      )}

      {tab === 'equipment' && (
        <>
          <Card>
            <form
              className="flex flex-wrap items-end gap-3"
              onSubmit={(event) => {
                event.preventDefault();
                runAction(() => upsertEquipment({
                  unitId: Number(draft.unitId),
                  areaId: draft.areaId ? Number(draft.areaId) : null,
                  lineId: draft.lineId ? Number(draft.lineId) : null,
                  parentEquipmentId: draft.parentEquipmentId ? Number(draft.parentEquipmentId) : null,
                  name: draft.name,
                  iconKey: draft.iconKey || null,
                  displayOrder: Number(draft.displayOrder || 0),
                }), equipment);
              }}
            >
              <label className="flex w-40 flex-col gap-1 text-sm">
                {t('workorder.unit')}
                <select required value={draft.unitId ?? ''} onChange={set('unitId')} className={controlClass}>
                  <option value="" disabled>{t('workorder.selectUnit')}</option>
                  {unitOptions.map((unit) => (
                    <option key={unit.id} value={unit.id}>{unit.name}</option>
                  ))}
                </select>
              </label>

              <label className="flex w-48 flex-col gap-1 text-sm">
                {t('workorder.area')}
                <select value={draft.areaId ?? ''} onChange={set('areaId')} className={controlClass}>
                  <option value="">{t('workorder.selectArea')}</option>
                  {(areas.data?.items ?? [])
                    .filter((area) => String(area.unitId) === String(draft.unitId))
                    .map((area) => (
                      <option key={area.id} value={area.id}>{area.name}</option>
                    ))}
                </select>
              </label>

              <label className="flex w-44 flex-col gap-1 text-sm">
                {t('workorder.line')}
                <select value={draft.lineId ?? ''} onChange={set('lineId')} className={controlClass}>
                  <option value="">{t('workorder.selectLine')}</option>
                  {(lines.data?.items ?? [])
                    .filter((line) => String(line.areaId ?? '') === String(draft.areaId ?? ''))
                    .map((line) => (
                      <option key={line.id} value={line.id}>{line.name}</option>
                    ))}
                </select>
              </label>

              {/* Filled in only for a part of a bigger machine. The list offers whole
                  machines standing in the same place, because that is the rule the API
                  enforces - a part cannot live somewhere its machine does not. */}
              <label className="flex w-48 flex-col gap-1 text-sm">
                {t('admin.parentMachine')}
                <select
                  value={draft.parentEquipmentId ?? ''}
                  onChange={set('parentEquipmentId')}
                  className={controlClass}
                >
                  <option value="">{t('admin.noParentMachine')}</option>
                  {(equipment.data?.items ?? [])
                    .filter((machine) => !machine.parentEquipmentId
                      && String(machine.unitId) === String(draft.unitId)
                      && String(machine.areaId ?? '') === String(draft.areaId ?? '')
                      && String(machine.lineId ?? '') === String(draft.lineId ?? ''))
                    .map((machine) => (
                      <option key={machine.id} value={machine.id}>{machine.name}</option>
                    ))}
                </select>
              </label>

              <label className="flex flex-1 flex-col gap-1 text-sm">
                {t('admin.name')}
                <input required value={draft.name ?? ''} onChange={set('name')} className={controlClass} />
              </label>
              <label className="flex w-24 flex-col gap-1 text-sm">
                {t('admin.order')}
                <input type="number" value={draft.displayOrder ?? ''} onChange={set('displayOrder')} className={controlClass} />
              </label>
              <Button type="submit" disabled={busy}>{t('admin.add')}</Button>
            </form>
          </Card>

          <div className="mt-4">
            <AsyncSection {...equipment} isEmpty={(equipment.data?.items ?? []).length === 0}>
              <div className="flex flex-col gap-2">
                {(equipment.data?.items ?? []).map((machine) => (
                  <Card key={machine.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                    <span className="font-medium text-cfi-ink">
                      {machine.name}
                      {machine.parentEquipmentName && (
                        <span className="ml-2 text-xs text-cfi-muted">
                          {t('workorder.partOf', { machine: machine.parentEquipmentName })}
                        </span>
                      )}
                    </span>
                    {rowActions(machine, activateEquipment, deactivateEquipment, equipment)}
                  </Card>
                ))}
              </div>
            </AsyncSection>
          </div>
        </>
      )}

      {tab === 'shiftTypes' && (
        <>
          <Card>
            <form
              className="flex flex-wrap items-end gap-3"
              onSubmit={(event) => {
                event.preventDefault();
                runAction(() => upsertShiftType({
                  name: draft.name,
                  startTime: `${draft.startTime}:00`,
                  endTime: `${draft.endTime}:00`,
                  displayOrder: Number(draft.displayOrder || 0),
                }), shiftTypes);
              }}
            >
              <label className="flex flex-1 flex-col gap-1 text-sm">
                {t('admin.name')}
                <input required value={draft.name ?? ''} onChange={set('name')} className={controlClass} />
              </label>
              <label className="flex w-32 flex-col gap-1 text-sm">
                {t('admin.startTime')}
                <input required type="time" value={draft.startTime ?? ''} onChange={set('startTime')} className={controlClass} />
              </label>
              <label className="flex w-32 flex-col gap-1 text-sm">
                {t('admin.endTime')}
                <input required type="time" value={draft.endTime ?? ''} onChange={set('endTime')} className={controlClass} />
              </label>
              <label className="flex w-24 flex-col gap-1 text-sm">
                {t('admin.order')}
                <input type="number" value={draft.displayOrder ?? ''} onChange={set('displayOrder')} className={controlClass} />
              </label>
              <Button type="submit" disabled={busy}>{t('admin.add')}</Button>
            </form>
          </Card>

          <div className="mt-4">
            <AsyncSection {...shiftTypes} isEmpty={(shiftTypes.data?.items ?? []).length === 0}>
              <div className="flex flex-col gap-2">
                {(shiftTypes.data?.items ?? []).map((type) => (
                  <Card key={type.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                    <span className="font-medium text-cfi-ink">
                      {type.name}
                      <span className="ml-2 font-mono text-xs text-cfi-muted">
                        {type.startTime.slice(0, 5)}–{type.endTime.slice(0, 5)}
                      </span>
                    </span>
                    {rowActions(type, activateShiftType, deactivateShiftType, shiftTypes)}
                  </Card>
                ))}
              </div>
            </AsyncSection>
          </div>
        </>
      )}
    </div>
  );
}
