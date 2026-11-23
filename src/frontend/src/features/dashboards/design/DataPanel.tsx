import { type ReactElement, useId, useState } from 'react';

import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import {
  type DateUnit,
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
  fieldName,
  filterDimensionsFor,
  filterFieldName,
  filterFieldsFor,
  filterOptions,
  grainOf,
  groupedBy,
  groupFieldsFor,
  groupsByFieldOnly,
  isDateType,
  MAX_FILTER_VALUES,
  MAX_FILTERS,
  MAX_VALUES,
  NO_FIELD_DATA,
  sourceName,
  withFilter,
} from './fields.ts';
import { axisLabels, takesAxes } from './axes.ts';
import { ColumnsWell } from './ColumnsWell.tsx';
import { FieldGroup, FieldRow, RemoveButton } from './FieldList.tsx';
import { groupBysOf, sourceOf, takesSource } from './recordSources.ts';
import { useDesignRiskMatrix } from './riskMatrixContext.ts';
import { SourceSelect } from './SourceSelect.tsx';
import {
  firstGrouping,
  GROUP_BYS,
  MAX_MEASURES,
  measureHint,
  measuresFor,
  withValues,
} from './visuals.ts';

/** What the one-line note says was replaced: the figures, the fields, or the values of the other grain (now, or over time). */
type Replaced = 'figures' | 'fields' | 'byHistory' | 'byCurrent';

const REPLACED_NOTE = {
  figures: 'dashboards.designer.replacedFigures',
  fields: 'dashboards.designer.replacedFields',
  byHistory: 'dashboards.designer.replacedByHistory',
  byCurrent: 'dashboards.designer.replacedByCurrent',
} as const;

/** The Data panel's first group: the Portfolio figures (`measures`), before the governed fields' sources. */
const FIGURES = 'FIGURES';

/** The Data panel's Project details on Risks: the project attributes a risk is grouped by. */
const DETAILS = 'DETAILS';

/**
 * The selected visual's data, as Power BI's field list offers it (TASK-112): the Portfolio figures and the governed fields by source, searched
 * and ticked; each chosen field's calculation; the grouping, by a project attribute or a status field; and the visual's own filters. A visual
 * draws figures or fields, never both: ticking one kind replaces the other, and the panel says so. A table has its Columns instead (ColumnsWell).
 */
