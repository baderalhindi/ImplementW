import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { ALLOWLIST, cell, FINANCIAL_COLUMNS } from '@/test/reportFixtures.ts';

import {
  compositionOfView,
  EMPTY_COMPOSITION,
  isFilterComplete,
  optionsFromAllowlist,
  optionsFromReport,
  toExplorerRequest,
  toSavedColumns,
} from './composition.ts';
import { cellContent, failureMessage, isJobCancellable, jobTone } from './presentation.ts';

// The rules the reports screens rest on, without a screen: a cell is never shown as 0 or blank, and a composition
// sends only what the API offered.

const t = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const formatDateTime = (value: string) => value;
function column(fieldCode: string) {
  const found = FINANCIAL_COLUMNS.find((c) => c.fieldCode === fieldCode);
  if (found === undefined) {
    throw new Error(`No column ${fieldCode}.`);
  }
  return found;
}

describe('cellContent', () => {
  test('a masked value is Restricted even if the source sent one', () => {
    expect(
      cellContent(
        cell('1250000.00', { isMasked: true }),
        column('APPROVED_BUDGET'),
        'en',
        t,
        formatDateTime,
      ),
    ).toEqual({ kind: 'unknown', reason: 'RESTRICTED' });
  });

  test('a value-less cell with no reason is No data, never blank or 0', () => {
    expect(
      cellContent(
        cell(null, { unknownReason: null }),
        column('APPROVED_BUDGET'),
        'en',
        t,
        formatDateTime,
      ),
    ).toEqual({ kind: 'unknown', reason: 'MISSING' });
  });

  test('a reason the API gave is kept', () => {
    expect(
      cellContent(
        cell(null, { unknownReason: 'NOT_APPLICABLE' }),
        column('APPROVED_BUDGET'),
        'en',
        t,
        formatDateTime,
      ),
    ).toEqual({ kind: 'unknown', reason: 'NOT_APPLICABLE' });
  });

  test('a zero the source counted is shown as 0, an amount grouped to its two places', () => {
    expect(
      cellContent(cell('0'), { valueType: 'COUNT', projectionCode: null }, 'en', t, formatDateTime),
    ).toMatchObject({ kind: 'value', text: '0' });
    expect(
      cellContent(cell('1250000.00'), column('APPROVED_BUDGET'), 'en', t, formatDateTime),
    ).toMatchObject({ kind: 'value', text: t('reports.units.sar', { amount: '1,250,000.00' }) });
  });

  test("a code reads in its source's words, a reference by the name the API sent", () => {
    expect(
      cellContent(cell('AMBER'), column('FINANCIAL_STATUS'), 'en', t, formatDateTime),
    ).toMatchObject({
      kind: 'value',
      text: t('financialKpi.financialStatus.AMBER'),
    });
    expect(
      cellContent(
        cell('d1', { label: { en: 'Strategy', ar: 'الاستراتيجية' } }),
        column('DEPARTMENT'),
        'ar',
        t,
        formatDateTime,
      ),
    ).toMatchObject({ kind: 'value', text: 'الاستراتيجية', isText: true });
  });
});

describe('composition', () => {
  const options = optionsFromAllowlist(ALLOWLIST);

  test('a field, filter or sort that is not offered is dropped, never sent', () => {
    const request = toExplorerRequest(
      {
        columns: ['PROJECT.TITLE', 'FINANCIAL_KPI_FINANCIAL_POSITION.APPROVED_BUDGET'],
        sort: { key: 'FINANCIAL_KPI_FINANCIAL_POSITION.APPROVED_BUDGET', direction: 'DESC' },
        filters: [
          { key: 'PROJECT.TITLE', operator: 'EQ', value: 'x' },
          { key: 'PROJECT_LIFECYCLE_STATE.LIFECYCLE_STATE', operator: 'CONTAINS', value: 'A' },
          { key: 'PROJECT_LIFECYCLE_STATE.LIFECYCLE_STATE', operator: 'EQ', value: 'ACTIVE' },
        ],
      },
      options,
    );
    expect(request).toEqual({
      columns: [{ sourceEntityCode: 'PROJECT', fieldCode: 'TITLE' }],
      sort: [],
      filters: [
        {
          sourceEntityCode: 'PROJECT_LIFECYCLE_STATE',
          fieldCode: 'LIFECYCLE_STATE',
          operator: 'EQ',
          value: 'ACTIVE',
        },
      ],
    });
  });

  test("a report's own columns are sortable by type, never filterable", () => {
    const report = optionsFromReport(FINANCIAL_COLUMNS);
    expect(report.filter((option) => option.isFilterable)).toEqual([]);
    expect(report.find((option) => option.ref.fieldCode === 'DEPARTMENT')?.isSortable).toBe(false);
  });

  test('a saved composition round-trips: columns in order, the sorted one, its filters', () => {
    const composition = {
      columns: ['PROJECT.FORMAL_PROJECT_ID', 'PROJECT.TITLE'],
      sort: { key: 'PROJECT.TITLE', direction: 'DESC' as const },
      filters: [{ key: 'PROJECT.FORMAL_PROJECT_ID', operator: 'CONTAINS' as const, value: 'PRJ' }],
    };
    const columns = toSavedColumns(composition, options);
    expect(columns.map((saved) => saved.sortDirection)).toEqual([null, 'DESC']);
    expect(
      compositionOfView({
        columns,
        filters: toExplorerRequest(composition, options).filters,
      } as Parameters<typeof compositionOfView>[0]),
    ).toEqual(composition);
    expect(toSavedColumns(EMPTY_COMPOSITION, options)).toEqual([]);
  });

  test('a filter needs a value, a range both ends, a list at least one', () => {
    expect(isFilterComplete({ key: 'k', operator: 'EQ', value: '' })).toBe(false);
    expect(isFilterComplete({ key: 'k', operator: 'BETWEEN', value: '1,' })).toBe(false);
    expect(isFilterComplete({ key: 'k', operator: 'BETWEEN', value: '1,5' })).toBe(true);
    expect(isFilterComplete({ key: 'k', operator: 'IN', value: 'ACTIVE' })).toBe(true);
  });
});

describe('jobs', () => {
  test('a job may be cancelled only before it runs, and is never shown in green', () => {
    expect(isJobCancellable('QUEUED')).toBe(true);
    expect(isJobCancellable('RUNNING')).toBe(false);
    expect(jobTone('COMPLETED')).toBe('neutral');
    expect(jobTone('FAILED')).toBe('negative');
  });

  test('a failure code reads in words, an unknown one as itself', () => {
    expect(failureMessage('REPORT_EXPORT_SIZE_LIMIT_EXCEEDED', t)).toBe(
      t('reports.failure.REPORT_EXPORT_SIZE_LIMIT_EXCEEDED'),
    );
    expect(failureMessage('SOMETHING_NEW', t)).toBe(
      t('reports.failure.unknown', { code: 'SOMETHING_NEW' }),
    );
  });
});
