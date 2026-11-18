import { type ReactElement, useId, useState } from 'react';

import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import {
  type FieldAggregation,
  type FieldDescriptor,
  type FieldFilter,
  type GroupBy,
  type Measure,
  type Visual,
} from './designApi.ts';
import {
  aggregationsFor,
  dimensionKey,
  drawsFields,
  FILTER_DIMENSIONS,
  fieldName,
  filterFieldName,
  filterOptions,
  groupsByField,
  MAX_FILTER_VALUES,
  MAX_FILTERS,
  MAX_VALUES,
  NO_FIELD_DATA,
  sourceName,
  withFilter,
} from './fields.ts';
import { firstGrouping, GROUP_BYS, groupBysFor, MAX_MEASURES, measuresFor } from './visuals.ts';

/** The Data panel's first group: the eight Portfolio figures (`measures`), before the governed fields' sources. */
const FIGURES = 'FIGURES';

/**
 * The selected visual's data, as Power BI's field list offers it (TASK-112): the Portfolio figures and the governed fields by source, searched
 * and ticked; each chosen field's calculation; the grouping, by a project attribute or a status field; and the visual's own filters. A visual
 * draws figures or fields, never both: ticking one kind replaces the other, and the panel says so.
 */
export function DataPanel({
  visual,
  fields,
  onChange,
}: {
  visual: Visual;
  fields: ApiResource<FieldDescriptor[]>;
  onChange: (next: Visual) => void;
}): ReactElement {
  const { t, language } = useI18n();
  const id = useId();
  const [search, setSearch] = useState('');
  // Every group starts open, so each field is in view at once; a group is closed by its heading.
  const [closed, setClosed] = useState<ReadonlySet<string>>(() => new Set());
  const [replaced, setReplaced] = useState<'figures' | 'fields' | null>(null);
  const type = visual.visualType;
  const catalogue = fields.data ?? [];
  const onFields = drawsFields(visual);
  const values = visual.values ?? [];
  const max = onFields ? MAX_VALUES[type] : MAX_MEASURES[type];
  const chosen = onFields ? values.length : visual.measures.length;
  const descriptor = (key: string) => catalogue.find((field) => field.key === key);

  const offered = catalogue.filter((field) => aggregationsFor(field, type).length > 0);
  const query = search.trim().toLocaleLowerCase();
  const matches = (words: string) => query === '' || words.toLocaleLowerCase().includes(query);
  const figures = measuresFor(type).filter((measure) =>
    matches(t(`dashboards.designer.measure.${measure}`)),
  );
  const sources = [...new Set(offered.map((field) => field.projectionCode))].flatMap((code) => {
    const name = sourceName(code, language);
    const inSource = offered.filter(
      (field) =>
        field.projectionCode === code &&
        (matches(name) || matches(fieldName(field.key, language)) || matches(field.fieldCode)),
    );
    return inSource.length === 0 ? [] : [{ code, name, fields: inSource }];
  });

  const toggleGroup = (group: string) => {
    setClosed((before) => {
      const next = new Set(before);
      if (!next.delete(group)) {
        next.add(group);
      }
      return next;
    });
  };

  const pickMeasure = (measure: Measure, on: boolean) => {
    if (onFields) {
      setReplaced('fields');
      onChange({
        ...visual,
        ...NO_FIELD_DATA,
        measures: [measure],
        groupBy:
          visual.groupField === undefined || visual.groupField === null
            ? visual.groupBy
            : firstGrouping(type),
      });
      return;
    }
    setReplaced(null);
    const measures = !on
      ? visual.measures.filter((candidate) => candidate !== measure)
      : max === 1
        ? [measure]
        : [...visual.measures, measure];
    if (measures.length > 0) {
      onChange({ ...visual, measures });
    }
  };

  const pickField = (field: FieldDescriptor, on: boolean) => {
    const [aggregation] = aggregationsFor(field, type);
    if (aggregation === undefined) {
      return;
    }
    const value = { field: field.key, aggregation };
    if (!onFields) {
      setReplaced('figures');
      onChange({ ...visual, measures: [], values: [value] });
      return;
    }
    setReplaced(null);
    const next = !on
      ? values.filter((candidate) => candidate.field !== field.key)
      : max === 1
        ? [value]
        : [...values, value];
    if (next.length > 0) {
      onChange({ ...visual, values: next });
    }
  };

  const setAggregation = (at: number, aggregation: FieldAggregation) => {
    onChange({
      ...visual,
      values: values.map((value, index) => (index === at ? { ...value, aggregation } : value)),
    });
  };

  const sourceOpen = (group: string) => query !== '' || !closed.has(group);

  return (
    <>
      <fieldset className="field field--group designer-data" aria-describedby={`${id}-limit`}>
        <legend className="field__label">{t('dashboards.designer.dataLabel')}</legend>
        <p id={`${id}-limit`} className="field__hint">
          {t(max === 1 ? 'dashboards.designer.measuresOne' : 'dashboards.designer.measuresHint', {
            count: max,
          })}
        </p>
        <ul className="designer-data__chosen" aria-label={t('dashboards.designer.measuresLabel')}>
          {onFields
            ? values.map((value, index) => {
                const field = descriptor(value.field);
                const allowed =
                  field === undefined ? [value.aggregation] : aggregationsFor(field, type);
                const name = fieldName(value.field, language);
                return (
                  <li key={value.field} className="designer-data__value">
                    <span className="designer-data__value-name" dir="auto">
                      {name}
                    </span>
                    <select
                      className="field__input designer-data__aggregation"
                      aria-label={t('dashboards.designer.aggregationLabel', { field: name })}
                      value={value.aggregation}
                      disabled={allowed.length === 1}
                      onChange={(event) => {
                        setAggregation(index, event.target.value as FieldAggregation);
                      }}
                    >
                      {allowed.map((aggregation) => (
                        <option key={aggregation} value={aggregation}>
                          {t(`dashboards.designer.aggregation.${aggregation}`)}
                        </option>
                      ))}
                    </select>
                    <RemoveButton
                      label={t('dashboards.designer.removeValue', { field: name })}
                      disabled={values.length === 1}
                      onClick={() => {
                        onChange({
                          ...visual,
                          values: values.filter((_, at) => at !== index),
                        });
                      }}
                    />
                  </li>
                );
              })
            : visual.measures.map((measure) => {
                const name = t(`dashboards.designer.measure.${measure}`);
                return (
                  <li key={measure} className="designer-data__value">
                    <span className="designer-data__value-name">{name}</span>
                    <RemoveButton
                      label={t('dashboards.designer.removeValue', { field: name })}
                      disabled={visual.measures.length === 1}
                      onClick={() => {
                        pickMeasure(measure, false);
                      }}
                    />
                  </li>
                );
              })}
        </ul>
        {replaced !== null && (
          <p className="designer-data__note" role="status">
            {t(
              replaced === 'figures'
                ? 'dashboards.designer.replacedFigures'
                : 'dashboards.designer.replacedFields',
            )}
          </p>
        )}
        <input
          type="search"
          className="field__input designer-data__search"
          aria-label={t('dashboards.designer.searchFields')}
          placeholder={t('dashboards.designer.searchFields')}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
          }}
        />
        <div className="designer-data__list">
          {figures.length > 0 && (
            <FieldGroup
              name={t('dashboards.designer.portfolioFigures')}
              chosen={onFields ? 0 : visual.measures.length}
              open={sourceOpen(FIGURES)}
              onToggle={() => {
                toggleGroup(FIGURES);
              }}
            >
              {figures.map((measure) => {
                const checked = !onFields && visual.measures.includes(measure);
                return (
                  <FieldRow
                    key={measure}
                    name={t(`dashboards.designer.measure.${measure}`)}
                    hint={null}
                    checked={checked}
                    disabled={
                      checked ? visual.measures.length === 1 : !onFields && max > 1 && chosen >= max
                    }
                    onChange={(on) => {
                      pickMeasure(measure, on);
                    }}
                  />
                );
              })}
            </FieldGroup>
          )}
          {sources.map((source) => (
            <FieldGroup
              key={source.code}
              name={source.name}
              chosen={
                values.filter((value) => source.fields.some((f) => f.key === value.field)).length
              }
              open={sourceOpen(source.code)}
              onToggle={() => {
                toggleGroup(source.code);
              }}
            >
              {source.fields.map((field) => {
                const checked = values.some((value) => value.field === field.key);
                return (
                  <FieldRow
                    key={field.key}
                    name={fieldName(field.key, language)}
                    hint={t(`dashboards.designer.fieldType.${field.type}`)}
                    checked={checked}
                    disabled={checked ? values.length === 1 : onFields && max > 1 && chosen >= max}
                    onChange={(on) => {
                      pickField(field, on);
                    }}
                  />
                );
              })}
            </FieldGroup>
          ))}
          {query !== '' && figures.length === 0 && sources.length === 0 && (
            <p className="designer__hint">{t('dashboards.designer.noFieldMatch')}</p>
          )}
        </div>
        {fields.error !== undefined && fields.error !== null && (
          <p className="designer__hint">{t('dashboards.designer.fieldsUnavailable')}</p>
        )}
      </fieldset>
      <GroupBySelect visual={visual} catalogue={catalogue} onChange={onChange} />
      {onFields ? (
        <FiltersEditor visual={visual} catalogue={catalogue} onChange={onChange} />
      ) : (
        <div className="field">
          <span className="field__label">{t('dashboards.designer.filtersLabel')}</span>
          <p className="field__hint">{t('dashboards.designer.filtersFieldsOnly')}</p>
        </div>
      )}
    </>
  );
}

