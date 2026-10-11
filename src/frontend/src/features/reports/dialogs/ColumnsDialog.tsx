import { type ReactElement, useId, useState } from 'react';

import { labelOfValue, SEMANTIC_STATES } from '@/features/dashboards/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, SelectField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { type ReportFilterOperator, type ReportSortDirection } from '../api/types.ts';
import {
  type Composition,
  type FieldOption,
  type FilterDraft,
  isFilterComplete,
  valueParts,
} from '../composition.ts';
import { OPERATOR_LABELS } from '../presentation.ts';

// MOD-061 Select Columns / Filters (FG-02 §13.6): "Unavailable/sensitive fields never appear as choices". Every choice
// here is a FieldOption the API offered — a column the report publishes, or an entry of the PUBLISHED allowlist — and
// a filter's operator and values are that entry's own. There is no box that takes a field name, a join or a query.

interface ColumnsFormProps {
  options: FieldOption[];
  initial: Composition;
  /** SCR-138 filters on allowlisted fields; a published report narrows through its parameters instead. */
  allowFilters: boolean;
  onApply: (composition: Composition) => void;
  onClose: () => void;
}

function FilterValue({
  option,
  filter,
  onChange,
}: {
  option: FieldOption;
  filter: FilterDraft;
  onChange: (value: string) => void;
}): ReactElement {
  const { t, language } = useI18n();
  const id = useId();
  const parts = valueParts(filter.value);
  const wordOf = (value: string) =>
    labelOfValue(option.projectionCode ?? '', value, null, language, t).text;

  if (option.values.length > 0 && filter.operator === 'IN') {
    return (
      <fieldset className="field field--group">
        <legend className="field__label">{t('reports.columns.values')}</legend>
        <div className="field__options">
          {option.values.map((value) => (
            <label key={value} className="field__option">
              <input
                type="checkbox"
                checked={parts.includes(value)}
                onChange={(event) => {
                  const next = event.target.checked
                    ? [...parts, value]
                    : parts.filter((part) => part !== value);
                  onChange(option.values.filter((candidate) => next.includes(candidate)).join(','));
                }}
              />
              {wordOf(value)}
            </label>
          ))}
        </div>
      </fieldset>
    );
  }
  if (option.values.length > 0) {
    return (
      <SelectField
        label={t('reports.columns.value')}
        name={`${id}-value`}
        value={filter.value}
        onChange={onChange}
        placeholder={t('common.form.choose')}
        options={option.values.map((value) => ({ value, label: wordOf(value) }))}
      />
    );
  }
  if (option.valueType === 'BOOLEAN') {
    return (
      <SelectField
        label={t('reports.columns.value')}
        name={`${id}-value`}
        value={filter.value}
        onChange={onChange}
        placeholder={t('common.form.choose')}
        options={[
          { value: 'true', label: t('common.values.yes') },
          { value: 'false', label: t('common.values.no') },
        ]}
      />
    );
  }
  const type = option.valueType === 'DATE' ? 'date' : 'text';
  const numeric = ['COUNT', 'PERCENT', 'DAYS', 'SAR'].includes(option.valueType);
  const input = (index: number, inputLabel: string) => (
    <div className="field" key={index}>
      <label className="field__label" htmlFor={`${id}-${String(index)}`}>
        {inputLabel}
      </label>
      <input
        id={`${id}-${String(index)}`}
        className="field__input"
        type={type}
        dir={numeric || type === 'date' ? 'ltr' : 'auto'}
        inputMode={numeric ? 'decimal' : undefined}
        value={parts[index] ?? ''}
        onChange={(event) => {
          const next = [...parts];
          next[index] = event.target.value.replaceAll(',', '');
          onChange(
            filter.operator === 'BETWEEN' ? `${next[0] ?? ''},${next[1] ?? ''}` : (next[0] ?? ''),
          );
        }}
      />
    </div>
  );
  return filter.operator === 'BETWEEN' ? (
    <>
      {input(0, t('reports.columns.from'))}
      {input(1, t('reports.columns.to'))}
    </>
  ) : (
    input(0, t('reports.columns.value'))
  );
}

