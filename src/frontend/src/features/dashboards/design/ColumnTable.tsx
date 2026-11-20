import { type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { type OnPlanStatus } from '@/features/projects/api/types.ts';
import { OnPlanBadge } from '@/features/projects/components/FollowUpBadges.tsx';
import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { type SortDirection, SortHeader } from '@/shared/ui/SortHeader.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import {
  type GroupBy,
  type TableCell,
  type TableColumn,
  type TableResult,
  type TableRow,
} from './designApi.ts';
import {
  cellExact,
  cellText,
  columnHeading,
  groupsRows,
  isFigureType,
  sortedRows,
  type TableSort,
} from './tableColumns.ts';
import { riskCellPath } from './riskFields.ts';
import { useDesignRiskMatrix } from './riskMatrixContext.ts';
import { drillPath, GROUP_BYS } from './visuals.ts';

const ON_PLAN: readonly string[] = ['ON_TRACK', 'LATE', 'STALLED'];

/** No column sorted yet: the API's order (its first grouping column's words). */
const UNSORTED = { key: '', direction: 'ascending' as SortDirection };

/**
 * A column table as the dashboard's section tables draw theirs (20 Nov 2026): a selection column with export, the columns' headings (each sorts
 * the drawn rows, for the viewer only), cells formatted by their column's type (an amount compact with its full figure on hover, n%, ±points, a
 * date, the On plan badge), the grouping cells opening the projects under them and a title the project, the totals row, and the note when the
 * rows were cut at 500. With `filters` (on the dashboard) the cells link; in the designer they do not.
 */
export function ColumnTable({
  caption,
  columns,
  table,
  showTotals,
  filters,
}: {
  caption: string;
  columns: readonly TableColumn[];
  table: TableResult;
  showTotals: boolean;
  filters: PortfolioFilters | undefined;
}): ReactElement {
  const { t, language } = useI18n();
  const lookups = useProjectLookups();
  const riskMatrix = useDesignRiskMatrix();
  const names = { ...lookups, riskMatrix };
  const selection = useRowSelection();
  const [sort, setSort] = useState<{ key: string; direction: SortDirection }>(UNSORTED);
  const types = table.columnTypes;
  const textOf = (cell: TableCell, at: number) => {
    const column = columns[at];
    return column === undefined ? '' : cellText(column, types[at], cell, names, t, language);
  };
  const order: TableSort | null =
    sort.key === '' ? null : { column: Number(sort.key), direction: sort.direction };
  const rows = sortedRows(table.rows, order, types, textOf, language);
  const rowId = (row: TableRow) =>
    row.projectId ?? row.cells.map((cell) => cell.key ?? cell.label?.en ?? '').join('|');
  const groupsAny = columns.some(groupsRows);
  // With no grouping column there is one row, which is already the total.
  // A table of grouping columns only (a list of risks) has nothing to total.
  const totals = showTotals && groupsAny && !columns.every(groupsRows) ? table.totals : null;

  /** Where a grouping cell opens: the project for its title or ID, the projects list filtered by its attribute; nowhere in the designer. */
  const hrefOf = (column: TableColumn, cell: TableCell, row: TableRow): string | undefined => {
    if (filters === undefined) {
      return undefined;
    }
    if (column.kind === 'FIELD') {
      // A table on Risks: a project cell opens its project, a risk's title the risk.
      return riskCellPath(column, cell, row, columns);
    }
    if (column.kind !== 'DIMENSION' || column.aggregation !== 'NONE') {
      return undefined;
    }
    if (column.key === 'TITLE' || column.key === 'FORMAL_PROJECT_ID') {
      return row.projectId === null ? undefined : `/projects/${row.projectId}`;
    }
    return (GROUP_BYS as readonly string[]).includes(column.key)
      ? drillPath('PROJECTS', column.key as GroupBy, cell.key, filters)
      : undefined;
  };

  const cellOf = (cell: TableCell | undefined, at: number, row: TableRow | null) => {
    const column = columns[at];
    if (column === undefined) {
      return null;
    }
    const type = types[at];
    if (cell === undefined || (row === null && groupsRows(column))) {
      // The totals row's grouping cells are empty, as the sections' total rows are.
      return <td key={at} />;
    }
    if (type === 'ON_PLAN') {
      return (
        <td key={at}>
          <OnPlanBadge
            status={
              cell.key !== null && ON_PLAN.includes(cell.key) ? (cell.key as OnPlanStatus) : null
            }
          />
        </td>
      );
    }
    const words = textOf(cell, at);
    if (isFigureType(type)) {
      // As the sections write them: a count left to right in its cell, any other figure in the cell's own direction.
      return cell.value === null ? (
        <td key={at}>
          <span className="value--unavailable">{words}</span>
        </td>
      ) : type === 'COUNT' ? (
        <td key={at} dir="ltr" className="cell--ltr">
          {words}
        </td>
      ) : (
        <td key={at}>
          {/* A percentage and points left to right, as the sections write them; an amount, days or a date reads in its own words' order. */}
          <span
            dir={type === 'PERCENT' || type === 'POINTS' ? 'ltr' : undefined}
            className={type === 'SAR' ? 'value--nowrap' : undefined}
            title={cellExact(type, cell, t)}
          >
            {words}
          </span>
        </td>
      );
    }
    const href = row === null ? undefined : hrefOf(column, cell, row);
    const content =
      href === undefined ? (
        <span dir="auto">{words}</span>
      ) : (
        <Link
          to={href}
          title={
            column.kind === 'FIELD'
              ? undefined
              : t('dashboards.drill.projects', { count: row?.projects ?? 0 })
          }
          dir="auto"
        >
          {words}
        </Link>
      );
    // The row's first grouping cell heads it, as a section's name cell does.
    return at === columns.findIndex(groupsRows) ? (
      <th key={at} scope="row">
        {content}
      </th>
    ) : (
      <td key={at}>{content}</td>
    );
  };

  return (
    <div className="designed-table">
      <TableContainer caption={caption} selection={{ selection, exportName: 'designed-table' }}>
        <thead>
          <tr>
            <SelectAllHeader ids={rows.map(rowId)} selection={selection} />
            {columns.map((column, at) => (
              <SortHeader
                key={at}
                label={columnHeading(column, language)}
                column={String(at)}
                sort={sort}
                onSort={setSort}
                firstDirection={isFigureType(types[at]) ? 'descending' : 'ascending'}
              />
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowId(row)}>
              <SelectRowCell
                id={rowId(row)}
                name={row.cells.map((cell, at) => textOf(cell, at)).join(' ')}
                selection={selection}
              />
              {columns.map((_, at) => cellOf(row.cells[at], at, row))}
            </tr>
          ))}
        </tbody>
        {totals !== null && (
          <tfoot>
            <tr data-row="total">
              <td className="cell--select" />
              {columns.map((column, at) =>
                at === 0 && groupsRows(column) ? (
                  <th key={at} scope="row">
                    {t('dashboards.insights.total')}
                  </th>
                ) : (
                  cellOf(totals[at], at, null)
                ),
              )}
            </tr>
          </tfoot>
        )}
      </TableContainer>
      {table.truncated && (
        <p className="insights__notes" role="note">
          {t('dashboards.designer.tableTruncated')}
        </p>
      )}
    </div>
  );
}