export function DataPanel({
  visual,
  fields,
  projectFields,
  onSourceOpen,
  onChange,
}: {
  visual: Visual;
  /** The catalogue of the visual's source: the projects' fields, or a record source's. */
  fields: ApiResource<FieldDescriptor[]>;
  /** The projects' catalogue, which a visual switched back to Projects starts from. */
  projectFields: readonly FieldDescriptor[];
  /** The Source select was opened: the risk fields are asked for. */
  onSourceOpen: () => void;
  onChange: (next: Visual) => void;
}): ReactElement {
  const { t, language } = useI18n();
  const source = sourceOf(visual);
  const sourceSelect = takesSource(visual.visualType) ? (
    <SourceSelect
      visual={visual}
      projectFields={projectFields}
      onOpen={onSourceOpen}
      onChange={onChange}
    />
  ) : null;
  const id = useId();
  const [search, setSearch] = useState('');
  // Every group starts open, so each field is in view at once; a group is closed by its heading.
  const [closed, setClosed] = useState<ReadonlySet<string>>(() => new Set());
  const [replaced, setReplaced] = useState<Replaced | null>(null);
  const type = visual.visualType;
  const catalogue = fields.data ?? [];
  const onFields = drawsFields(visual);
  const values = visual.values ?? [];
  const max = onFields ? MAX_VALUES[type] : MAX_MEASURES[type];
  const chosen = onFields ? values.length : visual.measures.length;
  const descriptor = (key: string) => catalogue.find((field) => field.key === key);

  if (type === 'TABLE') {
    // A table's values box is its Columns (Power BI's): what groups the rows and what is calculated in each; its filters as any field visual's.
    return (
      <>
        {sourceSelect}
        <ColumnsWell visual={visual} fields={fields} onChange={onChange} />
        <FiltersEditor visual={visual} catalogue={catalogue} onChange={onChange} />
      </>
    );
  }

  const offered = catalogue.filter((field) => aggregationsFor(field, type).length > 0);
  const query = search.trim().toLocaleLowerCase();
  const matches = (words: string) => query === '' || words.toLocaleLowerCase().includes(query);
  // A visual on Risks counts risks: the Portfolio figures, which count projects, are not offered.
  const figures = (source === 'PROJECTS' ? measuresFor(type) : []).filter((measure) =>
    matches(t(`dashboards.designer.measure.${measure}`)),
  );
  const sources = [...new Set(offered.map((field) => field.projectionCode))].flatMap((code) => {
    const name = sourceName(code, language);
    const inSource = offered.filter(
      (field) =>
        field.projectionCode === code &&
        (matches(name) || matches(fieldName(field.key, language)) || matches(field.fieldCode)),
    );
    const overTime = inSource.some((field) => field.grain === 'SNAPSHOT');
    return inSource.length === 0 ? [] : [{ code, name, overTime, fields: inSource }];
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
        // A map's filters apply to its figures too, so they stay.
        ...(type === 'MAP' ? { filters: visual.filters ?? null } : {}),
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
    // A first field, or one of the other grain, starts the values over: the grouping and filters follow the new grain.
    const grain = grainOf(visual, catalogue);
    if (!onFields || field.grain !== grain) {
      const next = withValues(visual, type, [value], catalogue);
      if (next !== null) {
        setReplaced(!onFields ? 'figures' : field.grain === 'SNAPSHOT' ? 'byHistory' : 'byCurrent');
        onChange(next);
      }
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
  // On Risks, the risks' project attributes are offered as Project details: ticking one groups the visual by it, through each risk's project.
  const details =
    source === 'PROJECTS' || groupsByFieldOnly(type, grainOf(visual, catalogue))
      ? []
      : groupBysOf(type, source).filter(
          (groupBy) => groupBy !== 'NONE' && matches(t(`dashboards.designer.groupBy.${groupBy}`)),
        );
  const groupedByDetail = (groupBy: GroupBy) =>
    (visual.groupField ?? null) === null && visual.groupBy === groupBy;
  // A bar, column or line chart's data is two boxes, as Power BI's wells: the category axis (the grouping), then the value axis (the values).
  const wells = takesAxes(type) ? axisLabels(type) : null;
  const groupBy = <GroupBySelect visual={visual} catalogue={catalogue} onChange={onChange} />;

  return (
    <>
      {sourceSelect}
      {wells !== null && (
        <fieldset className="field field--group designer-well" aria-describedby={`${id}-category`}>
          <legend className="field__label designer-well__label">{t(wells.category)}</legend>
          <p id={`${id}-category`} className="field__hint">
            {t('dashboards.designer.categoryAxisHint')}
          </p>
          {groupBy}
        </fieldset>
      )}
      <fieldset
        className={`field field--group designer-data${wells === null ? '' : ' designer-well'}`}
        aria-describedby={`${id}-limit`}
      >
        <legend className={`field__label${wells === null ? '' : ' designer-well__label'}`}>
          {t(
            wells !== null
              ? wells.value
              : type === 'MAP'
                ? 'dashboards.designer.mapFigure'
                : 'dashboards.designer.dataLabel',
          )}
        </legend>
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
            {t(REPLACED_NOTE[replaced])}
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
              hint={null}
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
                    hint={measureHint(measure, t)}
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
              hint={source.overTime ? t('dashboards.designer.overTime') : null}
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
          {details.length > 0 && (
            <FieldGroup
              name={t('dashboards.designer.projectDetails')}
              hint={null}
              chosen={details.filter(groupedByDetail).length}
              open={sourceOpen(DETAILS)}
              onToggle={() => {
                toggleGroup(DETAILS);
              }}
            >
              {details.map((groupBy) => {
                const checked = groupedByDetail(groupBy);
                return (
                  <FieldRow
                    key={groupBy}
                    name={t(`dashboards.designer.groupBy.${groupBy}`)}
                    hint={null}
                    checked={checked}
                    disabled={checked}
                    onChange={() => {
                      setReplaced(null);
                      onChange({ ...visual, groupBy, groupField: null, groupDateUnit: null });
                    }}
                  />
                );
              })}
            </FieldGroup>
          )}
          {query !== '' && figures.length === 0 && sources.length === 0 && details.length === 0 && (
            <p className="designer__hint">{t('dashboards.designer.noFieldMatch')}</p>
          )}
        </div>
        {fields.error !== undefined && fields.error !== null && (
          <p className="designer__hint">{t('dashboards.designer.fieldsUnavailable')}</p>
        )}
      </fieldset>
      {wells === null && groupBy}
      {type === 'HEATMAP' && (
        <ColumnFieldSelect visual={visual} catalogue={catalogue} onChange={onChange} />
      )}
      {onFields || type === 'MAP' ? (
        // A map filters its figures whatever they are (docs/architecture/dashboard-designer-map-visual.md).
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

/** The fields of a list as a select's option groups: status, yes/no and date fields, each under its heading. */
function FieldOptions({ fields }: { fields: readonly FieldDescriptor[] }): ReactElement {
  const { t, language } = useI18n();
  const kinds = [
    { label: t('dashboards.designer.statusFields'), of: fields.filter((f) => f.type === 'CODE') },
    {
      label: t('dashboards.designer.yesNoFields'),
      of: fields.filter((f) => f.type === 'BOOLEAN'),
    },
    {
      label: t('dashboards.designer.dateFields'),
      of: fields.filter((f) => isDateType(f.type)),
    },
    { label: t('dashboards.designer.itemFields'), of: fields.filter((f) => f.type === 'ITEM') },
  ];
  return (
    <>
      {kinds
        .filter((kind) => kind.of.length > 0)
        .map((kind) => (
          <optgroup key={kind.label} label={kind.label}>
            {kind.of.map((field) => (
              <option key={field.key} value={field.key}>
                {fieldName(field.key, language)}
              </option>
            ))}
          </optgroup>
        ))}
    </>
  );
}

/** A heatmap's columns (22 Nov 2026): a status, yes/no or item field; the rows' field taken swaps the two. */
function ColumnFieldSelect({
  visual,
  catalogue,
  onChange,
}: {
  visual: Visual;
  catalogue: readonly FieldDescriptor[];
  onChange: (next: Visual) => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const axes = groupFieldsFor(grainOf(visual, catalogue), 'HEATMAP', catalogue);
  return (
    <div className="field">
      <label className="field__label" htmlFor={`${id}-columns`}>
        {t('dashboards.designer.heatmapColumns')}
      </label>
      <select
        id={`${id}-columns`}
        className="field__input"
        value={visual.columnField ?? ''}
        disabled={axes.length <= 1}
        onChange={(event) => {
          const chosen = event.target.value;
          onChange({
            ...visual,
            columnField: chosen,
            ...(chosen === visual.groupField ? { groupField: visual.columnField ?? null } : {}),
            // Its names name the old fields' values.
            labels: null,
          });
        }}
      >
        <FieldOptions fields={axes} />
      </select>
    </div>
  );
}

const DATE_UNITS: readonly DateUnit[] = ['YEAR', 'MONTH'];

/**
 * Group by: the project attributes the type takes, then, for a visual drawing fields, the status, yes/no and date fields of its grain; a date
 * field with a Year/Month select beside it. A line, and a visual over time, groups by its fields only.
 */
function GroupBySelect({
  visual,
  catalogue,
  onChange,
}: {
  visual: Visual;
  catalogue: readonly FieldDescriptor[];
  onChange: (next: Visual) => void;
}): ReactElement {
  const { t } = useI18n();
  const id = useId();
  const grain = grainOf(visual, catalogue);
  const fieldsOnly = groupsByFieldOnly(visual.visualType, grain);
  const groupings = fieldsOnly ? [] : groupBysOf(visual.visualType, sourceOf(visual));
  const groupFields = drawsFields(visual)
    ? groupFieldsFor(grain, visual.visualType, catalogue)
    : [];
  const groupField = visual.groupField ?? null;
  const dateField =
    groupField !== null &&
    groupFields.some((field) => field.key === groupField && isDateType(field.type));
  const attributes = groupings.map((groupBy) => (
    <option key={groupBy} value={groupBy}>
      {t(`dashboards.designer.groupBy.${groupBy}`)}
    </option>
  ));
  return (
    <div className="field">
      <label className="field__label" htmlFor={`${id}-group`}>
        {t(
          visual.visualType === 'MAP'
            ? 'dashboards.designer.mapPlaces'
            : visual.visualType === 'HEATMAP'
              ? 'dashboards.designer.heatmapRows'
              : 'dashboards.designer.groupByLabel',
        )}
      </label>
      <div className={dateField ? 'designer-data__group-by' : undefined}>
        <select
          id={`${id}-group`}
          className="field__input"
          value={groupField ?? visual.groupBy}
          disabled={groupings.length + groupFields.length <= 1}
          onChange={(event) => {
            const chosen = event.target.value;
            onChange(
              (GROUP_BYS as readonly string[]).includes(chosen)
                ? {
                    ...visual,
                    groupBy: chosen as GroupBy,
                    groupField: null,
                    groupDateUnit: null,
                  }
                : {
                    ...visual,
                    groupBy: 'NONE',
                    ...groupedBy(visual, chosen, catalogue),
                    // A heatmap's rows taking its columns' field swap the two; its names, of the old fields' values, go.
                    ...(visual.visualType === 'HEATMAP' && chosen === visual.columnField
                      ? { columnField: visual.groupField ?? null }
                      : {}),
                    ...(visual.visualType === 'HEATMAP' ? { labels: null } : {}),
                  },
            );
          }}
        >
          {groupFields.length === 0 ? (
            attributes
          ) : (
            <>
              {attributes.length > 0 && (
                <optgroup label={t('dashboards.designer.projectAttributes')}>{attributes}</optgroup>
              )}
              <FieldOptions fields={groupFields} />
            </>
          )}
        </select>
        {dateField && (
          <select
            className="field__input designer-data__unit"
            aria-label={t('dashboards.designer.dateUnitLabel')}
            value={visual.groupDateUnit ?? 'MONTH'}
            onChange={(event) => {
              onChange({ ...visual, groupDateUnit: event.target.value as DateUnit });
            }}
          >
            {DATE_UNITS.map((unit) => (
              <option key={unit} value={unit}>
                {t(`dashboards.designer.dateUnit.${unit}`)}
              </option>
            ))}
          </select>
        )}
      </div>
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
  const riskMatrix = useDesignRiskMatrix();
  const id = useId();
  const grain = grainOf(visual, catalogue);
  // The filter being added: null while none is, '' while its field is being picked, then the field until its first value is ticked.
  const [adding, setAdding] = useState<string | null>(null);
  const filters = visual.filters ?? [];
  const used = new Set(filters.map((filter) => filter.field));
  const testable = filterFieldsFor(grain, catalogue).filter((field) => !used.has(field.key));
  const dimensions = filterDimensionsFor(sourceOf(visual)).filter(
    (dimension) => !used.has(dimensionKey(dimension)),
  );
  const change = (next: FieldFilter[]) => {
    onChange({ ...visual, filters: next.length === 0 ? null : next });
  };

  return (
    <fieldset className="field field--group designer-data" aria-describedby={`${id}-hint`}>
      <legend className="field__label">{t('dashboards.designer.filtersLabel')}</legend>
      <p id={`${id}-hint`} className="field__hint">
        {t(
          sourceOf(visual) === 'RISKS'
            ? 'dashboards.designer.filtersHintRisks'
            : 'dashboards.designer.filtersHint',
          { count: MAX_FILTERS },
        )}
      </p>
      {filters.map((filter, at) => (
        <FilterValues
          key={filter.field}
          field={filter.field}
          chosen={filter.values}
          options={filterOptions(filter.field, catalogue, lookups, language, t, riskMatrix)}
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
          options={filterOptions(adding, catalogue, lookups, language, t, riskMatrix)}
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
            <FieldOptions fields={testable} />
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
