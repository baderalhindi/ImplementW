import { type ReactElement, useId } from 'react';

import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { AxesFormat } from './AxesFormat.tsx';
import { takesAxes } from './axes.ts';
import { DataPanel } from './DataPanel.tsx';
import { type FieldDescriptor, type Visual, type VisualType } from './designApi.ts';
import { asLine, MAX_HEIGHT, retitled, VISUAL_TYPES, withType } from './visuals.ts';

/** The sizes a visual may take where it is: a container's least size, and a child's columns (its container's width). */
export interface SizeBounds {
  minWidth: number;
  minHeight: number;
  maxWidth: number;
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
        <p className="designer__hint">
          {t(
            visual.section === 'DASHBOARD_WIDGET'
              ? 'dashboards.designer.widgetSettings'
              : 'dashboards.designer.sectionSettings',
          )}
        </p>
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
        <div className="field">
          <label className="field__label" htmlFor={`${id}-width`}>
            {t('dashboards.designer.width')}
          </label>
          <input
            id={`${id}-width`}
            className="field__input"
            type="number"
            min={bounds.minWidth}
            max={bounds.maxWidth}
            aria-describedby={`${id}-width-hint`}
            value={visual.layoutWidth}
            onChange={(event) => {
              const width = Number(event.target.value);
              if (Number.isInteger(width)) {
                onChange({ ...visual, layoutWidth: width });
              }
            }}
          />
          <p id={`${id}-width-hint`} className="field__hint">
            {t('dashboards.designer.widthHint', { count: bounds.maxWidth })}
          </p>
        </div>
        <div className="field">
          <label className="field__label" htmlFor={`${id}-height`}>
            {t('dashboards.designer.height')}
          </label>
          <input
            id={`${id}-height`}
            className="field__input"
            type="number"
            min={bounds.minHeight}
            max={MAX_HEIGHT}
            aria-describedby={`${id}-height-hint`}
            value={visual.layoutHeight}
            onChange={(event) => {
              const height = Number(event.target.value);
              if (Number.isInteger(height)) {
                onChange({ ...visual, layoutHeight: height });
              }
            }}
          />
          <p id={`${id}-height-hint`} className="field__hint">
            {t('dashboards.designer.heightHint', { count: MAX_HEIGHT })}
          </p>
        </div>
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
