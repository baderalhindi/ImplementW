import { describe, expect, test } from 'vitest';

import { riskMatrixOf } from '@/features/risks/riskRules.ts';
import { translate, type TranslationKey } from '@/shared/i18n/i18n.ts';
import { PROJECT_ID } from '@/test/projectFixtures.ts';
import { standardMatrix } from '@/test/riskFixtures.ts';

import { type TableCell, type TableColumn, type TableRow, type Visual } from './designApi.ts';
import { countsRisks } from './riskMatrixContext.ts';
import {
  isRiskCount,
  isRiskField,
  RISK_COUNT,
  RISK_TABLE_COLUMNS,
  riskCellPath,
  riskCodeText,
  riskCountText,
  riskMissingText,
} from './riskFields.ts';

// The Risks record source's pure parts (20 Nov 2026, docs/architecture/dashboard-designer-record-sources.md).

const en = (key: TranslationKey, params?: Record<string, string | number>) =>
  translate('en', key, params);
const MATRIX = riskMatrixOf(standardMatrix());
const RISK_ID = 'f1000000-0000-4000-8000-000000000001';

const field = (key: string, aggregation: TableColumn['aggregation'] = 'NONE'): TableColumn => ({
  kind: 'FIELD',
  key,
  aggregation,
});
const cell = (key: string | null): TableCell => ({ key, label: null, value: null });
const row = (projectId: string | null, cells: TableCell[]): TableRow => ({
  projects: 1,
  projectId,
  cells,
});

describe('the Risks defaults', () => {
  test('a visual counts the number of risks; a table lists the contract’s six columns, none summarized', () => {
    expect(RISK_COUNT).toEqual({ field: 'RISK.RISK', aggregation: 'COUNT' });
    expect(RISK_TABLE_COLUMNS).toEqual(
      ['TITLE', 'PROJECT', 'RATING', 'STATUS', 'OWNER', 'NEXT_REVIEW_DATE'].map((code) =>
        field(`RISK.${code}`),
      ),
    );
  });

  test('a risk field is RISK.<CODE>; the project register’s risk exposure is not', () => {
    expect(isRiskField('RISK.STATUS')).toBe(true);
    expect(isRiskField('PROJECT.PROJECT_PROFILE.REGISTRATION_BUDGET')).toBe(false);
  });

  test('"Number of risks" in either language', () => {
    expect(riskCountText('en')).toBe(en('dashboards.designer.riskCount'));
    expect(riskCountText('en')).toMatch(/risks/i);
    expect(riskCountText('ar')).toBe(translate('ar', 'dashboards.designer.riskCount'));
    expect(riskCountText('ar')).not.toBe('dashboards.designer.riskCount');
  });

  test('only the count of RISK.RISK is the number of risks', () => {
    expect(isRiskCount({ field: 'RISK.RISK', aggregation: 'COUNT' })).toBe(true);
    expect(isRiskCount({ field: 'RISK.RISK', aggregation: 'SUM' })).toBe(false);
    expect(isRiskCount({ field: 'RISK.SCORE', aggregation: 'COUNT' })).toBe(false);
  });
});

