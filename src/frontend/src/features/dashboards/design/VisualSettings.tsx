import { Fragment, type ReactElement, useId, useState } from 'react';

import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { AxesFormat } from './AxesFormat.tsx';
import { takesAxes } from './axes.ts';
import { DataPanel } from './DataPanel.tsx';
import { type FieldDescriptor, type Visual, type VisualType } from './designApi.ts';
import {
  hiddenColumnsOf,
  isLastShown,
  isTableSection,
  type SectionColumn,
  sectionTables,
  withColumnShown,
} from './sectionColumns.ts';
import { isEditableSection, madeEditable } from './tableColumns.ts';
import { asLine, MAX_HEIGHT, retitled, tenth, VISUAL_TYPES, withType } from './visuals.ts';

/** The sizes a visual may take where it is: a container's least size, and a child's columns (its container's width). */
export interface SizeBounds {
  minWidth: number;
  minHeight: number;
  maxWidth: number;
}

/**
 * A size in columns or rows: a decimal with one decimal place (20 Nov 2026), from `min` to `max`. While it has the focus it shows what is being
 * typed ("4." on the way to "4.5"), and each value that is whole or has one decimal within the range is applied as it is typed; on leaving it,
 * it shows the size the visual took.
 */
function SizeInput({
  id,
  label,
  hint,
  value,
  min,
  max,
  onChange,
}: {
  id: string;
  label: string;
  hint: string;
  value: number;
  min: number;
  max: number;
  onChange: (value: number) => void;
}): ReactElement {
  const [typed, setTyped] = useState<string | null>(null);
  return (
    <div className="field">
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        className="field__input"
        type="number"
        inputMode="decimal"
        step={0.1}
        min={min}
        max={max}
        aria-describedby={`${id}-hint`}
        value={typed ?? String(value)}
        onChange={(event) => {
          const text = event.target.value;
          setTyped(text);
          const size = Number(text);
          if (text !== '' && tenth(size) === size && size >= min && size <= max) {
            onChange(size);
          }
        }}
        onBlur={() => {
          setTyped(null);
        }}
      />
      <p id={`${id}-hint`} className="field__hint">
        {hint}
      </p>
    </div>
  );
}

/**
 * A table section's columns (20 Nov 2026, docs/architecture/dashboard-designer-table-columns.md): each ticked is shown, in the table's own
 * words; the first column is always shown and not listed, and the last ticked one may not be unticked. The sector's two tables are listed apart.
 */
