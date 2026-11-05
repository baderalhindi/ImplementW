import {
  type FollowUpReason,
  type NarrativeText,
  type OnPlanStatus,
  type ProjectStatus,
} from '@/features/projects/api/types.ts';

// What the Excel's dashboard answers (the 5 Nov 2026 review §3). A section is null when the caller holds no permission for it. A value is null
// when the caller may not see amounts; an average is null when nothing is measured: never zero.

export interface PortfolioHeadline {
  registeredProjects: number;
  inProgressProjects: number;
  totalValueSar: string | null;
  valueEntered: number;
  averagePlannedPercent: number | null;
  averageActualPercent: number | null;
  measured: number;
  gapPoints: number | null;
  onPlan: OnPlanStatus | null;
  needingFollowUp: number;
  overdueActions: number;
}

export interface PortfolioSplit {
  portfolioItemId: string | null;
  projects: number;
}

export interface PortfolioBreakdownRow {
  itemId: string | null;
  parentItemId: string | null;
  projects: number;
  valueSar: string | null;
  valueEntered: number;
  measured: number;
  averagePlannedPercent: number | null;
  averageActualPercent: number | null;
  gapPoints: number | null;
  onPlan: OnPlanStatus | null;
  needingFollowUp: number;
  averagePlannedSpendPercent?: number | null;
  averageActualSpendPercent?: number | null;
  spendMeasured?: number;
  portfolioSplit?: PortfolioSplit[] | null;
}

export interface PortfolioBreakdown {
  rows: PortfolioBreakdownRow[];
  total: PortfolioBreakdownRow;
}

export interface PortfolioStageCount {
  status: ProjectStatus;
  projects: number;
  percent: number;
}

export interface PortfolioOnPlan {
  onTrack: number;
  late: number;
  stalled: number;
  notMeasured: number;
}

export interface PortfolioFollowUpReasons {
  stalled: number;
  suspended: number;
  late: number;
  overdueAction: number;
  openObstacle: number;
  total: number;
}

export interface PortfolioTopProject {
  projectId: string;
  formalProjectId: string | null;
  legacyReference: string | null;
  title: NarrativeText;
  portfolioItemId: string | null;
  reason: FollowUpReason;
  currentObstacle: NarrativeText | null;
  nextAction: NarrativeText | null;
  nextActionOwner: string | null;
  nextActionDueDate: string | null;
}

export interface PortfolioInsights {
  headline: PortfolioHeadline | null;
  byPortfolio: PortfolioBreakdown | null;
  byInitiative: PortfolioBreakdown | null;
  bySector: PortfolioBreakdown | null;
  stages: PortfolioStageCount[] | null;
  onPlan: PortfolioOnPlan | null;
  followUpReasons: PortfolioFollowUpReasons | null;
  topFollowUp: PortfolioTopProject[] | null;
}

export type PortfolioCardDimension = 'PORTFOLIO' | 'INITIATIVE' | 'SECTOR';

export interface PortfolioCardProject {
  projectId: string;
  formalProjectId: string | null;
  legacyReference: string | null;
  title: NarrativeText;
  status: ProjectStatus;
  departmentId: string;
  externalEntityId: string | null;
  valueSar: string | null;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  plannedPercent: number | null;
  actualPercent: number | null;
  gapPoints: number | null;
  onPlan: OnPlanStatus | null;
  currentObstacle: NarrativeText | null;
  nextAction: NarrativeText | null;
  nextActionOwner: string | null;
  nextActionDueDate: string | null;
}

export interface PortfolioCard {
  dimension: PortfolioCardDimension;
  itemId: string;
  summary: PortfolioBreakdownRow;
  projects: PortfolioCardProject[];
}
