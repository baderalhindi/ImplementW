import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { type ExternalUpdateRequestDetail } from './api/types.ts';

// How WF-13's codes read: a schema field, a request's purpose, an attempt's safe failure. A code the platform does not
// name is shown as it is.

/** The schema fields the platform names (`ContributionSchemas`); any other code is shown as it is. */
const FIELD_LABELS: Partial<Record<string, TranslationKey>> = {
  actualPercentComplete: 'externalParticipation.fields.actualPercentComplete',
  progressNote: 'externalParticipation.fields.progressNote',
  response: 'externalParticipation.fields.response',
  asOfDate: 'externalParticipation.fields.asOfDate',
};

export function useFieldLabel(): (fieldCode: string) => string {
  const { t } = useI18n();
  return (fieldCode) => {
    const key = FIELD_LABELS[fieldCode];
    return key === undefined ? fieldCode : t(key);
  };
}

/** What the request is for, in the platform's words for its schema; an unknown code as it is. */
export function usePurpose(): (
  request: Pick<ExternalUpdateRequestDetail, 'contributionSchemaCode'>,
) => string {
  const { t } = useI18n();
  return ({ contributionSchemaCode: code }) =>
    code === 'TASK_PROGRESS' || code === 'PROJECT_INFORMATION'
      ? t(`externalParticipation.schema.${code}`)
      : code;
}

/** The safe failure codes an attempt records (EXT-F-156), in words; any other code is shown as it is. */
const FAILURES: Partial<Record<string, TranslationKey>> = {
  SOURCE_RECORD_NOT_FOUND: 'externalParticipation.failure.SOURCE_RECORD_NOT_FOUND',
  SOURCE_RECORD_TERMINAL: 'externalParticipation.failure.SOURCE_RECORD_TERMINAL',
  SOURCE_RECORD_STATE_INVALID: 'externalParticipation.failure.SOURCE_RECORD_STATE_INVALID',
  PROJECT_STATE_NOT_PERMITTED: 'externalParticipation.failure.PROJECT_STATE_NOT_PERMITTED',
};

export function useFailureText(): (code: string) => string {
  const { t } = useI18n();
  return (code) => {
    const key = FAILURES[code];
    return key === undefined ? code : t(key);
  };
}