/** A source's fields under a heading that opens and closes them, with how many of them are chosen. */
function FieldGroup({
  name,
  chosen,
  open,
  onToggle,
  children,
}: {
  name: string;
  chosen: number;
  open: boolean;
  onToggle: () => void;
  children: ReactElement[];
}): ReactElement {
  const id = useId();
  return (
    <div className="designer-data__group">
      <button
        type="button"
        className="designer-data__group-head"
        aria-expanded={open}
        aria-controls={id}
        onClick={onToggle}
      >
        <span className="designer-data__chevron" aria-hidden="true" />
        <span className="designer-data__group-name">{name}</span>
        {chosen > 0 && <span className="designer-data__count">{chosen}</span>}
      </button>
      {open && (
        <ul id={id} className="designer-data__fields">
          {children}
        </ul>
      )}
    </div>
  );
}

function FieldRow({
  name,
  hint,
  checked,
  disabled,
  onChange,
}: {
  name: string;
  hint: string | null;
  checked: boolean;
  disabled: boolean;
  onChange: (on: boolean) => void;
}): ReactElement {
  return (
    <li>
      <label className="designer-data__field">
        <input
          type="checkbox"
          checked={checked}
          disabled={disabled}
          onChange={(event) => {
            onChange(event.target.checked);
          }}
        />
        <span className="designer-data__field-name" dir="auto">
          {name}
        </span>
        {hint !== null && <span className="designer-data__type">{hint}</span>}
      </label>
    </li>
  );
}