function ColumnChoice({
  visual,
  onChange,
}: {
  visual: Visual;
  onChange: (next: Visual) => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const hidden = hiddenColumnsOf(visual);
  const tables = sectionTables(visual.section);
  const boxes = (columns: readonly SectionColumn[]) =>
    columns.map((column) => (
      <label key={column.code} className="field__checkbox">
        <input
          type="checkbox"
          checked={!hidden.has(column.code)}
          disabled={isLastShown(visual, column.code)}
          onChange={(event) => {
            onChange(withColumnShown(visual, column.code, event.target.checked));
          }}
        />
        {t(column.label)}
      </label>
    ));
  return (
    <fieldset className="field field--group designer-well" aria-describedby={`${id}-hint`}>
      <legend className="field__label designer-well__label">
        {t('dashboards.designer.columns')}
      </legend>
      <p id={`${id}-hint`} className="field__hint">
        {t('dashboards.designer.columnsHint')}
      </p>
      {tables.map((table) =>
        table.caption === null ? (
          <Fragment key="columns">{boxes(table.columns)}</Fragment>
        ) : (
          <fieldset key={table.caption} className="designer-columns">
            <legend className="designer-columns__legend">{t(table.caption)}</legend>
            {boxes(table.columns)}
          </fieldset>
        ),
      )}
    </fieldset>
  );
}

/**
 * Make editable (20 Nov 2026, docs/architecture/dashboard-designer-power-table.md): the table section becomes a table visual with the same
 * columns, at the same place, size and titles, whose columns and calculations can then be changed; an unsaved change as any other.
 */
function MakeEditable({
  sectorSpend,
  onClick,
}: {
  sectorSpend: boolean;
  onClick: () => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  return (
    <div className="field designer-make-editable">
      <button type="button" className="button" aria-describedby={id} onClick={onClick}>
        {t('dashboards.designer.makeEditable')}
      </button>
      <p id={id} className="field__hint">
        {t('dashboards.designer.makeEditableHint')}
        {sectorSpend && <> {t('dashboards.designer.makeEditableSectorSpend')}</>}
      </p>
    </div>
  );
}

/**
 * The selected visual's settings: its titles, type, data (DataPanel: values, grouping and filters), a chart's axes (AxesFormat) and size. Only what the type takes is offered, so whatever is chosen
 * saves; a size that would cover another visual is refused by the designer, which says so. A container has its titles and size only.
 */
export function VisualSettings({
  visual,
  fields,
  bounds,
  onChange,
  onDuplicate,
  onRemove,
}: {
  visual: Visual;
  fields: ApiResource<FieldDescriptor[]>;
  bounds: SizeBounds;
  onChange: (next: Visual) => void;
  onDuplicate: () => void;
  onRemove: () => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  // A section shows the dashboard's own figures: only its titles and size are the designer's.
  const isSection = visual.visualType === 'SECTION';
  const isTable = isSection && isTableSection(visual.section);
  const isContainer = visual.visualType === 'CONTAINER';
  // A change of type, values or grouping renames the visual while its titles are still the automatic ones.
  const change = (next: Visual) => {
    onChange(retitled(visual, next));
  };

  return (
    <div className="form designer__form">
      <div className="field">
        <label className="field__label" htmlFor={`${id}-ar`}>
          {t('dashboards.designer.titleAr')}
        </label>
        <input
          id={`${id}-ar`}
          className="field__input"
          dir="rtl"
          lang="ar"
          maxLength={100}
          value={visual.title.ar}
          onChange={(event) => {
            onChange({ ...visual, title: { ...visual.title, ar: event.target.value } });
          }}
        />
      </div>
      <div className="field">
        <label className="field__label" htmlFor={`${id}-en`}>
          {t('dashboards.designer.titleEn')}
        </label>
        <input
          id={`${id}-en`}
          className="field__input"
          dir="ltr"
          lang="en"
          maxLength={100}
          value={visual.title.en}
          onChange={(event) => {
            onChange({ ...visual, title: { ...visual.title, en: event.target.value } });
          }}
        />
      </div>
      {isContainer ? (
        <p className="designer__hint">{t('dashboards.designer.containerSettings')}</p>
      ) : isSection ? (
        <>
          <p className="designer__hint">
            {t(
              visual.section === 'DASHBOARD_WIDGET'
                ? 'dashboards.designer.widgetSettings'
                : isTable
                  ? 'dashboards.designer.tableSectionSettings'
                  : 'dashboards.designer.sectionSettings',
            )}
          </p>
          {isEditableSection(visual.section) && (
            <MakeEditable
              sectorSpend={visual.section === 'SECTOR_TABLE'}
              onClick={() => {
                onChange(madeEditable(visual, hiddenColumnsOf(visual)));
              }}
            />
          )}
          {isTable && <ColumnChoice visual={visual} onChange={onChange} />}
        </>
      ) : (
        <>
          <div className="field">
            <label className="field__label" htmlFor={`${id}-type`}>
              {t('dashboards.designer.typeLabel')}
            </label>
            <select
              id={`${id}-type`}
              className="field__input"
              value={visual.visualType}
              onChange={(event) => {
                change(withType(visual, event.target.value as VisualType, fields.data ?? []));
              }}
            >
              {VISUAL_TYPES.map((type) => (
                <option
                  key={type}
                  value={type}
                  // A line draws fields only: it is offered once the catalogue gives it one.
                  disabled={
                    type === 'LINE' &&
                    visual.visualType !== 'LINE' &&
                    asLine(visual, fields.data ?? []) === null
                  }
                >
                  {t(`dashboards.designer.type.${type}`)}
                </option>
              ))}
            </select>
          </div>
          <DataPanel visual={visual} fields={fields} onChange={change} />
          {takesAxes(visual.visualType) && <AxesFormat visual={visual} onChange={onChange} />}
        </>
      )}
      <div className="designer__size">
        <SizeInput
          id={`${id}-width`}
          label={t('dashboards.designer.width')}
          hint={t('dashboards.designer.widthHint', { count: bounds.maxWidth })}
          value={visual.layoutWidth}
          min={bounds.minWidth}
          max={bounds.maxWidth}
          onChange={(width) => {
            onChange({ ...visual, layoutWidth: width });
          }}
        />
        <SizeInput
          id={`${id}-height`}
          label={t('dashboards.designer.height')}
          hint={t('dashboards.designer.heightHint', { count: MAX_HEIGHT })}
          value={visual.layoutHeight}
          min={bounds.minHeight}
          max={MAX_HEIGHT}
          onChange={(height) => {
            onChange({ ...visual, layoutHeight: height });
          }}
        />
      </div>
      {isContainer && (
        <p className="designer__hint">{t('dashboards.designer.containerSizeHint')}</p>
      )}
      <div className="form__actions">
        {!isSection && !isContainer && (
          <button type="button" className="button" onClick={onDuplicate}>
            {t('dashboards.designer.duplicate')}
          </button>
        )}
        <button type="button" className="button button--danger" onClick={onRemove}>
          {t(
            isContainer
              ? 'dashboards.designer.removeContainer'
              : isSection
                ? 'dashboards.designer.removeSection'
                : 'dashboards.designer.remove',
          )}
        </button>
      </div>
    </div>
  );
}
