import {
  type PortfolioBreakdownRow,
  type PortfolioCard,
  type PortfolioInsights,
} from '@/features/dashboards/api/insightTypes.ts';

import { PROJECT_ID } from './projectFixtures.ts';

// Test data only. Ids of the catalogue items named by the rows; the item names fall back to "Item xxxxxxxx" without lookups.

export const PORTFOLIO_A = 'aa000000-0000-4000-8000-000000000001';
export const INITIATIVE_A = 'ab000000-0000-4000-8000-000000000001';
export const SECTOR_A = 'ac000000-0000-4000-8000-000000000001';

export function breakdownRow(
  overrides: Partial<PortfolioBreakdownRow> = {},
): PortfolioBreakdownRow {
  return {
    itemId: PORTFOLIO_A,
    parentItemId: null,
    projects: 167,
    valueSar: '1500000.00',
    valueEntered: 167,
    measured: 97,
    averagePlannedPercent: 61.5,
    averageActualPercent: 54.25,
    gapPoints: -7.25,
    onPlan: 'LATE',
    needingFollowUp: 12,
    ...overrides,
  };
}

export function fullInsights(overrides: Partial<PortfolioInsights> = {}): PortfolioInsights {
  return {
    headline: {
      registeredProjects: 167,
      inProgressProjects: 120,
      totalValueSar: '1500000.00',
      valueEntered: 167,
      averagePlannedPercent: 61.5,
      averageActualPercent: 54.25,
      measured: 97,
      gapPoints: -7.25,
      onPlan: 'LATE',
      needingFollowUp: 12,
      overdueActions: 4,
    },
    byPortfolio: {
      rows: [breakdownRow()],
      total: breakdownRow({ itemId: null, projects: 200, measured: 99, needingFollowUp: 15 }),
    },
    byInitiative: {
      rows: [breakdownRow({ itemId: INITIATIVE_A, parentItemId: PORTFOLIO_A })],
      total: breakdownRow({ itemId: null }),
    },
    bySector: {
      rows: [
        breakdownRow({
          itemId: SECTOR_A,
          averagePlannedSpendPercent: null,
          averageActualSpendPercent: null,
          spendMeasured: 0,
          portfolioSplit: [{ portfolioItemId: PORTFOLIO_A, projects: 167 }],
        }),
      ],
      total: breakdownRow({ itemId: null, spendMeasured: 0 }),
    },
    stages: [{ status: 'ACTIVE', projects: 120, percent: 71.9 }],
    onPlan: { onTrack: 50, late: 30, stalled: 17, notMeasured: 70 },
    followUpReasons: {
      stalled: 5,
      suspended: 1,
      late: 3,
      overdueAction: 2,
      openObstacle: 1,
      total: 12,
    },
    topFollowUp: [
      {
        projectId: PROJECT_ID,
        formalProjectId: 'PRJ-2026-0042',
        legacyReference: null,
        title: { text: 'Coastal road upgrade', language: 'EN' },
        portfolioItemId: PORTFOLIO_A,
        reason: 'STALLED',
        currentObstacle: { text: 'Permit delay', language: 'EN' },
        nextAction: { text: 'Escalate the permit', language: 'EN' },
        nextActionOwner: 'Nora',
        nextActionDueDate: '2026-11-20',
      },
    ],
    ...overrides,
  };
}

export const NO_SECTIONS: PortfolioInsights = {
  headline: null,
  byPortfolio: null,
  byInitiative: null,
  bySector: null,
  stages: null,
  onPlan: null,
  followUpReasons: null,
  topFollowUp: null,
};

export function portfolioCard(): PortfolioCard {
  return {
    dimension: 'PORTFOLIO',
    itemId: PORTFOLIO_A,
    summary: breakdownRow(),
    projects: [
      {
        projectId: PROJECT_ID,
        formalProjectId: 'PRJ-2026-0042',
        legacyReference: null,
        title: { text: 'Coastal road upgrade', language: 'EN' },
        status: 'ACTIVE',
        departmentId: 'd1',
        externalEntityId: null,
        valueSar: '1500000.00',
        plannedStartDate: '2026-01-01',
        plannedEndDate: '2026-12-31',
        plannedPercent: null,
        actualPercent: null,
        gapPoints: null,
        onPlan: null,
        currentObstacle: null,
        nextAction: { text: 'Escalate the permit', language: 'EN' },
        nextActionOwner: null,
        nextActionDueDate: null,
      },
    ],
  };
}
