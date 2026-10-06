import { checkText } from '@/features/identity-access/forms.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import {
  type BilingualLabel,
  type ImpactLevelEntry,
  type ProbabilityLevelEntry,
  type RiskAcceptCommand,
  type RiskAssessCommand,
  type RiskCloseCommand,
  type RiskDetail,
  type RiskMatrixResolution,
  type RiskRatingEntry,
  type RiskRequest,
  type RiskStatus,
  type RiskTreatmentActionDetail,
  type RiskTreatmentActionRequest,
  type RiskTreatmentActionType,
} from './api/types.ts';

// The rules the risk screens apply before anything is sent, and how they read the published matrix. Each mirrors the
// API (TASK-055 §2–§5), which stays the authority: whatever it refuses is shown as it comes. Nothing here rates a risk:
// a rating is the server's, from the matrix version its assessment pinned (D-5); the matrix is only drawn and previewed.

/** A risk and the project it belongs to, as SCR-080 and SCR-081 list them. */
export interface RiskEntry {
  risk: RiskDetail;
  project: ProjectSummary;
}

export function isOpen(risk: Pick<RiskDetail, 'status'>): boolean {
  return risk.status !== 'CLOSED';
}

/** An open risk whose next review date has come (UTC, TASK-055 F-17). */
export function isReviewDue(
  risk: Pick<RiskDetail, 'status' | 'nextReviewDate'>,
  today: string,
): boolean {
  return isOpen(risk) && risk.nextReviewDate !== null && risk.nextReviewDate <= today;
}

// ---------------------------------------------------------------------------------------------------------------------
// The matrix

export interface ImpactDimension {
  /** An IMPACT_DIMENSION master data item. */
  id: string;
  /** The levels this version defines for the dimension, lowest first. */
  levels: ImpactLevelEntry[];
}

/** The RISK_MATRIX version in force, arranged to be drawn: every axis and rating is the version's own. */
export interface RiskMatrix {
  versionId: string;
  versionNo: number;
  effectiveFrom: string;
  /** Lowest first. */
  probabilityLevels: ProbabilityLevelEntry[];
  /** The overall impact levels, lowest first: every level any dimension defines. */
  impactLevels: number[];
  dimensions: ImpactDimension[];
  /** Least severe first: the version's sort order (RiskRatingDefinition: "Low, Medium, High, Critical"). */
  ratings: RiskRatingEntry[];
  /** `probability:impact` → rating code. */
  cells: Map<string, string>;
}

function cellKey(probabilityLevel: number, impactLevel: number): string {
  return `${String(probabilityLevel)}:${String(impactLevel)}`;
}

export function riskMatrixOf(resolution: RiskMatrixResolution): RiskMatrix {
  const { probabilityLevels, impactLevels, riskRatings, riskMatrixCells } = resolution.content;
  const dimensions: ImpactDimension[] = [];
  for (const entry of impactLevels) {
    const dimension = dimensions.find((candidate) => candidate.id === entry.impactDimensionItemId);
    if (dimension === undefined) {
      dimensions.push({ id: entry.impactDimensionItemId, levels: [entry] });
    } else {
      dimension.levels.push(entry);
    }
  }
  for (const dimension of dimensions) {
    dimension.levels.sort((a, b) => a.level - b.level);
  }
  return {
    versionId: resolution.versionId,
    versionNo: resolution.versionNo,
    effectiveFrom: resolution.effectiveFrom,
    probabilityLevels: [...probabilityLevels].sort((a, b) => a.level - b.level),
    impactLevels: [...new Set(impactLevels.map((entry) => entry.level))].sort((a, b) => a - b),
    dimensions,
    ratings: [...riskRatings].sort((a, b) => a.sortOrder - b.sortOrder),
    cells: new Map(
      riskMatrixCells.map((cell) => [
        cellKey(cell.probabilityLevel, cell.impactLevel),
        cell.ratingCode,
      ]),
    ),
  };
}

/** The rating the version maps a probability and an overall impact to; null for a cell it leaves unmapped. */
export function ratingAt(
  matrix: RiskMatrix,
  probabilityLevel: number,
  impactLevel: number,
): RiskRatingEntry | null {
  const code = matrix.cells.get(cellKey(probabilityLevel, impactLevel));
  return matrix.ratings.find((rating) => rating.code === code) ?? null;
}

/** The number of colour steps the heat-map has, least to most severe. */
export const SEVERITY_STEPS = 5;

/**
 * Where a rating sits among the version's, as one of SEVERITY_STEPS colour steps (1 least severe). The version has no
 * colours of its own (TBC-RSK-005), so the colour follows the published order; the label always says the rating.
 */
