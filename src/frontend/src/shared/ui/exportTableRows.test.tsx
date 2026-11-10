import { render } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { checkedRowsAsCsv } from './exportTableRows.ts';

function renderTable(): HTMLTableElement {
  const { container } = render(
    <table>
      <thead>
        <tr>
          <th className="cell--select">
            <input type="checkbox" readOnly />
          </th>
          <th>Code</th>
          <th>Name</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td className="cell--select">
            <input type="checkbox" checked readOnly />
          </td>
          <td>A-1</td>
          <td>
            Plan <span className="visually-hidden">for screen readers</span>
          </td>
          <td className="cell--actions">
            <button type="button">Edit</button>
          </td>
        </tr>
        <tr>
          <td className="cell--select">
            <input type="checkbox" readOnly />
          </td>
          <td>B-2</td>
          <td>Skipped</td>
          <td className="cell--actions">
            <button type="button">Edit</button>
          </td>
        </tr>
        <tr>
          <td className="cell--select">
            <input type="checkbox" checked readOnly />
          </td>
          <td>=cmd()</td>
          <td>Says &quot;hi&quot;, twice</td>
          <td className="cell--actions" />
        </tr>
      </tbody>
    </table>,
  );
  const table = container.querySelector('table');
  if (table === null) {
    throw new Error('No table.');
  }
  return table;
}

describe('Export selected', () => {
  test('writes only the checked rows, as the screen shows them, without the control columns', () => {
    const { csv, count } = checkedRowsAsCsv(renderTable());
    expect(count).toBe(2);
    expect(csv.split('\r\n')).toEqual([
      'Code,Name',
      'A-1,Plan',
      // A formula is neutralised, and quotes and commas are escaped.
      `'=cmd(),"Says ""hi"", twice"`,
    ]);
  });
});