function RemoveButton({
  label,
  disabled,
  onClick,
}: {
  label: string;
  disabled: boolean;
  onClick: () => void;
}): ReactElement {
  return (
    <button
      type="button"
      className="designer-data__remove"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
    >
      <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
        <path d="M6 6l12 12M18 6L6 18" />
      </svg>
    </button>
  );
}

/** Group by: the project attributes the type takes, then, for a visual drawing fields, the status (CODE) fields. */
function GroupBySelect({
  visual,
  catalogue,
  onChange,
}: {
  visual: Visual;
  catalogue: readonly FieldDescriptor[];
  onChange: (next: Visual) => void;
}): ReactElement {
  const { t, language } = useI18n();
  const id = useId();
  const groupings = groupBysFor(visual.visualType);
  const statusFields =
    drawsFields(visual) && groupsByField(visual.visualType)
      ? catalogue.filter((field) => field.type === 'CODE')
      : [];
  const attributes = groupings.map((groupBy) => (
    <option key={groupBy} value={groupBy}>
      {t(`dashboards.designer.groupBy.${groupBy}`)}
    </option>
  ));
  return (
    <div className="field">
      <label className="field__label" htmlFor={`${id}-group`}>
        {t('dashboards.designer.groupByLabel')}
      </label>
      <select
        id={`${id}-group`}
        className="field__input"
        value={visual.groupField ?? visual.groupBy}
        disabled={groupings.length === 1 && statusFields.length === 0}
        onChange={(event) => {
          const chosen = event.target.value;
          onChange(
            (GROUP_BYS as readonly string[]).includes(chosen)
              ? { ...visual, groupBy: chosen as GroupBy, groupField: null }
              : { ...visual, groupBy: 'NONE', groupField: chosen },
          );
        }}
      >
        {statusFields.length === 0 ? (
          attributes
        ) : (
          <>
            <optgroup label={t('dashboards.designer.projectAttributes')}>{attributes}</optgroup>
            <optgroup label={t('dashboards.designer.statusFields')}>
              {statusFields.map((field) => (
                <option key={field.key} value={field.key}>
                  {fieldName(field.key, language)}
                </option>
              ))}
            </optgroup>
          </>
        )}
      </select>
    </div>
  );
}

/**
 * The visual's own filters: each a status field or a project attribute and the values a project must have one of. A filter being added is
 * kept here until its first value is ticked, so the design never holds a filter with none.
 */