export function severityStep(matrix: RiskMatrix, code: string): number | null {
  const rank = matrix.ratings.findIndex((rating) => rating.code === code);
  if (rank === -1) {
    return null;
  }
  const count = matrix.ratings.length;
  return count === 1 ? SEVERITY_STEPS : 1 + Math.round((rank * (SEVERITY_STEPS - 1)) / (count - 1));
}

/**
 * The rating SCR-081 lists: the version's most severe one. No high/critical threshold is configured yet (TBC-RSK-005,
 * D-4), so "critical" is the top of the published order, never a code the screen knows.
 */
export function criticalRating(matrix: RiskMatrix): RiskRatingEntry | null {
  return matrix.ratings.at(-1) ?? null;
}

/** An open risk whose latest assessment carries the version's most severe rating. */
export function isCritical(risk: RiskDetail, matrix: RiskMatrix): boolean {
  const critical = criticalRating(matrix);
  return critical !== null && isOpen(risk) && risk.currentAssessment?.rating.code === critical.code;
}

/** The overall impact the server works out (D-4): the highest dimension level. Null until every level is chosen. */
export function overallImpact(levels: (number | null)[]): number | null {
  if (levels.length === 0 || levels.some((level) => level === null)) {
    return null;
  }
  return Math.max(...levels.filter((level) => level !== null));
}

/** The register's order: most severe first, then the earliest review; unassessed after rated, closed last. */
export function byExposure(matrix: RiskMatrix | null) {
  const rank = (risk: RiskDetail) => {
    if (!isOpen(risk)) {
      return -2;
    }
    const code = risk.currentAssessment?.rating.code;
    if (code === undefined) {
      return -1;
    }
    return matrix === null ? 0 : matrix.ratings.findIndex((rating) => rating.code === code);
  };
  return (a: RiskEntry, b: RiskEntry): number =>
    rank(b.risk) - rank(a.risk) ||
    (a.risk.nextReviewDate ?? '9999-12-31').localeCompare(b.risk.nextReviewDate ?? '9999-12-31');
}

// ---------------------------------------------------------------------------------------------------------------------
// The register's views and the dashboard's inputs

export const REGISTER_VIEWS = ['open', 'reviewDue', 'unassessed', 'closed', 'all'] as const;
export type RegisterView = (typeof REGISTER_VIEWS)[number];

export function matchesView(risk: RiskDetail, view: RegisterView, today: string): boolean {
  switch (view) {
    case 'open':
      return isOpen(risk);
    case 'reviewDue':
      return isReviewDue(risk, today);
    case 'unassessed':
      return risk.status === 'IDENTIFIED';
    case 'closed':
      return risk.status === 'CLOSED';
    case 'all':
      return true;
  }
}

/** A rating filter's value: any rating, or a rating code of the version. */
export const ANY_RATING = '';

export function matchesRating(risk: RiskDetail, ratingCode: string): boolean {
  return ratingCode === ANY_RATING || risk.currentAssessment?.rating.code === ratingCode;
}

export interface RatingCount {
  code: string;
  label: BilingualLabel;
  count: number;
}

/**
 * What the Risk Dashboard's widgets compose (WF-06 §13.2; FG-01 DSH-010, built by TASK-070): counts over the open risks
 * a caller may see, by the ratings the server gave them. Nothing is re-rated (BR-DSH-004). Rating-based figures need
 * the matrix in force; without it they are null, never zero.
 */
export interface RiskSummary {
  open: number;
  unassessed: number;
  reviewDue: number;
  /** Open risks with an ACTIVE acceptance. */
  accepted: number;
  byStatus: Record<RiskStatus, number>;
  /** Least severe first, the version's ratings then any older rating still carried by a risk. */
  byRating: RatingCount[];
  /** Null when the matrix in force cannot be read. */
  critical: number | null;
  /** Open, assessed risks per `probability:impact` cell. */
  cells: Map<string, number>;
}

export function riskSummary(
  risks: RiskDetail[],
  matrix: RiskMatrix | null,
  today: string,
): RiskSummary {
  const byStatus: Record<RiskStatus, number> = {
    IDENTIFIED: 0,
    ASSESSED: 0,
    TREATMENT: 0,
    MONITORING: 0,
    CLOSED: 0,
  };
  const byRating: RatingCount[] = (matrix?.ratings ?? []).map((rating) => ({
    code: rating.code,
    label: rating.label,
    count: 0,
  }));
  const cells = new Map<string, number>();
  for (const risk of risks) {
    byStatus[risk.status] += 1;
    const assessment = risk.currentAssessment;
    if (!isOpen(risk) || assessment === null) {
      continue;
    }
    const key = cellKey(assessment.probabilityLevel, assessment.overallImpactLevel);
    cells.set(key, (cells.get(key) ?? 0) + 1);
    const counted = byRating.find((entry) => entry.code === assessment.rating.code);
    if (counted === undefined) {
      byRating.push({ code: assessment.rating.code, label: assessment.rating.label, count: 1 });
    } else {
      counted.count += 1;
    }
  }
  const open = risks.filter(isOpen);
  return {
    open: open.length,
    unassessed: byStatus.IDENTIFIED,
    reviewDue: open.filter((risk) => isReviewDue(risk, today)).length,
    accepted: open.filter((risk) => risk.acceptedUntil !== null).length,
    byStatus,
    byRating,
    critical: matrix === null ? null : open.filter((risk) => isCritical(risk, matrix)).length,
    cells,
  };
}

