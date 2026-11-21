import { describe, expect, test } from 'vitest';

import { type ProjectIndicators } from '@/features/projects/api/types.ts';
import { projectSummary } from '@/test/projectFixtures.ts';

import {
  DEFAULT_MANAGED_SORT,
  filterRows,
  type ManagedRow,
  needsAction,
  sortRows,
} from './managedProjects.ts';

const INDICATORS: ProjectIndicators = {
  gapPoints: null,
  onPlan: null,
  daysRemaining: null,
  actionDue: null,
  followUpReasons: [],
};

function row(
  id: string,
  title: string,
  options: {
    onPlan?: ProjectIndicators['onPlan'];
    daysRemaining?: number | null;
    needsSetup?: boolean;
    updateDueDate?: string | null;
    overdueTasks?: number;
    requestsWaiting?: number;
    formalProjectId?: string | null;
  } = {},
): ManagedRow {
  return {
    project: projectSummary({
      id,
      title: { text: title, language: 'EN' },
      formalProjectId: options.formalProjectId ?? null,
      indicators: {
        ...INDICATORS,
        onPlan: options.onPlan ?? null,
        daysRemaining: options.daysRemaining ?? null,
        ...(options.needsSetup === undefined
          ? {}
          : {
              progress: {
                method: 'STAGES',
                source: 'STAGES',
                actualPercent: null,
                plannedPercent: null,
                asOf: null,
                currentStageCode: null,
                stages: [],
                needsSetup: options.needsSetup,
                methodChangeNeedsReason: false,
              },
            }),
      },
    }),
    attention: {
      projectId: id,
      updateDueDate: options.updateDueDate ?? null,
      overdueTasks: options.overdueTasks ?? 0,
      overdueMilestones: 0,
    },
    requestsWaiting: options.requestsWaiting ?? 0,
  };
}

const quiet = row('a', 'Alpha', { onPlan: 'ON_TRACK', daysRemaining: 300 });
const due = row('b', 'Bravo', { updateDueDate: '2026-11-30', daysRemaining: 200 });
const late = row('c', 'Charlie', { onPlan: 'LATE', overdueTasks: 2, daysRemaining: 50 });
const unplanned = row('d', 'Delta', { needsSetup: true, formalProjectId: 'PRJ-000417' });
const waiting = row('e', 'Echo', { onPlan: 'STALLED', requestsWaiting: 1 });
const rows = [quiet, due, late, unplanned, waiting];

describe('review round 8, point 13: the Project Manager’s projects', () => {
  test('"needs action this month" is an update due, an overdue task or milestone, or a request waiting', () => {
    expect(rows.filter(needsAction).map((r) => r.project.id)).toEqual(['b', 'c', 'e']);
    expect(filterRows(rows, new Set(['needsAction']), '').map((r) => r.project.id)).toEqual([
      'b',
      'c',
      'e',
    ]);
  });

  test('the chips combine, and late / stalled and no stage plan keep their projects', () => {
    expect(filterRows(rows, new Set(['lateOrStalled']), '').map((r) => r.project.id)).toEqual([
      'c',
      'e',
    ]);
    expect(filterRows(rows, new Set(['noStagePlan']), '').map((r) => r.project.id)).toEqual(['d']);
    expect(
      filterRows(rows, new Set(['needsAction', 'lateOrStalled']), '').map((r) => r.project.id),
    ).toEqual(['c', 'e']);
  });

  test('the search finds a project by its name or its number, ignoring case', () => {
    expect(filterRows(rows, new Set(), 'charl').map((r) => r.project.id)).toEqual(['c']);
    expect(filterRows(rows, new Set(), 'prj-000417').map((r) => r.project.id)).toEqual(['d']);
  });

  test('the default order puts what needs action first, then the worst on-plan, then the fewest days left', () => {
    expect(sortRows(rows, DEFAULT_MANAGED_SORT, 'en').map((r) => r.project.id)).toEqual([
      'c',
      'e',
      'b',
      'a',
      'd',
    ]);
  });

  test('names sort as people read numbers: "Project 2" before "Project 10"', () => {
    const numbered = [row('x', 'Project 10'), row('y', 'Project 2'), row('z', 'Project 1')];
    expect(
      sortRows(numbered, { key: 'title', direction: 'ascending' }, 'en').map((r) => r.project.id),
    ).toEqual(['z', 'y', 'x']);
  });

  test('every column sorts both ways, with an empty value last', () => {
    expect(
      sortRows(rows, { key: 'title', direction: 'descending' }, 'en').map((r) => r.project.id),
    ).toEqual(['e', 'd', 'c', 'b', 'a']);
    expect(
      sortRows(rows, { key: 'daysRemaining', direction: 'ascending' }, 'en').map(
        (r) => r.project.id,
      ),
    ).toEqual(['c', 'b', 'a', 'e', 'd']);
    expect(
      sortRows(rows, { key: 'daysRemaining', direction: 'descending' }, 'en').map(
        (r) => r.project.id,
      ),
    ).toEqual(['a', 'b', 'c', 'e', 'd']);
  });
});
