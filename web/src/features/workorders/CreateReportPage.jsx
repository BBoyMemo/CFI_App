import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { createWorkOrder, getUnits, getEquipment, getAreas, getLines } from '../../api/endpoints';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';
import PhotoUploader from './PhotoUploader';

const PRIORITY_OPTIONS = [0, 1, 2]; // Low, Medium, High - see api/enums.js

/** Sentinel for the machine dropdown: picked when what broke is not on the list yet. */
const OTHER = 'other';

const selectClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3';

const empty = Promise.resolve({ data: { items: [] } });

export default function CreateReportPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { me } = useAuth();

  const [manualUnitId, setManualUnitId] = useState('');
  const [manualAreaId, setManualAreaId] = useState('');
  const [lineId, setLineId] = useState('');
  const [equipmentId, setEquipmentId] = useState('');
  const [equipmentFreeText, setEquipmentFreeText] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState(1);
  const [photoAssetIds, setPhotoAssetIds] = useState([]);
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const { data: unitsPage } = useApiData(() => getUnits({ pageSize: 100 }));

  // Somebody posted to specific rooms reports from where they stand, not from a menu of
  // every unit on site - the approval screen already asked a manager which rooms this
  // person works in, and re-asking here only invites picking the wrong one.
  const hasOwnAreas = (me?.areaIds?.length ?? 0) > 0;

  const { data: homeAreasPage } = useApiData(
    () => (hasOwnAreas ? getAreas({ pageSize: 100 }) : empty),
    [hasOwnAreas],
  );

  const homeAreas = useMemo(
    () => (homeAreasPage?.items ?? []).filter((area) => me?.areaIds?.includes(area.id)),
    [homeAreasPage, me],
  );

  // Locking the unit down only makes sense when every room this person works in is in the
  // same one - somebody posted to a room in Unit 1 and another in the Yard still needs the
  // full picker, same as the approval screen lets a manager assign across units.
  const homeUnitIds = useMemo(() => [...new Set(homeAreas.map((area) => area.unitId))], [homeAreas]);
  const lockedUnitId = homeUnitIds.length === 1 ? homeUnitIds[0] : null;

  // The room itself locks down only when there is exactly one of them - the case the
  // approval form now produces by default, since it hands out a single room, not several.
  // Somebody still carrying more than one room from before this change keeps picking
  // among just their own, same as the unit above.
  const lockedAreaId = homeAreas.length === 1 ? homeAreas[0].id : null;

  // Derived, not stored: whichever unit or room is in force is read straight off the
  // locked value or the manual pick, so there is no state to fall out of sync with the
  // other.
  const unitId = lockedUnitId ? String(lockedUnitId) : manualUnitId;
  const areaId = lockedAreaId ? String(lockedAreaId) : manualAreaId;

  // Each level waits for the one above it: no unit, no rooms; no room, every machine in
  // the unit. Picking higher up clears what was chosen below, so the form can never be
  // submitted with a machine that does not belong to the place it names.
  //
  // Skipped once the unit is locked down: the room choices come from homeAreas instead,
  // so fetching every room in the unit again would just be thrown away.
  const { data: areasPage } = useApiData(
    () => (unitId && !lockedUnitId ? getAreas({ unitId, pageSize: 100 }) : empty),
    [unitId, lockedUnitId],
  );

  // Only the filling room runs lines, so this step appears there and nowhere else.
  const { data: linesPage } = useApiData(
    () => (areaId ? getLines({ unitId, areaId, pageSize: 100 }) : empty),
    [unitId, areaId],
  );

  // Fetched by room and narrowed to the line here rather than in a second request: a room
  // holds a couple of dozen machines at most, and the line step has to be able to offer
  // "no line" as well, which the API cannot express as a filter.
  const { data: equipmentPage } = useApiData(
    () => (unitId ? getEquipment({ unitId, ...(areaId ? { areaId } : {}), pageSize: 200 }) : empty),
    [unitId, areaId],
  );

  const units = unitsPage?.items ?? [];
  const areas = lockedUnitId ? homeAreas : (areasPage?.items ?? []);
  const lines = linesPage?.items ?? [];

  /**
   * The machine list as it should read: every machine in the chosen place, each followed
   * by its own parts. Blender 2 and its two FIBCs stay together, so picking the part is
   * one movement rather than a hunt through an alphabetical list.
   */
  const machines = useMemo(() => {
    const all = equipmentPage?.items ?? [];

    const inPlace = lineId
      ? all.filter((item) => String(item.lineId ?? '') === lineId)
      : all.filter((item) => (lines.length > 0 ? !item.lineId : true));

    const partsByParent = new Map();
    inPlace
      .filter((item) => item.parentEquipmentId)
      .forEach((part) => {
        const siblings = partsByParent.get(part.parentEquipmentId) ?? [];
        partsByParent.set(part.parentEquipmentId, [...siblings, part]);
      });

    return inPlace
      .filter((item) => !item.parentEquipmentId)
      .flatMap((machine) => [machine, ...(partsByParent.get(machine.id) ?? [])]);
  }, [equipmentPage, lineId, lines.length]);

  const isOther = equipmentId === OTHER;

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const response = await createWorkOrder({
        unitId: Number(unitId),
        areaId: areaId ? Number(areaId) : null,
        lineId: lineId ? Number(lineId) : null,
        equipmentId: isOther || !equipmentId ? null : Number(equipmentId),
        equipmentFreeText: isOther ? equipmentFreeText : null,
        priority: Number(priority),
        description,
        photoAssetIds,
      });

      navigate(`/workorders/${response.data.id}`, {
        state: { flash: t('workorder.reportSubmitted', { number: response.data.number }) },
      });
    } catch (submitError) {
      setError(describeApiError(submitError));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="mx-auto max-w-xl">
      <h1 className="text-xl font-bold text-cfi-brown-dark">{t('workorder.createTitle')}</h1>
      <p className="mt-1 text-sm text-cfi-muted">{t('workorder.createSubtitle')}</p>

      <Card className="mt-4">
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          {/* Hidden once we know which single unit this person's rooms are all in - they
              are standing there, so asking them to name it is one more tap for nothing. */}
          {!lockedUnitId && (
            <label className="flex flex-col gap-1 text-sm">
              {t('workorder.unit')}
              <select
                required
                value={unitId}
                onChange={(event) => {
                  setManualUnitId(event.target.value);
                  setManualAreaId('');
                  setLineId('');
                  setEquipmentId('');
                }}
                className={selectClass}
              >
                <option value="" disabled>
                  {t('workorder.selectUnit')}
                </option>
                {units.map((unit) => (
                  <option key={unit.id} value={unit.id}>
                    {unit.name}
                  </option>
                ))}
              </select>
            </label>
          )}

          {/* Hidden once this person has exactly one room to work in - they are standing
              in it, so the form goes straight to what broke instead of naming the room
              they are already stood in. */}
          {!lockedAreaId && (
            <label className="flex flex-col gap-1 text-sm">
              {t('workorder.area')}
              <select
                value={areaId}
                disabled={!unitId}
                onChange={(event) => {
                  setManualAreaId(event.target.value);
                  setLineId('');
                  setEquipmentId('');
                }}
                className={selectClass}
              >
                <option value="">{t('workorder.selectArea')}</option>
                {areas.map((area) => (
                  <option key={area.id} value={area.id}>
                    {area.name}
                  </option>
                ))}
              </select>
            </label>
          )}

          {/* Hidden where it would only be an extra tap: most rooms run no lines. */}
          {lines.length > 0 && (
            <label className="flex flex-col gap-1 text-sm">
              {t('workorder.line')}
              <select
                value={lineId}
                onChange={(event) => {
                  setLineId(event.target.value);
                  setEquipmentId('');
                }}
                className={selectClass}
              >
                <option value="">{t('workorder.selectLine')}</option>
                {lines.map((line) => (
                  <option key={line.id} value={line.id}>
                    {line.name}
                  </option>
                ))}
              </select>
            </label>
          )}

          <label className="flex flex-col gap-1 text-sm">
            {t('workorder.equipment')}
            <select
              required
              value={equipmentId}
              disabled={!unitId}
              onChange={(event) => setEquipmentId(event.target.value)}
              className={selectClass}
            >
              <option value="" disabled>
                {t('workorder.selectEquipment')}
              </option>
              {machines.map((item) => (
                <option key={item.id} value={item.id}>
                  {/* A part reads as belonging to its machine, not as a machine of its own. */}
                  {item.parentEquipmentId ? `\u00a0\u00a0\u2014 ${item.name}` : item.name}
                </option>
              ))}
              {/* Always last, and always there: the list is never complete enough to stop
                  somebody reporting a fault on something new. */}
              <option value={OTHER}>{t('workorder.equipmentOther')}</option>
            </select>
          </label>

          {isOther && (
            <label className="flex flex-col gap-1 text-sm">
              {t('workorder.equipmentFreeTextLabel')}
              <input
                required
                value={equipmentFreeText}
                onChange={(event) => setEquipmentFreeText(event.target.value)}
                placeholder={t('workorder.equipmentFreeTextPlaceholder')}
                className={selectClass}
              />
            </label>
          )}

          <label className="flex flex-col gap-1 text-sm">
            {t('workorder.priority')}
            <select
              value={priority}
              onChange={(event) => setPriority(event.target.value)}
              className={selectClass}
            >
              {PRIORITY_OPTIONS.map((value) => (
                <option key={value} value={value}>
                  {t(`workorder.priority_${['Low', 'Medium', 'High'][value]}`)}
                </option>
              ))}
            </select>
          </label>

          {/* Not required. The photograph and the machine name usually say more than a
              line typed one-handed on the floor, and a report nobody files is worse than a
              short one. */}
          <label className="flex flex-col gap-1 text-sm">
            {t('workorder.description')} <span className="text-cfi-muted">({t('common.optional')})</span>
            <textarea
              rows={4}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder={t('workorder.descriptionPlaceholder')}
              className="rounded border border-cfi-rule bg-white p-3"
            />
          </label>

          <div>
            <span className="text-sm">{t('common.photos')} <span className="text-cfi-muted">({t('common.optional')})</span></span>
            <PhotoUploader assetIds={photoAssetIds} onChange={setPhotoAssetIds} />
          </div>

          <ErrorBanner error={error} />

          <Button type="submit" disabled={submitting || !unitId || !equipmentId}>
            {t('workorder.submitReport')}
          </Button>
        </form>
      </Card>
    </div>
  );
}
