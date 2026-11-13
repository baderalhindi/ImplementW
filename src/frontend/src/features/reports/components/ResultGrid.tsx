import { type ReactElement } from 'react';

import { SEMANTIC_STATES } from '@/features/dashboards/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { type ReportResultPage, type ReportRow } from '../api/types.ts';
import { cellContent } from '../presentation.ts';
import { CellValue } from './CellValue.tsx';
import { CoverageBanner } from './CoverageBanner.tsx';

interface ResultGridProps {
  result: ReportResultPage;
  caption: string;
  onPageChange: (page: number) => void;
}

/**
 * A run's result (FE-CMP-002): the Coverage/Freshness Banner, then one row per project (or snapshot) the server
 * authorised, each column headed by its label and its semantic state. The server owns the rows and the count; the SPA
 * never lists what it was not sent. When the report was run is said apart from any source's as-of (BR-RPT-030).
 */
export function ResultGrid({ result, caption, onPageChange }: ResultGridProps): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const selection = useRowSelection();
  const rowId = (row: ReportRow) => `${row.projectId}:${String(row.snapshotIndex ?? '')}`;
  /** The row's first value, or its place in the result when that value is unknown. */
  const rowName = (row: ReportRow, index: number) => {
    const [cell] = row.cells;
    const [column] = result.columns;
    const content =
      cell === undefined || column === undefined
        ? null
        : cellContent(cell, column, language, t, formatDateTime);
    return content?.kind === 'value'
      ? content.text
      : String((result.page - 1) * result.pageSize + index + 1);
  };
  return (
    <div className="report-result">
      <CoverageBanner sections={result.sections} columns={result.columns} />
      <p className="report-result__meta" data-generated-at={result.generatedAt}>
        {t('reports.result.generatedAt', { date: formatDateTime(result.generatedAt) })}
        {' · '}
        {t('reports.result.rowCount', { count: result.totalCount })}
      </p>
      {result.items.length === 0 ? (
        <EmptyState title={t('reports.result.empty')} />
      ) : (
        <TableContainer caption={caption} selection={{ selection, exportName: 'report-result' }}>
          <thead>
            <tr>
              <SelectAllHeader ids={result.items.map(rowId)} selection={selection} />
              {result.columns.map((column) => (
                <th
                  key={`${column.sourceEntityCode}.${column.fieldCode}`}
                  scope="col"
                  data-semantic-state={column.semanticState ?? undefined}
                >
                  {column.label[language]}
                  {column.semanticState !== null && (
                    <span className="report-result__semantic">
                      {t(SEMANTIC_STATES[column.semanticState].label)}
                    </span>
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {result.items.map((row, rowIndex) => (
              <tr key={rowId(row)}>
                <SelectRowCell
                  id={rowId(row)}
                  name={rowName(row, rowIndex)}
                  selection={selection}
                />
                {row.cells.map((cell, index) => {
                  const column = result.columns[index];
                  return column === undefined ? null : (
                    <td key={`${column.sourceEntityCode}.${column.fieldCode}`}>
                      <CellValue cell={cell} column={column} />
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </TableContainer>
      )}
      <Pagination
        page={result.page}
        pageSize={result.pageSize}
        totalCount={result.totalCount}
        onPageChange={onPageChange}
      />
    </div>
  );
}
