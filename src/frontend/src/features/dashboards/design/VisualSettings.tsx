import { type ReactElement, useId } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type GroupBy, type Measure, type Visual, type VisualType } from './designApi.ts';
import {
  COLUMNS,
  groupBysFor,
  MAX_HEIGHT,
  MAX_MEASURES,
  measuresFor,
  retitled,
  VISUAL_TYPES,
  withType,
} from './visuals.ts';

/**
 * The selected visual's settings: its titles, type, values, grouping and size. Only what the type takes is offered, so whatever is chosen
 * saves; a size that would cover another visual is refused by the designer, which says so.
 */
export function VisualSettings({
  visual,
  onChange,
  onDuplicate,
  onRemove,
}: {
  visual: Visual;
  onChange: (next: Visual) => void;
  onDuplicate: () => void;
  onRemove: () => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const max = MAX_MEASURES[visual.visualType];
  const groupings = groupBysFor(visual.visualType);
  // A change of values or grouping renames the visual while its titles are still the automatic ones.
  const change = (next: Visual) => {
    onChange(retitled(visual, next));
  };

  const toggle = (measure: Measure, on: boolean) => {
    const measures = on
      ? [...visual.measures, measure]
      : visual.measures.filter((candidate) => candidate !== measure);
    if (measures.length > 0) {
      change({ ...visual, measures });
    }
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
      <div className="field">
        <label className="field__label" htmlFor={`${id}-type`}>
          {t('dashboards.designer.typeLabel')}
        </label>
        <select
          id={`${id}-type`}
          className="field__input"
          value={visual.visualType}
          onChange={(event) => {
            change(withType(visual, event.target.value as VisualType));
          }}
        >
          {VISUAL_TYPES.map((type) => (
            <option key={type} value={type}>
              {t(`dashboards.designer.type.${type}`)}
            </option>
          ))}
        </select>
      </div>
      <fieldset className="field field--group" aria-describedby={`${id}-measures-hint`}>
        <legend className="field__label">{t('dashboards.designer.measuresLabel')}</legend>
        <p id={`${id}-measures-hint`} className="field__hint">
          {t(max === 1 ? 'dashboards.designer.measuresOne' : 'dashboards.designer.measuresHint', {
            count: max,
          })}
        </p>
        <div className="field__options">
          {measuresFor(visual.visualType).map((measure) => {
            const checked = visual.measures.includes(measure);
            return (
              <label key={measure} className="field__option">
                {max === 1 ? (
                  <input
                    type="radio"
                    name={`${id}-measure`}
                    checked={checked}
                    onChange={() => {
                      change({ ...visual, measures: [measure] });
                    }}
                  />
                ) : (
                  <input
                    type="checkbox"
                    checked={checked}
                    disabled={
                      (!checked && visual.measures.length >= max) ||
                      (checked && visual.measures.length === 1)
                    }
                    onChange={(event) => {
                      toggle(measure, event.target.checked);
                    }}
                  />
                )}
                {t(`dashboards.designer.measure.${measure}`)}
              </label>
            );
          })}
        </div>
      </fieldset>
      <div className="field">
        <label className="field__label" htmlFor={`${id}-group`}>
          {t('dashboards.designer.groupByLabel')}
        </label>
        <select
          id={`${id}-group`}
          className="field__input"
          value={visual.groupBy}
          disabled={groupings.length === 1}
          onChange={(event) => {
            change({ ...visual, groupBy: event.target.value as GroupBy });
          }}
        >
          {groupings.map((groupBy) => (
            <option key={groupBy} value={groupBy}>
              {t(`dashboards.designer.groupBy.${groupBy}`)}
            </option>
          ))}
        </select>
      </div>
      <div className="designer__size">
        <div className="field">
          <label className="field__label" htmlFor={`${id}-width`}>
            {t('dashboards.designer.width')}
          </label>
          <input
            id={`${id}-width`}
            className="field__input"
            type="number"
            min={1}
            max={COLUMNS}
            value={visual.layoutWidth}
            onChange={(event) => {
              const width = Number(event.target.value);
              if (Number.isInteger(width)) {
                onChange({ ...visual, layoutWidth: width });
              }
            }}
          />
        </div>
        <div className="field">
          <label className="field__label" htmlFor={`${id}-height`}>
            {t('dashboards.designer.height')}
          </label>
          <input
            id={`${id}-height`}
            className="field__input"
            type="number"
            min={1}
            max={MAX_HEIGHT}
            value={visual.layoutHeight}
            onChange={(event) => {
              const height = Number(event.target.value);
              if (Number.isInteger(height)) {
                onChange({ ...visual, layoutHeight: height });
              }
            }}
          />
        </div>
      </div>
      <div className="form__actions">
        <button type="button" className="button" onClick={onDuplicate}>
          {t('dashboards.designer.duplicate')}
        </button>
        <button type="button" className="button button--danger" onClick={onRemove}>
          {t('dashboards.designer.remove')}
        </button>
      </div>
    </div>
  );
}
