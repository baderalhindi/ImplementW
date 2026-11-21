import { checkText } from '@/features/identity-access/forms.ts';
import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import { weightedOverall, currentStage } from '@/features/projects/stagePlan/stageProgress.ts';

import {
  type ProgressStageDetail,
  type ProgressStageRequest,
  type ProgressSubmissionDetail,
  type ProgressSubmissionRequest,
} from './api/types.ts';

// The DRAFT as MOD-020 and MOD-021 edit it, and the checks the API makes on it (ProgressSubmissionRequest.Validate,
// TASK-044 D-3), so a mistake is caught before the round trip. The API stays the authority: its field errors land on
// the same inputs, by the request's field paths.

/** The reported figure: the derived roll-up, or an override of it with a reason (ADR-009). */
export type FigureSource = 'calculated' | 'override';

export interface ProgressUpdateValues {
  narrative: string;
  figureSource: FigureSource;
  overridePercent: string;
  overrideReason: string;
  /** A stage-based period's stages that apply, as entered (review round 7, item 1); empty otherwise. */
  stages: StageEntryValues[];
}

export interface StageEntryValues {
  stageCode: string;
  percent: string;
  actualStartDate: string;
  actualFinishDate: string;
  note: string;
  decreaseReason: string;
}

/** ProgressStageRequest's places and note length. */
export const STAGE_PERCENT_PLACES = 2;
export const STAGE_NOTE_LENGTH = 200;

export function stageField(stageCode: string, field: keyof StageEntryValues): string {
  return `stage.${stageCode}.${field}`;
}

/** The request's field paths, which the API's field errors name. */
export const OVERRIDE_PERCENT_FIELD = 'override.actualPercent';
export const OVERRIDE_REASON_FIELD = 'override.reason';
export const NARRATIVE_FIELD = 'narrative';

export type ProgressUpdateCodes = Partial<Record<string, string | null>>;

/** ProgressOverrideRequest.Places. */
export const PERCENT_PLACES = 4;
const NUMBER_PATTERN = /^-?[0-9]+(\.[0-9]+)?$/;

/**
 * The API code of a percentage as typed: REQUIRED, MALFORMED when it is not a number, OUT_OF_RANGE when it is below
 * 0, above 100 or has more than four decimal places — the server's rule, so both sides refuse the same values.
 */
export function checkPercent(value: string, places: number = PERCENT_PLACES): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return 'REQUIRED';
  }
  if (!NUMBER_PATTERN.test(trimmed)) {
    return 'MALFORMED';
  }
  const decimals = trimmed.split('.')[1]?.length ?? 0;
  const percent = Number(trimmed);
  return percent < 0 || percent > 100 || decimals > places ? 'OUT_OF_RANGE' : null;
}

/**
 * The code of each field's first failed check, keyed by the request's field path, or null. A stage's checks are the API's at submission
 * (ProgressStages.SubmitAsync): its %, its actual start once it has started, its finish once complete, a reason below the last published %.
 */
export function checkProgressUpdate(
  values: ProgressUpdateValues,
  stages: readonly ProgressStageDetail[] = [],
): ProgressUpdateCodes {
  const overriding = values.figureSource === 'override';
  const codes: ProgressUpdateCodes = {
    [NARRATIVE_FIELD]: checkText(values.narrative, { maxLength: TEXT_LENGTH }),
    [OVERRIDE_PERCENT_FIELD]: overriding ? checkPercent(values.overridePercent) : null,
    [OVERRIDE_REASON_FIELD]: overriding
      ? checkText(values.overrideReason, { required: true, maxLength: TEXT_LENGTH })
      : null,
  };
  for (const entry of values.stages) {
    const stage = stages.find((s) => s.stageCode === entry.stageCode);
    if (stage === undefined) {
      continue;
    }
    const percentCode = stage.isFromSchedule
      ? null
      : checkPercent(entry.percent, STAGE_PERCENT_PLACES);
    codes[stageField(entry.stageCode, 'percent')] = percentCode;
    const percent = percentCode === null ? percentOf(entry, stage) : null;
    codes[stageField(entry.stageCode, 'actualStartDate')] =
      percent !== null && percent > 0 && entry.actualStartDate === '' ? 'REQUIRED' : null;
    codes[stageField(entry.stageCode, 'actualFinishDate')] =
      percent === 100 && entry.actualFinishDate === ''
        ? 'REQUIRED'
        : entry.actualFinishDate !== '' &&
            entry.actualStartDate !== '' &&
            entry.actualFinishDate < entry.actualStartDate
          ? 'DATE_BEFORE_START'
          : entry.actualFinishDate !== '' && percent !== 100
            ? 'NOT_ALLOWED'
            : null;
    codes[stageField(entry.stageCode, 'decreaseReason')] = isDecrease(entry, stage)
      ? checkText(entry.decreaseReason, { required: true, maxLength: TEXT_LENGTH })
      : null;
    codes[stageField(entry.stageCode, 'note')] = /[\r\n]/.test(entry.note)
      ? 'NOT_ALLOWED'
      : checkText(entry.note, { maxLength: STAGE_NOTE_LENGTH });
  }
  return codes;
}

/** The stage's % as entered, or the schedule's; null when not a number yet. */
export function percentOf(entry: StageEntryValues, stage: ProgressStageDetail): number | null {
  if (stage.isFromSchedule) {
    return stage.percentComplete === null ? null : Number(stage.percentComplete);
  }
  const trimmed = entry.percent.trim();
  return trimmed === '' || !NUMBER_PATTERN.test(trimmed) ? null : Number(trimmed);
}