describe('a risk code in words', () => {
  test('a status in the Risk feature’s words, an unknown status as its code', () => {
    expect(riskCodeText('STATUS', 'TREATMENT', 'en', en, null)).toBe(en('risks.status.TREATMENT'));
    expect(riskCodeText('STATUS', 'TREATMENT', 'ar', en, null)).toBe(
      translate('ar', 'risks.status.TREATMENT'),
    );
    expect(riskCodeText('STATUS', 'GONE', 'en', en, null)).toBe('GONE');
  });

  test('a rating as the matrix names it, else in the usual ratings’ words; the code for a rating neither defines', () => {
    expect(riskCodeText('RATING', 'HIGH', 'en', en, MATRIX)).toBe('High');
    expect(riskCodeText('RATING', 'HIGH', 'ar', en, MATRIX)).toBe('مرتفع');
    // Without the matrix, the usual ratings' own words.
    expect(riskCodeText('RATING', 'HIGH', 'en', en, null)).toBe(
      en('dashboards.designer.fieldValue.RISK.RATING.HIGH'),
    );
    expect(riskCodeText('RATING', 'HIGH', 'ar', en, null)).toBe(
      translate('ar', 'dashboards.designer.fieldValue.RISK.RATING.HIGH'),
    );
    expect(riskCodeText('RATING', 'EXTREME', 'en', en, MATRIX)).toBe('EXTREME');
  });

  test('a probability as its level and name, an impact level likewise', () => {
    expect(riskCodeText('PROBABILITY', '4', 'en', en, MATRIX)).toBe(
      en('risks.matrix.probabilityLevel', { level: '4', label: 'Likely' }),
    );
    expect(riskCodeText('PROBABILITY', '4', 'en', en, null)).toBe('4');
    expect(riskCodeText('IMPACT', '2', 'en', en, MATRIX)).toBe(
      en('risks.matrix.probabilityLevel', { level: '2', label: 'Impact level 2' }),
    );
    expect(riskCodeText('IMPACT', '2', 'en', en, null)).toBe(
      en('risks.matrix.impactLevel', { level: '2' }),
    );
  });

  test('an assessment’s field with no value reads Not assessed; any other field falls back to the designer’s own', () => {
    for (const code of ['RATING', 'PROBABILITY', 'IMPACT', 'SCORE']) {
      expect(riskMissingText(code, en)).toBe(en('risks.rating.none'));
    }
    expect(riskMissingText('STATUS', en)).toBeNull();
    expect(riskMissingText('OWNER', en)).toBeNull();
  });
});

describe('where a risk table cell opens', () => {
  const columns = RISK_TABLE_COLUMNS;
  const cells = [
    cell(RISK_ID),
    cell(PROJECT_ID),
    cell('HIGH'),
    cell('TREATMENT'),
    cell(null),
    cell(null),
  ];
  const [title, project, rating] = columns;
  if (title === undefined || project === undefined || rating === undefined) {
    throw new Error('The risk table has fewer than three columns');
  }

  test('a project cell opens its project; a risk title its risk in that project', () => {
    expect(riskCellPath(project, cell(PROJECT_ID), row(null, cells), columns)).toBe(
      `/projects/${PROJECT_ID}`,
    );
    expect(riskCellPath(title, cell(RISK_ID), row(null, cells), columns)).toBe(
      `/projects/${PROJECT_ID}/risks/${RISK_ID}`,
    );
  });

  test('a risk title takes the row’s project before the project cell’s', () => {
    const other = 'a0000000-0000-4000-8000-0000000000ff';
    expect(riskCellPath(title, cell(RISK_ID), row(other, cells), columns)).toBe(
      `/projects/${other}/risks/${RISK_ID}`,
    );
  });

  test('a risk title with no project known, a summarized column, a cell with no key and any other column open nothing', () => {
    expect(riskCellPath(title, cell(RISK_ID), row(null, [cell(RISK_ID)]), [title])).toBeUndefined();
    expect(
      riskCellPath(field('RISK.TITLE', 'COUNT'), cell(RISK_ID), row(PROJECT_ID, cells), columns),
    ).toBeUndefined();
    expect(riskCellPath(project, cell(null), row(null, cells), columns)).toBeUndefined();
    expect(riskCellPath(rating, cell('HIGH'), row(PROJECT_ID, cells), columns)).toBeUndefined();
    expect(
      riskCellPath(
        { kind: 'DIMENSION', key: 'SECTOR', aggregation: 'NONE' },
        cell('x'),
        row(PROJECT_ID, cells),
        columns,
      ),
    ).toBeUndefined();
  });
});

describe('whether a design counts risks', () => {
  test('a risk visual, at the top or inside a container, reads the matrix; projects only do not', () => {
    expect(countsRisks([{ source: null }, {}])).toBe(false);
    expect(countsRisks([{ source: 'PROJECTS' }, { source: 'RISKS' }])).toBe(true);
    const risks: Visual = {
      title: { ar: 'المخاطر', en: 'Risks' },
      visualType: 'CARD',
      source: 'RISKS',
      measures: [],
      groupBy: 'NONE',
      layoutColumn: 0,
      layoutRow: 0,
      layoutWidth: 4,
      layoutHeight: 2,
    };
    expect(countsRisks([{ children: [risks] }])).toBe(true);
    expect(countsRisks([{ children: [{ ...risks, source: null }] }])).toBe(false);
  });
});
