import { saveFile } from '@/shared/api/saveFile.ts';

/** A spreadsheet reads a cell starting with these as a formula; a leading apostrophe keeps it text (CSV injection). */
const FORMULA_START = /^[=+\-@\t\r]/;

function cellText(cell: Element): string {
  const copy = cell.cloneNode(true) as Element;
  copy.querySelectorAll('.visually-hidden, button, input').forEach((node) => {
    node.remove();
  });
  return copy.textContent.replace(/\s+/g, ' ').trim();
}

function csvField(value: string): string {
  const safe = FORMULA_START.test(value) ? `'${value}` : value;
  return /[",\n\r]/.test(safe) ? `"${safe.replaceAll('"', '""')}"` : safe;
}

/**
 * The table's checked rows as CSV, column for column as the screen shows them: the selection and actions columns are
 * left out, and so is text kept only for screen readers. What is read is what the viewer already sees.
 */
export function checkedRowsAsCsv(table: HTMLTableElement): { csv: string; count: number } {
  const headerCells = [...table.querySelectorAll('thead tr:last-child th')];
  const bodyRows = [...table.querySelectorAll('tbody tr')];
  const controlColumns = new Set(
    bodyRows.flatMap((row) =>
      [...row.children].flatMap((cell, index) =>
        cell.matches('.cell--select, .cell--actions') ? [index] : [],
      ),
    ),
  );
  const keep = headerCells
    .map((cell, index) => ({ cell, index }))
    .filter(
      ({ cell, index }) =>
        !controlColumns.has(index) &&
        !cell.matches('.cell--select, .cell--actions') &&
        cellText(cell) !== '',
    )
    .map(({ index }) => index);
  const rows = bodyRows.filter(
    (row) => row.querySelector(':scope > .cell--select input:checked') !== null,
  );
  const lines = [
    headerCells.filter((_, index) => keep.includes(index)).map((cell) => csvField(cellText(cell))),
    ...rows.map((row) => {
      const cells = [...row.children];
      return keep.map((index) =>
        csvField(cells[index] === undefined ? '' : cellText(cells[index])),
      );
    }),
  ].map((fields) => fields.join(','));
  return { csv: lines.join('\r\n'), count: rows.length };
}

/** Saves the checked rows; the byte-order mark lets Excel read Arabic as UTF-8. */
export function exportCheckedRows(table: HTMLTableElement, name: string): number {
  const { csv, count } = checkedRowsAsCsv(table);
  const day = new Date().toISOString().slice(0, 10);
  saveFile(
    { content: new Blob(['﻿', csv], { type: 'text/csv;charset=utf-8' }), fileName: null },
    `${name}-${day}.csv`,
  );
  return count;
}