function ColumnsForm({
  options,
  initial,
  allowFilters,
  onApply,
  onClose,
}: ColumnsFormProps): ReactElement {
  const { t, language } = useI18n();
  const byKey = new Map(options.map((option) => [option.key, option]));
  const [columns, setColumns] = useState(initial.columns.filter((key) => byKey.has(key)));
  const [sortKey, setSortKey] = useState(
    initial.sort !== null && byKey.get(initial.sort.key)?.isSortable === true
      ? initial.sort.key
      : '',
  );
  const [direction, setDirection] = useState<ReportSortDirection>(initial.sort?.direction ?? 'ASC');
  const [filters, setFilters] = useState<FilterDraft[]>(
    initial.filters.filter((filter) => byKey.get(filter.key)?.isFilterable === true),
  );
  const [problem, setProblem] = useState<string | null>(null);

  const nameOf = (option: FieldOption) => option.label[language];
  const filterable = options.filter((option) => option.isFilterable);
  const unchosen = options.filter((option) => !columns.includes(option.key));

  const move = (index: number, by: number) => {
    setColumns((current) => {
      const next = [...current];
      const [moved] = next.splice(index, 1);
      if (moved !== undefined) {
        next.splice(index + by, 0, moved);
      }
      return next;
    });
  };

  const updateFilter = (index: number, change: Partial<FilterDraft>) => {
    setFilters((current) =>
      current.map((filter, position) => (position === index ? { ...filter, ...change } : filter)),
    );
  };

  const fieldTag = (option: FieldOption) =>
    option.semanticState === null ? null : (
      <span className="report-result__semantic">
        {t(SEMANTIC_STATES[option.semanticState].label)}
      </span>
    );

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        if (columns.length === 0) {
          setProblem(t('reports.columns.noColumn'));
          return;
        }
        if (filters.some((filter) => !isFilterComplete(filter))) {
          setProblem(t('reports.columns.incompleteFilter'));
          return;
        }
        onApply({
          columns,
          sort: sortKey === '' ? null : { key: sortKey, direction },
          filters,
        });
      }}
    >
      <p className="form__note">
        {allowFilters ? t('reports.columns.introExplorer') : t('reports.columns.introReport')}
      </p>
      <FormAlert message={problem} />

      <fieldset className="form__section">
        <legend>{t('reports.columns.shown')}</legend>
        {columns.length === 0 ? (
          <p className="form__note">{t('reports.columns.noneShown')}</p>
        ) : (
          <ol className="personalize__list">
            {columns.map((key, index) => {
              const option = byKey.get(key);
              return option === undefined ? null : (
                <li key={key} className="personalize__item">
                  <label className="field__option">
                    <input
                      type="checkbox"
                      checked
                      onChange={() => {
                        setColumns((current) => current.filter((candidate) => candidate !== key));
                        if (sortKey === key) {
                          setSortKey('');
                        }
                      }}
                    />
                    {nameOf(option)}
                    {fieldTag(option)}
                  </label>
                  <span className="personalize__moves">
                    <button
                      type="button"
                      className="button button--quiet"
                      disabled={index === 0}
                      onClick={() => {
                        move(index, -1);
                      }}
                    >
                      {t('reports.columns.moveUp')}{' '}
                      <span className="visually-hidden">{nameOf(option)}</span>
                    </button>
                    <button
                      type="button"
                      className="button button--quiet"
                      disabled={index === columns.length - 1}
                      onClick={() => {
                        move(index, 1);
                      }}
                    >
                      {t('reports.columns.moveDown')}{' '}
                      <span className="visually-hidden">{nameOf(option)}</span>
                    </button>
                  </span>
                </li>
              );
            })}
          </ol>
        )}
      </fieldset>

      {unchosen.length > 0 && (
        <fieldset className="form__section" data-field-picker>
          <legend>{t('reports.columns.available')}</legend>
          <ul className="choice-list">
            {unchosen.map((option) => (
              <li key={option.key} className="choice-list__item">
                <label className="field__option">
                  <input
                    type="checkbox"
                    checked={false}
                    onChange={() => {
                      setColumns((current) => [...current, option.key]);
                    }}
                  />
                  {nameOf(option)}
                  {fieldTag(option)}
                </label>
              </li>
            ))}
          </ul>
        </fieldset>
      )}

      <fieldset className="form__section">
        <legend>{t('reports.columns.sort')}</legend>
        <SelectField
          label={t('reports.columns.sortBy')}
          name="sortBy"
          value={sortKey}
          onChange={setSortKey}
          placeholder={t('reports.columns.noSort')}
          options={options
            .filter((option) => option.isSortable)
            .map((option) => ({ value: option.key, label: nameOf(option) }))}
        />
        {sortKey !== '' && (
          <RadioGroupField
            label={t('reports.columns.direction')}
            name="direction"
            value={direction}
            onChange={(value) => {
              setDirection(value === 'DESC' ? 'DESC' : 'ASC');
            }}
            options={[
              { value: 'ASC', label: t('reports.columns.ascending') },
              { value: 'DESC', label: t('reports.columns.descending') },
            ]}
          />
        )}
      </fieldset>

      {allowFilters && (
        <fieldset className="form__section">
          <legend>{t('reports.columns.filters')}</legend>
          {filterable.length === 0 && (
            <p className="form__note">{t('reports.columns.noFilterable')}</p>
          )}
          {filters.map((filter, index) => {
            const option = byKey.get(filter.key);
            return option === undefined ? null : (
              <div key={index} className="report-filter" data-filter-row>
                <SelectField
                  label={t('reports.columns.filterField')}
                  name={`filter-${String(index)}-field`}
                  value={filter.key}
                  onChange={(key) => {
                    const next = byKey.get(key);
                    if (next !== undefined) {
                      updateFilter(index, {
                        key,
                        operator: next.operators[0] ?? 'EQ',
                        value: '',
                      });
                    }
                  }}
                  options={filterable.map((candidate) => ({
                    value: candidate.key,
                    label: nameOf(candidate),
                  }))}
                />
                <SelectField
                  label={t('reports.columns.operator')}
                  name={`filter-${String(index)}-operator`}
                  value={filter.operator}
                  onChange={(operator) => {
                    updateFilter(index, { operator: operator as ReportFilterOperator, value: '' });
                  }}
                  options={option.operators.map((operator) => ({
                    value: operator,
                    label: t(OPERATOR_LABELS[operator]),
                  }))}
                />
                <FilterValue
                  option={option}
                  filter={filter}
                  onChange={(value) => {
                    updateFilter(index, { value });
                  }}
                />
                <button
                  type="button"
                  className="button button--quiet"
                  onClick={() => {
                    setFilters((current) => current.filter((_, position) => position !== index));
                  }}
                >
                  {t('reports.columns.removeFilter')}{' '}
                  <span className="visually-hidden">{nameOf(option)}</span>
                </button>
              </div>
            );
          })}
          {filterable.length > 0 && (
            <button
              type="button"
              className="button"
              onClick={() => {
                const first = filterable[0];
                if (first !== undefined) {
                  setFilters((current) => [
                    ...current,
                    { key: first.key, operator: first.operators[0] ?? 'EQ', value: '' },
                  ]);
                }
              }}
            >
              {t('reports.columns.addFilter')}
            </button>
          )}
        </fieldset>
      )}

      <div className="form__actions">
        <button type="submit" className="button button--primary">
          {t('reports.columns.apply')}
        </button>
        <button type="button" className="button button--quiet" onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}

export function ColumnsDialog({
  open,
  onClose,
  ...form
}: ColumnsFormProps & { open: boolean }): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={open} title={t('reports.columns.title')} onClose={onClose}>
      <ColumnsForm {...form} onClose={onClose} />
    </Dialog>
  );
}
