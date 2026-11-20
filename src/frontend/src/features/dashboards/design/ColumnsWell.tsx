import { type ReactElement, useId, useState } from 'react';

import { type ApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import {
  type FieldDescriptor,
  type TableAggregation,
  type TableColumn,
  type TableColumnKind,
  type Visual,
} from './designApi.ts';
import { fieldName, sourceName } from './fields.ts';
import { FieldGroup, RemoveButton } from './FieldList.tsx';
import {
  addedColumn,
  asColumnTable,
  columnId,
  columnName,
  columnsOf,
  freeAggregations,
  MAX_COLUMNS,
  movedColumn,
  tableDimensionsFor,
  tableMeasuresFor,
} from './tableColumns.ts';
import { sourceOf } from './recordSources.ts';
import { measureHint } from './visuals.ts';

const DETAILS = 'DETAILS';
const FIGURES = 'FIGURES';

/** One entry the field list offers: what it adds and its words. */
interface Offer {
  kind: TableColumnKind;
  key: string;
  name: string;
  /** A field's type, beside its name for the eye only. */
  hint: string | null;
  /** Where a figure's boxes are ("On-plan indicator"), part of its name: two figures read alike without it. */
  source: string | null;
}

/**
 * A table's Columns (20 Nov 2026, docs/architecture/dashboard-designer-power-table.md), as Power BI's: an ordered list, each column with its
 * calculation ("Don't summarize" groups the rows by it), moved by dragging or by its arrows and removed by its ×; Add column opens the field
 * list (Project details, Portfolio figures, then each data source), searched as the Data panel's; and Show totals. A table saved with measures
 * or field values shows them as columns, and becomes a column table at its first change.
 */
export function ColumnsWell({
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
  const catalogue = fields.data ?? [];
  const columns = columnsOf(visual, catalogue);
  const [adding, setAdding] = useState(false);
  const [search, setSearch] = useState('');
  const [closed, setClosed] = useState<ReadonlySet<string>>(() => new Set());
  const [dragged, setDragged] = useState<number | null>(null);

  const change = (next: TableColumn[]) => {
    onChange(asColumnTable(visual, next, catalogue));
  };
  const move = (from: number, to: number) => {
    change(movedColumn(columns, from, to));
  };

  const query = search.trim().toLocaleLowerCase();
  const matches = (words: string) => query === '' || words.toLocaleLowerCase().includes(query);
  // A table on Risks offers its risks' project attributes and the risk fields; the Portfolio figures count projects, so not those.
  const source = sourceOf(visual);
  const dimensions = tableDimensionsFor(source);
  const measures = tableMeasuresFor(source);
  const details: Offer[] = dimensions
    .map((key) => ({
      kind: 'DIMENSION' as const,
      key,
      name: columnName({ kind: 'DIMENSION', key }, language),
      hint: null,
      source: null,
    }))
    .filter((offer) => matches(offer.name));
  const figures: Offer[] = measures
    .map((key) => ({
      kind: 'MEASURE' as const,
      key,
      name: columnName({ kind: 'MEASURE', key }, language),
      hint: null,
      source: measureHint(key, t),
    }))
    .filter((offer) => matches(offer.name));
  // A column table is calculated over projects: the fields over time are not offered.
  const offered = catalogue.filter((field) => field.grain === 'PROJECT');
  const sources = [...new Set(offered.map((field) => field.projectionCode))].flatMap((code) => {
    const name = sourceName(code, language);
    const offers: Offer[] = offered
      .filter(
        (field) =>
          field.projectionCode === code &&
          (matches(name) || matches(fieldName(field.key, language)) || matches(field.fieldCode)),
      )
      .map((field) => ({
        kind: 'FIELD',
        key: field.key,
        name: fieldName(field.key, language),
        hint: t(`dashboards.designer.fieldType.${field.type}`),
        source: null,
      }));
    return offers.length === 0 ? [] : [{ code, name, offers }];
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
  const open = (group: string) => query !== '' || !closed.has(group);
  const offerRows = (offers: Offer[]) =>
    offers.map((offer) => {
      const column = addedColumn(columns, offer.kind, offer.key, catalogue);
      return (
        <li key={`${offer.kind}:${offer.key}`}>
          <button
            type="button"
            className="designer-data__field designer-data__pick"
            disabled={column === null}
            onClick={() => {
              if (column !== null) {
                change([...columns, column]);
              }
            }}
          >
            <span className="designer-data__field-name" dir="auto">
              {offer.name}
            </span>
            {offer.source !== null && <span className="designer-data__type">{offer.source}</span>}
            {offer.hint !== null && (
              <span className="designer-data__type" aria-hidden="true">
                {offer.hint}
              </span>
            )}
          </button>
        </li>
      );
    });
  const chosenOf = (kind: TableColumnKind, keys: readonly string[]) =>
    columns.filter((column) => column.kind === kind && keys.includes(column.key)).length;

  return (
    <fieldset className="field field--group designer-data" aria-describedby={`${id}-hint`}>
      <legend className="field__label">{t('dashboards.designer.columnsLabel')}</legend>
      <p id={`${id}-hint`} className="field__hint">
        {t('dashboards.designer.columnsWellHint', { count: MAX_COLUMNS })}
      </p>
      <ul
        className="designer-data__chosen designer-columns-well"
        role="list"
        aria-label={t('dashboards.designer.columnsLabel')}
      >
        {columns.map((column, index) => {
          const name = columnName(column, language);
          const allowed = freeAggregations(columns, index, catalogue);
          return (
            <li
              key={columnId(column)}
              className={`designer-data__value designer-columns-well__column${
                dragged === index ? ' is-dragging' : ''
              }`}
              draggable
              onDragStart={(event) => {
                event.dataTransfer.effectAllowed = 'move';
                setDragged(index);
              }}
              onDragOver={(event) => {
                if (dragged !== null) {
                  event.preventDefault();
                }
              }}
              onDrop={(event) => {
                event.preventDefault();
                if (dragged !== null) {
                  move(dragged, index);
                }
                setDragged(null);
              }}
              onDragEnd={() => {
                setDragged(null);
              }}
            >
              <span className="designer-columns-well__grip" aria-hidden="true" />
              <span className="designer-data__value-name" dir="auto">
                {name}
              </span>
              {column.aggregation !== null && (
                <select
                  className="field__input designer-data__aggregation"
                  aria-label={t('dashboards.designer.columnAggregationLabel', { column: name })}
                  value={column.aggregation}
                  disabled={allowed.length <= 1}
                  onChange={(event) => {
                    const aggregation = event.target.value as TableAggregation;
                    change(
                      columns.map((other, at) =>
                        at === index ? { ...other, aggregation } : other,
                      ),
                    );
                  }}
                >
                  {allowed.map((aggregation) =>
                    aggregation === null ? null : (
                      <option key={aggregation} value={aggregation}>
                        {t(`dashboards.designer.aggregation.${aggregation}`)}
                      </option>
                    ),
                  )}
                </select>
              )}
              <span className="designer-columns-well__actions">
                <MoveButton
                  label={t('dashboards.designer.moveColumnUp', { column: name })}
                  up
                  disabled={index === 0}
                  onClick={() => {
                    move(index, index - 1);
                  }}
                />
                <MoveButton
                  label={t('dashboards.designer.moveColumnDown', { column: name })}
                  up={false}
                  disabled={index === columns.length - 1}
                  onClick={() => {
                    move(index, index + 1);
                  }}
                />
                <RemoveButton
                  label={t('dashboards.designer.removeColumn', { column: name })}
                  disabled={columns.length === 1}
                  onClick={() => {
                    change(columns.filter((_, at) => at !== index));
                  }}
                />
              </span>
            </li>
          );
        })}
      </ul>
      {columns.length >= MAX_COLUMNS && (
        <p className="designer__hint">
          {t('dashboards.designer.columnsFull', { count: MAX_COLUMNS })}
        </p>
      )}
      <button
        type="button"
        className="button designer-data__add"
        aria-expanded={adding}
        aria-controls={`${id}-list`}
        onClick={() => {
          setAdding(!adding);
        }}
      >
        {t('dashboards.designer.addColumn')}
      </button>
      {adding && (
        <div id={`${id}-list`} className="designer-columns-well__add">
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
            {details.length > 0 && (
              <FieldGroup
                name={t('dashboards.designer.projectDetails')}
                hint={null}
                chosen={chosenOf('DIMENSION', dimensions)}
                open={open(DETAILS)}
                onToggle={() => {
                  toggleGroup(DETAILS);
                }}
              >
                {offerRows(details)}
              </FieldGroup>
            )}
            {figures.length > 0 && (
              <FieldGroup
                name={t('dashboards.designer.portfolioFigures')}
                hint={null}
                chosen={chosenOf('MEASURE', measures)}
                open={open(FIGURES)}
                onToggle={() => {
                  toggleGroup(FIGURES);
                }}
              >
                {offerRows(figures)}
              </FieldGroup>
            )}
            {sources.map((source) => (
              <FieldGroup
                key={source.code}
                name={source.name}
                hint={null}
                chosen={chosenOf(
                  'FIELD',
                  source.offers.map((offer) => offer.key),
                )}
                open={open(source.code)}
                onToggle={() => {
                  toggleGroup(source.code);
                }}
              >
                {offerRows(source.offers)}
              </FieldGroup>
            ))}
            {query !== '' &&
              details.length === 0 &&
              figures.length === 0 &&
              sources.length === 0 && (
                <p className="designer__hint">{t('dashboards.designer.noFieldMatch')}</p>
              )}
          </div>
          {fields.error !== undefined && fields.error !== null && (
            <p className="designer__hint">{t('dashboards.designer.fieldsUnavailable')}</p>
          )}
        </div>
      )}
      <label className="field__checkbox">
        <input
          type="checkbox"
          checked={visual.showTotals !== false}
          onChange={(event) => {
            onChange({
              ...asColumnTable(visual, columns, catalogue),
              showTotals: event.target.checked,
            });
          }}
        />
        {t('dashboards.designer.showTotals')}
      </label>
    </fieldset>
  );
}

function MoveButton({
  label,
  up,
  disabled,
  onClick,
}: {
  label: string;
  up: boolean;
  disabled: boolean;
  onClick: () => void;
}): ReactElement {
  return (
    <button
      type="button"
      className="designer-data__remove designer-columns-well__move"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
    >
      <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
        <path d={up ? 'M6 15l6-6 6 6' : 'M6 9l6 6 6-6'} />
      </svg>
    </button>
  );
}