export function cellCount(summary: RiskSummary, probabilityLevel: number, impactLevel: number) {
  return summary.cells.get(cellKey(probabilityLevel, impactLevel)) ?? 0;
}

// ---------------------------------------------------------------------------------------------------------------------
// MOD-030 Create Risk, MOD-031 Edit Risk, MOD-033 Assign Risk Owner

export interface RiskFormValues {
  title: string;
  description: string;
  riskCategoryItemId: string;
  identifiedDate: string;
  nextReviewDate: string;
}

export function emptyRiskForm(today: string): RiskFormValues {
  return {
    title: '',
    description: '',
    riskCategoryItemId: '',
    identifiedDate: today,
    nextReviewDate: '',
  };
}

export function riskFormValuesOf(risk: RiskDetail): RiskFormValues {
  return {
    title: risk.title.text,
    description: risk.description.text,
    riskCategoryItemId: risk.riskCategoryItemId,
    identifiedDate: risk.identifiedDate,
    nextReviewDate: risk.nextReviewDate ?? '',
  };
}

/** The API's checks (RiskRequest): title, description, category and identified date; no review before identification. */
export function checkRiskForm(
  values: RiskFormValues,
  today: string,
): Record<string, string | null> {
  return {
    title: checkText(values.title, { required: true, maxLength: TEXT_LENGTH }),
    description: checkText(values.description, { required: true, maxLength: TEXT_LENGTH }),
    riskCategoryItemId: values.riskCategoryItemId === '' ? 'REQUIRED' : null,
    identifiedDate:
      values.identifiedDate === ''
        ? 'REQUIRED'
        : values.identifiedDate > today
          ? 'DATE_IN_FUTURE'
          : null,
    nextReviewDate:
      values.nextReviewDate !== '' &&
      values.identifiedDate !== '' &&
      values.nextReviewDate < values.identifiedDate
        ? 'DATE_BEFORE_START'
        : null,
  };
}

export function toRiskRequest(
  values: RiskFormValues,
  ownerUserId: string | null,
  language: Language,
  before: RiskDetail | null,
): RiskRequest {
  return {
    title: narrativeRequest(values.title, language, before?.title ?? null),
    description: narrativeRequest(values.description, language, before?.description ?? null),
    riskCategoryItemId: values.riskCategoryItemId,
    ownerUserId,
    identifiedDate: values.identifiedDate,
    nextReviewDate: values.nextReviewDate === '' ? null : values.nextReviewDate,
  };
}

/** MOD-033: the register fields re-sent as they are (R-5), with another owner. */
export function withOwner(risk: RiskDetail, ownerUserId: string | null, language: Language) {
  return toRiskRequest(riskFormValuesOf(risk), ownerUserId, language, risk);
}

// ---------------------------------------------------------------------------------------------------------------------
// MOD-032 Risk Assessment

export interface AssessmentValues {
  probabilityLevel: string;
  /** Impact dimension id → level, '' while unchosen. */
  impacts: Record<string, string>;
  rationale: string;
}

/** The latest assessment's levels where they still fit the version, so a reassessment starts from them. */
export function assessmentValuesOf(
  matrix: RiskMatrix,
  previous: {
    probabilityLevel: number;
    impacts: { impactDimensionItemId: string; impactLevel: number }[];
  } | null,
): AssessmentValues {
  const fits = (levels: number[], level: number | undefined) =>
    level !== undefined && levels.includes(level) ? String(level) : '';
  return {
    probabilityLevel: fits(
      matrix.probabilityLevels.map((entry) => entry.level),
      previous?.probabilityLevel,
    ),
    impacts: Object.fromEntries(
      matrix.dimensions.map((dimension) => [
        dimension.id,
        fits(
          dimension.levels.map((entry) => entry.level),
          previous?.impacts.find((impact) => impact.impactDimensionItemId === dimension.id)
            ?.impactLevel,
        ),
      ]),
    ),
    rationale: '',
  };
}