/** Lower than the last published period's: it needs a reason. */
export function isDecrease(entry: StageEntryValues, stage: ProgressStageDetail): boolean {
  const percent = percentOf(entry, stage);
  return (
    percent !== null && stage.previousPercent !== null && percent < Number(stage.previousPercent)
  );
}

/** The period's overall % and current stage as entered so far, weighed as the API weighs them; null while a % is missing. */
export function liveFigures(
  values: ProgressUpdateValues,
  stages: readonly ProgressStageDetail[],
): { actual: number | null; planned: number | null; current: ProgressStageDetail | null } {
  const applicable = [...stages].filter((s) => s.applies).sort((a, b) => a.sortOrder - b.sortOrder);
  const entered = applicable.map((stage) => {
    const entry = values.stages.find((e) => e.stageCode === stage.stageCode);
    return { stage, percent: entry === undefined ? null : percentOf(entry, stage) };
  });
  return {
    actual: weightedOverall(
      entered.map((e) => ({ weight: Number(e.stage.weightPercent), percent: e.percent ?? 0 })),
    ),
    planned: weightedOverall(
      applicable.map((s) => ({
        weight: Number(s.weightPercent),
        percent: s.plannedPercent === null ? null : Number(s.plannedPercent),
      })),
    ),
    current: currentStage(entered, (e) => e.percent ?? 0)?.stage ?? null,
  };
}

export function hasErrors(codes: ProgressUpdateCodes): boolean {
  return Object.values(codes).some((code) => typeof code === 'string');
}

/** The form starts from the DRAFT, which the API pre-filled from the last published revision (ADR-017). */
export function valuesOf(submission: ProgressSubmissionDetail): ProgressUpdateValues {
  return {
    narrative: submission.narrative?.text ?? '',
    figureSource:
      submission.isOverridden || submission.actualSource === 'NOT_AVAILABLE'
        ? 'override'
        : 'calculated',
    overridePercent:
      submission.actualPercentOverride === null ? '' : String(submission.actualPercentOverride),
    overrideReason: submission.overrideReason?.text ?? '',
    stages: (submission.stages ?? [])
      .filter((stage) => stage.applies)
      .map((stage) => ({
        stageCode: stage.stageCode,
        percent: stage.percentComplete === null ? '' : String(Number(stage.percentComplete)),
        actualStartDate: stage.actualStartDate ?? '',
        actualFinishDate: stage.actualFinishDate ?? '',
        note: stage.note ?? '',
        decreaseReason: stage.decreaseReason?.text ?? '',
      })),
  };
}

/** The API writes a text's language as `EN`/`AR` and reads it as `en`/`ar`. */
function languageOf(tag: string): Language {
  return tag.toLowerCase() === 'ar' ? 'ar' : 'en';
}

/**
 * A text in the language it was entered in (ERD D-7): the interface language for new or changed text, the original
 * tag for text left as it was.
 */
export function narrativeRequest(
  text: string,
  language: Language,
  before: NarrativeText | null = null,
): NarrativeTextRequest {
  const trimmed = text.trim();
  const unchanged = before !== null && before.text === trimmed;
  return { text: trimmed, language: unchanged ? languageOf(before.language) : language };
}

/** The request for valid values. Choosing the calculated figure clears the override and its reason. */
export function toRequest(
  values: ProgressUpdateValues,
  language: Language,
  existing: ProgressSubmissionDetail,
): ProgressSubmissionRequest {
  return {
    narrative:
      values.narrative.trim() === ''
        ? null
        : narrativeRequest(values.narrative, language, existing.narrative),
    override:
      values.figureSource === 'override'
        ? {
            actualPercent: Number(values.overridePercent.trim()),
            reason: narrativeRequest(values.overrideReason, language, existing.overrideReason),
          }
        : null,
    ...(values.stages.length === 0
      ? {}
      : { stages: values.stages.map((entry) => stageRequest(entry, language, existing)) }),
  };
}

function stageRequest(
  entry: StageEntryValues,
  language: Language,
  existing: ProgressSubmissionDetail,
): ProgressStageRequest {
  const stage = existing.stages?.find((s) => s.stageCode === entry.stageCode);
  const decrease = stage !== undefined && isDecrease(entry, stage);
  return {
    stageCode: entry.stageCode,
    percentComplete:
      stage?.isFromSchedule === true || entry.percent.trim() === ''
        ? null
        : Number(entry.percent.trim()),
    actualStartDate: entry.actualStartDate === '' ? null : entry.actualStartDate,
    actualFinishDate: entry.actualFinishDate === '' ? null : entry.actualFinishDate,
    note: entry.note.trim() === '' ? null : entry.note.trim(),
    decreaseReason:
      decrease && entry.decreaseReason.trim() !== ''
        ? narrativeRequest(entry.decreaseReason, language, stage.decreaseReason)
        : null,
  };
}

/** True when the form says something other than what the DRAFT already holds, so saving it is a change. */
export function differsFrom(
  values: ProgressUpdateValues,
  submission: ProgressSubmissionDetail,
): boolean {
  const before = valuesOf(submission);
  const overriding = values.figureSource === 'override';
  return (
    JSON.stringify(values.stages) !== JSON.stringify(before.stages) ||
    values.narrative.trim() !== before.narrative.trim() ||
    values.figureSource !== before.figureSource ||
    (overriding &&
      (Number(values.overridePercent.trim()) !== Number(before.overridePercent) ||
        values.overrideReason.trim() !== before.overrideReason.trim()))
  );
}
