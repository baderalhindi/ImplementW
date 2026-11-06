import { type ContributionFieldDefinition, type ContributionFieldRequest } from '../api/types.ts';
import { type MyUpdateRequest } from '../api/monthlyUpdateTypes.ts';
import { responseFieldsToSend } from '../rules.ts';

/** The MONTHLY_UPDATE schema (`ContributionSchemas`), in its order; `/my-update-requests` serves values, not the definitions. */
export const MONTHLY_FIELDS: readonly ContributionFieldDefinition[] = [
  {
    fieldCode: 'actualProgressPercent',
    fieldType: 'NUMBER',
    required: true,
    minimum: 0,
    maximum: 100,
  },
  {
    fieldCode: 'actualSpendPercent',
    fieldType: 'NUMBER',
    required: false,
    minimum: 0,
    maximum: 100,
  },
  {
    fieldCode: 'currentObstacle',
    fieldType: 'NARRATIVE',
    required: false,
    minimum: null,
    maximum: null,
  },
  {
    fieldCode: 'nextAction',
    fieldType: 'NARRATIVE',
    required: false,
    minimum: null,
    maximum: null,
  },
  {
    fieldCode: 'nextActionDueDate',
    fieldType: 'DATE',
    required: false,
    minimum: null,
    maximum: null,
  },
  { fieldCode: 'note', fieldType: 'NARRATIVE', required: false, minimum: null, maximum: null },
];

/** The values as the API takes them: a narrative left as pre-filled keeps its language, one typed here takes the interface's. */
export function monthlyFieldsToSend(
  request: MyUpdateRequest,
  values: Readonly<Record<string, string>>,
  language: 'ar' | 'en',
): ContributionFieldRequest[] {
  return responseFieldsToSend(MONTHLY_FIELDS, values, language).map((field) => {
    const prefilled = request.prefillLanguages[field.fieldCode];
    return field.language !== null &&
      prefilled !== undefined &&
      field.value === (request.prefill[field.fieldCode] ?? '').trim()
      ? { ...field, language: prefilled === 'AR' ? 'ar' : 'en' }
      : field;
  });
}
