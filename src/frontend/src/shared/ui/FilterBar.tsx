import { type ReactElement, useId, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

/** A value a filter offers, with how many records have it when the source counts them. */
export interface FilterBarOption {
  value: string;
  label: string;
  count?: number | undefined;
}

export interface FilterBarField {
  /** The filter's name, as the URL and the API name it. */
  name: string;
  label: string;
  /** What the control reads with no value chosen, e.g. "All sectors". */
  all: string;
  options: FilterBarOption[];
  /** Under "More filters" rather than in the bar's first row. */
  more?: boolean;
}

interface FilterBarProps {
  fields: FilterBarField[];
  values: Partial<Record<string, string | undefined>>;
  onChange: (name: string, value: string | undefined) => void;
  onClearAll: () => void;
}

/**
 * The one filter bar of the dashboards and the project register (the 2 Nov 2026 review, R3-1): a row of compact drop-downs under the page title,
 * each with its label inside it ("Sector: All"), the rest under "More filters" with a count of those set, and the active filters as chips with ×
 * and Clear all. A filter applies as it is chosen; there is no Apply button. A filter with nothing to offer is not shown, unless it is set. On a
 * phone the bar folds into a "Filters (n)" button that opens it.
 */
export function FilterBar({
  fields,
  values,
  onChange,
  onClearAll,
}: FilterBarProps): ReactElement | null {
  const { t } = useI18n();
  const moreId = useId();
  const barId = useId();
  const shown = fields.filter(
    (field) => field.options.length > 0 || values[field.name] !== undefined,
  );
  const primary = shown.filter((field) => field.more !== true);
  const secondary = shown.filter((field) => field.more === true);
  const active = shown.filter((field) => values[field.name] !== undefined);
  const activeSecondary = secondary.filter((field) => values[field.name] !== undefined).length;
  const [moreOpen, setMoreOpen] = useState(activeSecondary > 0);
  const [phoneOpen, setPhoneOpen] = useState(false);
  if (shown.length === 0) {
    return null;
  }

  const labelOf = (field: FilterBarField, value: string) =>
    field.options.find((option) => option.value === value)?.label ?? value;

  const control = (field: FilterBarField) => {
    const value = values[field.name] ?? '';
    return (
      <select
        key={field.name}
        className="filter-bar__select"
        name={field.name}
        aria-label={field.label}
        value={value}
        onChange={(event) => {
          onChange(field.name, event.target.value === '' ? undefined : event.target.value);
        }}
      >
        <option value="">
          {t('common.filters.inline', { label: field.label, value: field.all })}
        </option>
        {value !== '' && !field.options.some((option) => option.value === value) && (
          <option value={value}>{t('common.filters.inline', { label: field.label, value })}</option>
        )}
        {field.options.map((option) => (
          <option key={option.value} value={option.value}>
            {t('common.filters.inline', {
              label: field.label,
              value:
                option.count === undefined
                  ? option.label
                  : t('common.filters.optionCount', { label: option.label, count: option.count }),
            })}
          </option>
        ))}
      </select>
    );
  };

  return (
    <div className={`filter-bar${phoneOpen ? ' filter-bar--open' : ''}`}>
      <button
        type="button"
        className="button filter-bar__toggle"
        aria-expanded={phoneOpen}
        aria-controls={barId}
        onClick={() => {
          setPhoneOpen((open) => !open);
        }}
      >
        {t('common.filters.toggle', { count: active.length })}
      </button>
      <div
        id={barId}
        className="filter-bar__body"
        role="search"
        aria-label={t('common.filters.label')}
      >
        <div className="filter-bar__row">
          {primary.map(control)}
          {secondary.length > 0 && (
            <button
              type="button"
              className="button button--quiet filter-bar__more"
              aria-expanded={moreOpen}
              aria-controls={moreId}
              onClick={() => {
                setMoreOpen((open) => !open);
              }}
            >
              {activeSecondary > 0
                ? t('common.filters.moreCount', { count: activeSecondary })
                : t('common.filters.more')}
            </button>
          )}
        </div>
        {secondary.length > 0 && (
          <div id={moreId} className="filter-bar__row" hidden={!moreOpen}>
            {secondary.map(control)}
          </div>
        )}
        {active.length > 0 && (
          <ul className="filter-bar__chips" aria-label={t('common.filters.active')}>
            {active.map((field) => {
              const value = values[field.name] ?? '';
              const text = t('common.filters.inline', {
                label: field.label,
                value: labelOf(field, value),
              });
              return (
                <li key={field.name}>
                  <button
                    type="button"
                    className="chip"
                    aria-label={t('common.filters.remove', { filter: text })}
                    onClick={() => {
                      onChange(field.name, undefined);
                    }}
                  >
                    {text} <span aria-hidden="true">×</span>
                  </button>
                </li>
              );
            })}
            <li>
              <button type="button" className="button button--quiet" onClick={onClearAll}>
                {t('common.filters.clearAll')}
              </button>
            </li>
          </ul>
        )}
      </div>
    </div>
  );
}
