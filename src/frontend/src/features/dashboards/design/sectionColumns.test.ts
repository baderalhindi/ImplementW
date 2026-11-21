import { describe, expect, test } from 'vitest';

import { type Visual } from './designApi.ts';
import { normalized } from './fields.ts';
import { isLastShown, sentHiddenColumns, withColumnShown } from './sectionColumns.ts';

const section = (code: Visual['section'], hiddenColumns?: string[] | null): Visual => ({
  title: { ar: 'قسم', en: 'Section' },
  visualType: 'SECTION',
  measures: [],
  groupBy: 'NONE',
  section: code,
  layoutColumn: 0,
  layoutRow: 0,
  layoutWidth: 12,
  layoutHeight: 6,
  ...(hiddenColumns === undefined ? {} : { hiddenColumns }),
});

describe('a table section’s hidden columns', () => {
  test('are sent in the table’s order, null when none, and not at all on any other visual', () => {
    expect(sentHiddenColumns(section('SECTOR_TABLE', ['SPLIT', 'VALUE']))).toEqual([
      'VALUE',
      'SPLIT',
    ]);
    expect(sentHiddenColumns(section('SECTOR_TABLE', []))).toBeNull();
    expect(sentHiddenColumns(section('STATUS_TABLE'))).toBeNull();
    expect(sentHiddenColumns(section('ON_PLAN_COUNTS', ['PROJECTS']))).toBeUndefined();
    expect('hiddenColumns' in normalized(section('PROJECT_LOCATIONS', ['X']))).toBe(false);
    expect(normalized(section('TOP_FOLLOW_UP')).hiddenColumns).toBeNull();
  });

  test('a column hides and shows again; the last shown one stays', () => {
    const hidden = withColumnShown(section('STATUS_TABLE'), 'PROJECTS', false);
    expect(hidden.hiddenColumns).toEqual(['PROJECTS']);
    expect(isLastShown(hidden, 'PERCENT')).toBe(true);
    expect(withColumnShown(hidden, 'PERCENT', false).hiddenColumns).toEqual(['PROJECTS']);
    expect(withColumnShown(hidden, 'PROJECTS', true).hiddenColumns).toBeNull();
  });
});
