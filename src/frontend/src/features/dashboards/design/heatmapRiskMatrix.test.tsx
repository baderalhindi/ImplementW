import { render, screen, within } from '@testing-library/react';
import { type ReactNode } from 'react';
import { MemoryRouter } from 'react-router';
import { describe, expect, test } from 'vitest';

import { riskMatrixOf } from '@/features/risks/riskRules.ts';
import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';
import { standardMatrix } from '@/test/riskFixtures.ts';

import { type VisualGroup, type VisualResult } from './designApi.ts';
import { RiskMatrixContext } from './riskMatrixContext.ts';
import { VisualBody } from './VisualBody.tsx';

// The 22 Nov 2026 heatmap of risks by probability and impact is drawn as the Risk register's matrix: each cell in its rating's colour and
// words with its number of risks, probability rising up the rows ("coloured as the risk matrix").

const cell = (row: string, column: string, value: number): VisualGroup => ({
  key: row,
  label: null,
  projects: value === 0 ? 0 : 1,
  measured: value,
  valueEntered: value,
  values: [value],
  onPlan: null,
  columnKey: column,
  columnLabel: null,
});

const LEVELS = ['1', '2', '3', '4', '5'];

function heatmap(rows: string, columns: string, counts: Record<string, number>): VisualResult {
  return {
    title: { ar: 'عدد المخاطر', en: 'Number of risks' },
    visualType: 'HEATMAP',
    source: 'RISKS',
    measures: [],
    values: [{ field: 'RISK.RISK', aggregation: 'COUNT' }],
    groupBy: 'NONE',
    groupField: rows,
    columnField: columns,
    layoutColumn: 0,
    layoutRow: 0,
    layoutWidth: 6,
    layoutHeight: 6,
    groups: LEVELS.flatMap((r) => LEVELS.map((c) => cell(r, c, counts[`${r}:${c}`] ?? 0))),
  };
}

const withMatrix = (children: ReactNode) => (
  <I18nProvider initialLanguage="en">
    <MemoryRouter>
      <RiskMatrixContext value={riskMatrixOf(standardMatrix())}>{children}</RiskMatrixContext>
    </MemoryRouter>
  </I18nProvider>
);

describe('a heatmap of risks by probability and impact', () => {
  test('is the risk matrix: each cell its rating and its number of risks, probability 5 on top', () => {
    render(
      withMatrix(
        <VisualBody visual={heatmap('RISK.PROBABILITY', 'RISK.IMPACT', { '4:4': 2, '2:5': 1 })} />,
      ),
    );

    const grid = screen.getByRole('table');
    expect(grid.querySelector('[data-cell="4:4"]')?.getAttribute('data-rating')).toBe('CRITICAL');
    expect(grid.querySelector('[data-cell="4:4"]')?.textContent).toContain('2');
    expect(grid.querySelector('[data-cell="1:1"]')?.getAttribute('data-rating')).toBe('LOW');
    expect(within(grid).getAllByRole('row')[1]?.textContent).toContain('5');
    expect(screen.queryByText('Fewer')).toBeNull();
  });

  test('impact down the side and probability across read into the same cells', () => {
    render(
      withMatrix(<VisualBody visual={heatmap('RISK.IMPACT', 'RISK.PROBABILITY', { '4:2': 3 })} />),
    );

    // Row impact 4, column probability 2: the matrix's probability 2 × impact 4.
    expect(screen.getByRole('table').querySelector('[data-cell="2:4"]')?.textContent).toContain(
      '3',
    );
  });

  test('any other two fields keep the shaded matrix', () => {
    render(withMatrix(<VisualBody visual={heatmap('RISK.STATUS', 'RISK.RATING', { '1:1': 1 })} />));

    expect(document.querySelector('.heatmap__table')).not.toBeNull();
  });
});