function FiltersEditor({
  visual,
  catalogue,
  onChange,
}: {
  visual: Visual;
  catalogue: readonly FieldDescriptor[];
  onChange: (next: Visual) => void;
}): ReactElement {
  const { t, language } = useI18n();
  const lookups = useProjectLookups();
  const id = useId();
  // The filter being added: null while none is, '' while its field is being picked, then the field until its first value is ticked.
  const [adding, setAdding] = useState<string | null>(null);
  const filters = visual.filters ?? [];
  const used = new Set(filters.map((filter) => filter.field));
  const codeFields = catalogue.filter((field) => field.type === 'CODE' && !used.has(field.key));
  const dimensions = FILTER_DIMENSIONS.filter((dimension) => !used.has(dimensionKey(dimension)));
  const change = (next: FieldFilter[]) => {
    onChange({ ...visual, filters: next.length === 0 ? null : next });
  };

  return (
    <fieldset className="field field--group designer-data" aria-describedby={`${id}-hint`}>
      <legend className="field__label">{t('dashboards.designer.filtersLabel')}</legend>
      <p id={`${id}-hint`} className="field__hint">
        {t('dashboards.designer.filtersHint', { count: MAX_FILTERS })}
      </p>
      {filters.map((filter, at) => (
        <FilterValues
          key={filter.field}
          field={filter.field}
          chosen={filter.values}
          options={filterOptions(filter.field, catalogue, lookups, language, t)}
          onChange={(chosen) => {
            change(
              withFilter(filters, at, chosen.length === 0 ? null : { ...filter, values: chosen }),
            );
          }}
          onRemove={() => {
            change(withFilter(filters, at, null));
          }}
        />
      ))}
      {adding !== null && adding !== '' && (
        <FilterValues
          field={adding}
          chosen={[]}
          options={filterOptions(adding, catalogue, lookups, language, t)}
          onChange={(chosen) => {
            setAdding(null);
            change([...filters, { field: adding, values: chosen }]);
          }}
          onRemove={() => {
            setAdding(null);
          }}
        />
      )}
      {adding === '' && (
        <div className="designer-data__filter-pick">
          <select
            className="field__input"
            aria-label={t('dashboards.designer.filterField')}
            value=""
            onChange={(event) => {
              setAdding(event.target.value);
            }}
          >
            <option value="">{t('dashboards.designer.chooseFilterField')}</option>
            <optgroup label={t('dashboards.designer.projectAttributes')}>
              {dimensions.map((dimension) => (
                <option key={dimension} value={dimensionKey(dimension)}>
                  {t(`dashboards.designer.groupBy.${dimension}`)}
                </option>
              ))}
            </optgroup>
            {codeFields.length > 0 && (
              <optgroup label={t('dashboards.designer.statusFields')}>
                {codeFields.map((field) => (
                  <option key={field.key} value={field.key}>
                    {fieldName(field.key, language)}
                  </option>
                ))}
              </optgroup>
            )}
          </select>
          <RemoveButton
            label={t('dashboards.designer.cancelFilter')}
            disabled={false}
            onClick={() => {
              setAdding(null);
            }}
          />
        </div>
      )}
      {adding === null && filters.length < MAX_FILTERS && (
        <button
          type="button"
          className="button designer-data__add"
          onClick={() => {
            setAdding('');
          }}
        >
          {t('dashboards.designer.addFilter')}
        </button>
      )}
    </fieldset>
  );
}

/** One filter: its subject, the values to tick (up to twenty), and its removal; unticking the last value removes it too. */
function FilterValues({
  field,
  chosen,
  options,
  onChange,
  onRemove,
}: {
  field: string;
  chosen: readonly string[];
  options: { value: string; label: string }[];
  onChange: (chosen: string[]) => void;
  onRemove: () => void;
}): ReactElement {
  const { t, language } = useI18n();
  const name = filterFieldName(field, language);
  const full = chosen.length >= MAX_FILTER_VALUES;
  return (
    <div className="designer-data__filter">
      <div className="designer-data__filter-head">
        <span className="designer-data__value-name" dir="auto">
          {name}
        </span>
        <RemoveButton
          label={t('dashboards.designer.removeFilter', { field: name })}
          disabled={false}
          onClick={onRemove}
        />
      </div>
      {options.length === 0 ? (
        <p className="designer__hint">{t('dashboards.designer.filterNoValues')}</p>
      ) : (
        <ul className="designer-data__fields designer-data__filter-values" aria-label={name}>
          {options.map((option) => {
            const checked = chosen.includes(option.value);
            return (
              <FieldRow
                key={option.value}
                name={option.label}
                hint={null}
                checked={checked}
                disabled={!checked && full}
                onChange={(on) => {
                  onChange(
                    on
                      ? [...chosen, option.value]
                      : chosen.filter((value) => value !== option.value),
                  );
                }}
              />
            );
          })}
        </ul>
      )}
    </div>
  );
}
