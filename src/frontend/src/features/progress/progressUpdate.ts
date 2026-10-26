import { checkText } from '@/features/identity-access/forms.ts';
import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import { type ProgressSubmissionDetail, type ProgressSubmissionRequest } from './api/types.ts';

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
export function checkPercent(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return 'REQUIRED';
  }
  if (!NUMBER_PATTERN.test(trimmed)) {
    return 'MALFORMED';
  }
  const places = trimmed.split('.')[1]?.length ?? 0;
  const percent = Number(trimmed);
  return percent < 0 || percent > 100 || places > PERCENT_PLACES ? 'OUT_OF_RANGE' : null;
}

/** The code of each field's first failed check, keyed by the request's field path, or null. */
export function checkProgressUpdate(values: ProgressUpdateValues): ProgressUpdateCodes {
  const overriding = values.figureSource === 'override';
  return {
    [NARRATIVE_FIELD]: checkText(values.narrative, { maxLength: TEXT_LENGTH }),
    [OVERRIDE_PERCENT_FIELD]: overriding ? checkPercent(values.overridePercent) : null,
    [OVERRIDE_REASON_FIELD]: overriding
      ? checkText(values.overrideReason, { required: true, maxLength: TEXT_LENGTH })
      : null,
  };
}

export function hasErrors(codes: ProgressUpdateCodes): boolean {
  return Object.values(codes).some((code) => typeof code === 'string');
}

/** The form starts from the DRAFT, which the API pre-filled from the last published revision (ADR-017). */
export function valuesOf(submission: ProgressSubmissionDetail): ProgressUpdateValues {
  return {
    narrative: submission.narrative?.text ?? '',
    figureSource: submission.isOverridden ? 'override' : 'calculated',
    overridePercent:
      submission.actualPercentOverride === null ? '' : String(submission.actualPercentOverride),
    overrideReason: submission.overrideReason?.text ?? '',
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
    values.narrative.trim() !== before.narrative.trim() ||
    values.figureSource !== before.figureSource ||
    (overriding &&
      (Number(values.overridePercent.trim()) !== Number(before.overridePercent) ||
        values.overrideReason.trim() !== before.overrideReason.trim()))
  );
}