/** A probability and one level for every dimension the version defines (the server's RISK_IMPACT_INVALID rule). */
export function checkAssessment(
  values: AssessmentValues,
  matrix: RiskMatrix,
): Record<string, string | null> {
  return {
    probabilityLevel: values.probabilityLevel === '' ? 'REQUIRED' : null,
    ...Object.fromEntries(
      matrix.dimensions.map((dimension) => [
        `impact-${dimension.id}`,
        (values.impacts[dimension.id] ?? '') === '' ? 'REQUIRED' : null,
      ]),
    ),
    rationale: checkText(values.rationale, { maxLength: TEXT_LENGTH }),
  };
}

/** The levels as chosen, for the preview; null for one not chosen yet. */
export function chosenLevels(values: AssessmentValues, matrix: RiskMatrix) {
  const level = (value: string | undefined) =>
    value === undefined || value === '' ? null : Number(value);
  return {
    probability: level(values.probabilityLevel),
    impacts: matrix.dimensions.map((dimension) => level(values.impacts[dimension.id])),
  };
}

export function toAssessCommand(
  values: AssessmentValues,
  matrix: RiskMatrix,
  language: Language,
): RiskAssessCommand {
  return {
    probabilityLevel: Number(values.probabilityLevel),
    impacts: matrix.dimensions.map((dimension) => ({
      impactDimensionItemId: dimension.id,
      impactLevel: Number(values.impacts[dimension.id]),
      rationale: null,
    })),
    rationale: values.rationale.trim() === '' ? null : narrativeRequest(values.rationale, language),
  };
}

// ---------------------------------------------------------------------------------------------------------------------
// MOD-035 Close Risk and accepting a risk

/** MOD-035's acceptance criterion: a closure rationale that is not blank, as the API requires (400 REQUIRED). */
export function checkClosure(rationale: string): Record<string, string | null> {
  return { rationale: checkText(rationale, { required: true, maxLength: TEXT_LENGTH }) };
}

export function toCloseCommand(rationale: string, language: Language): RiskCloseCommand {
  return { rationale: narrativeRequest(rationale, language) };
}

export interface AcceptanceValues {
  expiresOn: string;
  rationale: string;
}

/** An expiry after today (UTC; else 422 RISK_ACCEPTANCE_EXPIRY_INVALID) and a rationale. */
export function checkAcceptance(
  values: AcceptanceValues,
  today: string,
): Record<string, string | null> {
  return {
    expiresOn:
      values.expiresOn === ''
        ? 'REQUIRED'
        : values.expiresOn <= today
          ? 'EXPIRY_NOT_AFTER_TODAY'
          : null,
    rationale: checkText(values.rationale, { required: true, maxLength: TEXT_LENGTH }),
  };
}

export function toAcceptCommand(values: AcceptanceValues, language: Language): RiskAcceptCommand {
  return { expiresOn: values.expiresOn, rationale: narrativeRequest(values.rationale, language) };
}

// ---------------------------------------------------------------------------------------------------------------------
// MOD-034 Add Mitigation / Response

export const ACTION_TYPES: RiskTreatmentActionType[] = [
  'MITIGATE',
  'AVOID',
  'TRANSFER',
  'CONTINGENCY',
];

export interface ActionFormValues {
  title: string;
  description: string;
  actionType: string;
  dueDate: string;
}

export const EMPTY_ACTION_FORM: ActionFormValues = {
  title: '',
  description: '',
  actionType: 'MITIGATE',
  dueDate: '',
};

export function actionFormValuesOf(action: RiskTreatmentActionDetail): ActionFormValues {
  return {
    title: action.title.text,
    description: action.description?.text ?? '',
    actionType: action.actionType,
    dueDate: action.dueDate ?? '',
  };
}

function isActionType(value: string): value is RiskTreatmentActionType {
  return (ACTION_TYPES as string[]).includes(value);
}

export function checkActionForm(values: ActionFormValues): Record<string, string | null> {
  return {
    title: checkText(values.title, { required: true, maxLength: TEXT_LENGTH }),
    description: checkText(values.description, { maxLength: TEXT_LENGTH }),
    actionType: isActionType(values.actionType) ? null : 'REQUIRED',
  };
}

export function toActionRequest(
  values: ActionFormValues,
  ownerUserId: string | null,
  language: Language,
  before: RiskTreatmentActionDetail | null,
): RiskTreatmentActionRequest {
  return {
    title: narrativeRequest(values.title, language, before?.title ?? null),
    description:
      values.description.trim() === ''
        ? null
        : narrativeRequest(values.description, language, before?.description ?? null),
    actionType: isActionType(values.actionType) ? values.actionType : 'MITIGATE',
    ownerUserId,
    dueDate: values.dueDate === '' ? null : values.dueDate,
  };
}

/** A PLANNED or IN_PROGRESS action: what `start-treatment` needs at least one of (RISK_TREATMENT_ACTION_REQUIRED). */
export function isLiveAction(action: Pick<RiskTreatmentActionDetail, 'status'>): boolean {
  return action.status === 'PLANNED' || action.status === 'IN_PROGRESS';
}
