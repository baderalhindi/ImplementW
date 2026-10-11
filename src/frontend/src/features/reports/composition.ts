import { type BilingualLabel } from '@/features/identity-access/api/types.ts';

import {
  type ExplorerRunRequest,
  type ProjectionSemanticState,
  type ReportAllowlistEntry,
  type ReportColumnView,
  type ReportFieldReference,
  type ReportFilter,
  type ReportFilterOperator,
  type ReportSort,
  type ReportSortDirection,
  type ReportValueType,
  type SavedViewColumn,
  type SavedViewDetail,
} from './api/types.ts';
import { isSortableType } from './presentation.ts';

// MOD-061's model. A field is offered only when the API offered it — a column the report publishes, or an entry of
// the PUBLISHED REPORT_RULES allowlist (SCR-138) — and a composition names fields by the two codes the API gave them.
// There is no other way to name a field: no free text becomes a field, a join or a query (FG-02 §11.3, BR-RPT-046).

/** A field the person may choose, exactly as the API described it. */
export interface FieldOption {
  key: string;
  ref: ReportFieldReference;
  label: BilingualLabel;
  valueType: ReportValueType;
  semanticState: ProjectionSemanticState | null;
  isSortable: boolean;
  isFilterable: boolean;
  operators: ReportFilterOperator[];
  /** The field's closed set of values, when it has one: a filter then chooses among them. */
  values: string[];
  projectionCode: string | null;
  isSensitive: boolean;
  isDefaultVisible: boolean;
}

export interface FilterDraft {
  key: string;
  operator: ReportFilterOperator;
  value: string;
}

/** What the person composed: the shown columns in order, one sort, and (in the explorer) filters. */
export interface Composition {
  columns: string[];
  sort: { key: string; direction: ReportSortDirection } | null;
  filters: FilterDraft[];
}

export const EMPTY_COMPOSITION: Composition = { columns: [], sort: null, filters: [] };

export function fieldKey(ref: ReportFieldReference): string {
  return `${ref.sourceEntityCode}.${ref.fieldCode}`;
}

function refOf(field: ReportFieldReference): ReportFieldReference {
  return { sourceEntityCode: field.sourceEntityCode, fieldCode: field.fieldCode };
}

/** SCR-138: the allowlist's entries, and nothing else. */
export function optionsFromAllowlist(entries: ReportAllowlistEntry[]): FieldOption[] {
  return entries.map((entry) => ({
    key: fieldKey(entry),
    ref: refOf(entry),
    label: entry.label,
    valueType: entry.valueType,
    semanticState: entry.semanticState,
    isSortable: entry.isSortable && isSortableType(entry.valueType),
    isFilterable: entry.isFilterable && entry.operators.length > 0,
    operators: entry.operators,
    values: entry.values,
    projectionCode: entry.projectionCode,
    isSensitive: entry.isSensitive,
    isDefaultVisible: false,
  }));
}

/** A published report: its own columns, sortable by type; its filters are its parameters, not MOD-061's. */
export function optionsFromReport(columns: ReportColumnView[]): FieldOption[] {
  return columns.map((column) => ({
    key: fieldKey(column),
    ref: refOf(column),
    label: column.label,
    valueType: column.valueType,
    semanticState: column.semanticState,
    isSortable: isSortableType(column.valueType),
    isFilterable: false,
    operators: [],
    values: [],
    projectionCode: column.projectionCode,
    isSensitive: column.isSensitive,
    isDefaultVisible: column.isDefaultVisible,
  }));
}

/** The report's default columns, in its order: what a run shows when the person chose none. */
export function defaultColumns(options: FieldOption[]): string[] {
  return options.filter((option) => option.isDefaultVisible).map((option) => option.key);
}

/** Only keys that are offered survive: a stale choice is dropped, never sent. */
function offered(options: FieldOption[]): Map<string, FieldOption> {
  return new Map(options.map((option) => [option.key, option]));
}

export function toColumns(
  composition: Composition,
  options: FieldOption[],
): ReportFieldReference[] {
  const byKey = offered(options);
  return composition.columns.flatMap((key) => {
    const option = byKey.get(key);
    return option === undefined ? [] : [option.ref];
  });
}

export function toSort(composition: Composition, options: FieldOption[]): ReportSort[] {
  const option = composition.sort === null ? undefined : offered(options).get(composition.sort.key);
  return option === undefined || composition.sort === null || !option.isSortable
    ? []
    : [{ ...option.ref, direction: composition.sort.direction }];
}

export function toFilters(composition: Composition, options: FieldOption[]): ReportFilter[] {
  const byKey = offered(options);
  return composition.filters.flatMap((filter) => {
    const option = byKey.get(filter.key);
    return option?.operators.includes(filter.operator) === true
      ? [{ ...option.ref, operator: filter.operator, value: filter.value }]
      : [];
  });
}

export function toExplorerRequest(
  composition: Composition,
  options: FieldOption[],
): ExplorerRunRequest {
  return {
    columns: toColumns(composition, options),
    filters: toFilters(composition, options),
    sort: toSort(composition, options),
  };
}

/** A composition as a saved view stores it: each column with its sort direction, if it is the sorted one. */
export function toSavedColumns(
  composition: Composition,
  options: FieldOption[],
): SavedViewColumn[] {
  const sorted = toSort(composition, options)[0];
  return toColumns(composition, options).map((ref) => ({
    ...ref,
    sortDirection:
      sorted !== undefined && fieldKey(sorted) === fieldKey(ref) ? sorted.direction : null,
  }));
}

/** A saved composition read back: its columns in order, its sorted column, its filters. */
export function compositionOfView(view: SavedViewDetail): Composition {
  const sorted = view.columns.find((column) => column.sortDirection !== null);
  return {
    columns: view.columns.map(fieldKey),
    sort:
      sorted?.sortDirection == null
        ? null
        : { key: fieldKey(sorted), direction: sorted.sortDirection },
    filters: view.filters.map((filter) => ({
      key: fieldKey(filter),
      operator: filter.operator,
      value: filter.value,
    })),
  };
}

/** A filter's value parts: IN takes several, BETWEEN two, every other operator one (the API's comma form). */
export function valueParts(value: string): string[] {
  return value === '' ? [] : value.split(',');
}

/** Whether a filter is complete enough to send: a value, two for BETWEEN, at least one for IN. */
export function isFilterComplete(filter: FilterDraft): boolean {
  const parts = valueParts(filter.value).map((part) => part.trim());
  if (filter.operator === 'BETWEEN') {
    return parts.length === 2 && parts.every((part) => part !== '');
  }
  return parts.length > 0 && parts.every((part) => part !== '');
}
