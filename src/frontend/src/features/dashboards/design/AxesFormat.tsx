import { type ReactElement, useId } from 'react';

import { type BilingualLabel } from '@/features/identity-access/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import {
  AXIS_TITLE_MAX,
  axesOf,
  axisLabels,
  type RangeProblem,
  rangeProblem,
  titleIsHalf,
} from './axes.ts';
import { type Visual, type VisualAxes } from './designApi.ts';

const RANGE_PROBLEM = {
  order: 'dashboards.designer.axisRangeOrder',
  limit: 'dashboards.designer.axisRangeLimit',
} as const satisfies Record<RangeProblem, string>;

/**
 * The Format part of a bar, column or line chart (docs/architecture/dashboard-designer-axes.md): each axis shown or hidden and titled in both
 * languages; the value axis's start, end and gridlines. What is typed is kept as it is; a title in one language only, or a start not below the
 * end, is marked here and neither is sent nor drawn until it is put right (axes.ts).
 */
export function AxesFormat({
  visual,
  onChange,
}: {
  visual: Visual;
  onChange: (next: Visual) => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const axes = axesOf(visual);
  const labels = axisLabels(visual.visualType);
  const change = (next: Partial<VisualAxes>) => {
    onChange({ ...visual, axes: { ...axes, ...next } });
  };
  const { category, value } = axes;
  const problem = rangeProblem(value.min, value.max);
  const setEnd = (end: 'min' | 'max', text: string) => {
    const number = text.trim() === '' ? null : Number(text);
    if (number === null || Number.isFinite(number)) {
      change({ value: { ...value, [end]: number } });
    }
  };

  return (
    <section className="designer-format" aria-labelledby={`${id}-format`}>
      <h3 id={`${id}-format`} className="designer__subheading">
        {t('dashboards.designer.format')}
      </h3>
      <fieldset className="field field--group designer-well">
        <legend className="field__label designer-well__label">{t(labels.category)}</legend>
        <Toggle
          label={t('dashboards.designer.showAxis')}
          checked={category.show}
          onChange={(show) => {
            change({ category: { ...category, show } });
          }}
        />
        <AxisTitleFields
          title={category.title}
          onChange={(title) => {
            change({ category: { ...category, title } });
          }}
        />
      </fieldset>
      <fieldset className="field field--group designer-well">
        <legend className="field__label designer-well__label">{t(labels.value)}</legend>
        <Toggle
          label={t('dashboards.designer.showAxis')}
          checked={value.show}
          onChange={(show) => {
            change({ value: { ...value, show } });
          }}
        />
        <AxisTitleFields
          title={value.title}
          onChange={(title) => {
            change({ value: { ...value, title } });
          }}
        />
        <div className="designer__size">
          {(['min', 'max'] as const).map((end) => (
            <div key={end} className="field">
              <label className="field__label" htmlFor={`${id}-${end}`}>
                {t(end === 'min' ? 'dashboards.designer.axisStart' : 'dashboards.designer.axisEnd')}
              </label>
              <input
                id={`${id}-${end}`}
                className="field__input"
                type="number"
                inputMode="decimal"
                dir="ltr"
                aria-invalid={problem === null ? undefined : true}
                aria-describedby={problem === null ? `${id}-range-hint` : `${id}-range-error`}
                value={value[end] ?? ''}
                onChange={(event) => {
                  setEnd(end, event.target.value);
                }}
              />
            </div>
          ))}
        </div>
        {problem === null ? (
          <p id={`${id}-range-hint`} className="field__hint">
            {t('dashboards.designer.axisRangeHint')}
          </p>
        ) : (
          <p id={`${id}-range-error`} className="field__error" role="alert">
            {t(RANGE_PROBLEM[problem])}
          </p>
        )}
        <Toggle
          label={t('dashboards.designer.gridlines')}
          checked={value.gridlines}
          onChange={(gridlines) => {
            change({ value: { ...value, gridlines } });
          }}
        />
      </fieldset>
    </section>
  );
}

function Toggle({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked: boolean;
  onChange: (on: boolean) => void;
}): ReactElement {
  return (
    <label className="field__checkbox">
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => {
          onChange(event.target.checked);
        }}
      />
      {label}
    </label>
  );
}

/** An axis's title in Arabic and English; both empty is none. */
function AxisTitleFields({
  title,
  onChange,
}: {
  title: BilingualLabel | null;
  onChange: (title: BilingualLabel | null) => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const half = titleIsHalf(title);
  const set = (language: 'ar' | 'en', text: string) => {
    const next = { ar: title?.ar ?? '', en: title?.en ?? '', [language]: text };
    onChange(next.ar === '' && next.en === '' ? null : next);
  };
  const described = half ? `${id}-error` : `${id}-hint`;
  return (
    <>
      {(['ar', 'en'] as const).map((language) => (
        <div key={language} className="field">
          <label className="field__label" htmlFor={`${id}-${language}`}>
            {t(
              language === 'ar'
                ? 'dashboards.designer.axisTitleAr'
                : 'dashboards.designer.axisTitleEn',
            )}
          </label>
          <input
            id={`${id}-${language}`}
            className="field__input"
            dir={language === 'ar' ? 'rtl' : 'ltr'}
            lang={language}
            maxLength={AXIS_TITLE_MAX}
            aria-invalid={half && (title?.[language].trim() ?? '') === '' ? true : undefined}
            aria-describedby={described}
            value={title?.[language] ?? ''}
            onChange={(event) => {
              set(language, event.target.value);
            }}
          />
        </div>
      ))}
      {half ? (
        <p id={`${id}-error`} className="field__error" role="alert">
          {t('dashboards.designer.axisTitleBoth')}
        </p>
      ) : (
        <p id={`${id}-hint`} className="field__hint">
          {t('dashboards.designer.axisTitleHint')}
        </p>
      )}
    </>
  );
}
