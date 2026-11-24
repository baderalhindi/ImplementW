import { type CSSProperties, type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

import { type VisualGroup } from './designApi.ts';
import { heatmapMatrix, heatStrength } from './heatmap.ts';

/**
 * A heatmap (22 Nov 2026): a table, a row per value of the rows' field and a column per value of the columns' field, each cell its figure on a colour
 * as strong as the figure is large. A table, so a screen reader reads each cell with its row and column headers; the colour only repeats the number.
 */
export function HeatmapChart({
  caption,
  groups,
  rowsHeading,
  columnsHeading,
  rowText,
  columnText,
  format,
  cellHint,
}: {
  caption: string;
  groups: readonly VisualGroup[];
  rowsHeading: string;
  columnsHeading: string;
  rowText: (group: VisualGroup) => string;
  columnText: (group: VisualGroup) => string;
  format: (value: number | null) => string;
  /** A cell's title: what it counts ("3 risks"). */
  cellHint: (group: VisualGroup) => string;
}): ReactElement {
  const { t } = useI18n();
  const matrix = heatmapMatrix(groups);
  return (
    <div className="heatmap">
      <div className="heatmap__scroll">
        <table className="heatmap__table">
          <caption className="visually-hidden">{caption}</caption>
          <thead>
            <tr>
              <th scope="col" className="heatmap__corner">
                <span className="heatmap__axis heatmap__axis--rows">{rowsHeading}</span>
                <span className="heatmap__axis heatmap__axis--columns">{columnsHeading}</span>
              </th>
              {matrix.columns.map((column) => (
                <th key={column.key ?? 'none'} scope="col" className="heatmap__column">
                  {columnText(column)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {matrix.rows.map((row) => (
              <tr key={row.key ?? 'none'}>
                <th scope="row" className="heatmap__row">
                  {rowText(row)}
                </th>
                {matrix.columns.map((column) => {
                  const cell = matrix.cell(row.key, column.key);
                  const value = cell?.values[0] ?? null;
                  const strength = heatStrength(value, matrix.max);
                  return (
                    <td
                      key={column.key ?? 'none'}
                      className={`heatmap__cell${strength > 0.55 ? ' heatmap__cell--strong' : ''}`}
                      style={{ '--heat': String(strength) } as CSSProperties}
                      title={cell === undefined ? undefined : cellHint(cell)}
                    >
                      {format(value)}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="heatmap__legend" aria-hidden="true">
        <span>{t('dashboards.designer.heatmapLow')}</span>
        <span className="heatmap__ramp" />
        <span>{t('dashboards.designer.heatmapHigh')}</span>
      </p>
    </div>
  );
}
