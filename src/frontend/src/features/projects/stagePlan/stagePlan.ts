import { type BilingualLabel } from '@/features/identity-access/api/types.ts';

import { type StagePlanEntryRequest, type StagePlanStage } from './stagePlanApi.ts';

// The stage plan as the form edits it, and the API's checks on it (ProjectStagePlanService.Invalid), so a mistake is caught before the round trip.
// Field codes are keyed by the request's paths, `stages[i].plannedStartDate`, which the API's field errors name too.

export interface StagePlanRow {
  stageCode: string;
  name: BilingualLabel;
  weight: number;
  applies: boolean;
  plannedStartDate: string;
  plannedEndDate: string;
  startingPercent: string;
}

export type StagePlanCodes = Partial<Record<string, string>>;

export const STARTING_PERCENT_PLACES = 2;
const NUMBER_PATTERN = /^[0-9]+(\.[0-9]+)?$/;

export function rowsOf(stages: readonly StagePlanStage[]): StagePlanRow[] {
  return [...stages]
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((stage) => ({
      stageCode: stage.stageCode,
      name: stage.name,
      weight: Number(stage.weightPercent),
      applies: stage.applies,
      plannedStartDate: stage.plannedStartDate ?? '',
      plannedEndDate: stage.plannedEndDate ?? '',
      startingPercent: stage.startingPercent === null ? '' : String(Number(stage.startingPercent)),
    }));
}

export function fieldOf(index: number, field: keyof StagePlanEntryRequest): string {
  return `stages[${String(index)}].${field}`;
}

interface PlanBounds {
  /** The project's planned dates, which every stage that applies falls within. */
  projectStart: string;
  projectEnd: string;
  /** Submitting the registration, and any change after approval, needs every applicable stage dated; a draft may wait. */
  requireDates: boolean;
  acceptsStartingPercent: boolean;
}

/** The code of each failed check, by request path; `stages` when no stage applies. */
export function checkStagePlan(rows: readonly StagePlanRow[], bounds: PlanBounds): StagePlanCodes {
  const codes: StagePlanCodes = {};
  rows.forEach((row, index) => {
    if (!row.applies) {
      return;
    }
    const start = row.plannedStartDate;
    const end = row.plannedEndDate;
    if (start === '') {
      if (bounds.requireDates) {
        codes[fieldOf(index, 'plannedStartDate')] = 'REQUIRED';
      }
    } else if (bounds.projectStart !== '' && start < bounds.projectStart) {
      codes[fieldOf(index, 'plannedStartDate')] = 'DATE_BEFORE_START';
    }
    if (end === '') {
      if (bounds.requireDates) {
        codes[fieldOf(index, 'plannedEndDate')] = 'REQUIRED';
      }
    } else if (
      (start !== '' && end < start) ||
      (bounds.projectEnd !== '' && end > bounds.projectEnd)
    ) {
      codes[fieldOf(index, 'plannedEndDate')] = 'OUTSIDE_RANGE';
    }
    const starting = row.startingPercent.trim();
    if (bounds.acceptsStartingPercent && starting !== '') {
      const places = starting.split('.')[1]?.length ?? 0;
      if (
        !NUMBER_PATTERN.test(starting) ||
        Number(starting) > 100 ||
        places > STARTING_PERCENT_PLACES
      ) {
        codes[fieldOf(index, 'startingPercent')] = 'OUT_OF_RANGE';
      }
    }
  });
  if (rows.length > 0 && !rows.some((row) => row.applies)) {
    codes.stages = 'NOT_ALLOWED';
  }
  return codes;
}

export function hasStagePlanErrors(codes: StagePlanCodes): boolean {
  return Object.values(codes).some((code) => code !== undefined);
}

/** The request's stages: a stage that does not apply sends no dates and no starting %. */
export function toEntries(
  rows: readonly StagePlanRow[],
  acceptsStartingPercent: boolean,
): StagePlanEntryRequest[] {
  return rows.map((row) => ({
    stageCode: row.stageCode,
    applies: row.applies,
    plannedStartDate: row.applies && row.plannedStartDate !== '' ? row.plannedStartDate : null,
    plannedEndDate: row.applies && row.plannedEndDate !== '' ? row.plannedEndDate : null,
    startingPercent:
      row.applies && acceptsStartingPercent && row.startingPercent.trim() !== ''
        ? Number(row.startingPercent.trim())
        : null,
  }));
}

/** True when the rows say something other than the saved plan, so saving them is a change. */
export function stagePlanChanged(
  rows: readonly StagePlanRow[],
  saved: readonly StagePlanStage[],
  acceptsStartingPercent: boolean,
): boolean {
  const before = JSON.stringify(toEntries(rowsOf(saved), acceptsStartingPercent));
  return JSON.stringify(toEntries(rows, acceptsStartingPercent)) !== before;
}
