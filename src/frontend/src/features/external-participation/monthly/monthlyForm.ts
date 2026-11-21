import { type ContributionFieldDefinition, type ContributionFieldRequest } from '../api/types.ts';
import { type MyUpdateRequest } from '../api/monthlyUpdateTypes.ts';
import { responseFieldsToSend } from '../rules.ts';

import { weightedOverall } from '@/features/projects/stagePlan/stageProgress.ts';

/** The overall figure, as before stages: a project with no stage plan answers it. */
const OVERALL_FIELD: ContributionFieldDefinition = {
  fieldCode: 'actualProgressPercent',
  fieldType: 'NUMBER',
  required: true,
  minimum: 0,
  maximum: 100,
};

export const CURRENT_STAGE_FIELD = 'currentStageCode';
export const CURRENT_STAGE_PERCENT_FIELD = 'currentStagePercent';

/** Review round 7, item 1: a project with a stage plan answers its current stage and that stage's % instead. */
const STAGE_FIELDS: readonly ContributionFieldDefinition[] = [
  {
    fieldCode: CURRENT_STAGE_FIELD,
    fieldType: 'CODE',
    required: true,
    minimum: null,
    maximum: null,
  },
  {
    fieldCode: CURRENT_STAGE_PERCENT_FIELD,
    fieldType: 'NUMBER',
    required: true,
    minimum: 0,
    maximum: 100,
  },
];

/** The MONTHLY_UPDATE schema's fields this request answers (`ContributionSchemas`), in its order; `/my-update-requests` serves values, not definitions. */
export function monthlyFields(request: MyUpdateRequest): readonly ContributionFieldDefinition[] {
  return [
    ...((request.stages?.length ?? 0) > 0 ? STAGE_FIELDS : [OVERALL_FIELD]),
    ...COMMON_FIELDS,
  ];
}

/** The overall % and planned % the stage answer weighs to, as the API weighs it: the stages before the current one complete, the rest not started. */
export function monthlyStageFigures(
  request: MyUpdateRequest,
  values: Readonly<Record<string, string>>,
): { actual: number | null; planned: number | null } {
  const stages = request.stages ?? [];
  const current = stages.findIndex((stage) => stage.stageCode === values[CURRENT_STAGE_FIELD]);
  const percent = Number((values[CURRENT_STAGE_PERCENT_FIELD] ?? '').trim());
  const typed =
    (values[CURRENT_STAGE_PERCENT_FIELD] ?? '').trim() !== '' && Number.isFinite(percent);
  return {
    actual:
      current < 0 || !typed
        ? null
        : weightedOverall(
            stages.map((stage, index) => ({
              weight: Number(stage.weightPercent),
              percent: index < current ? 100 : index === current ? percent : 0,
            })),
          ),
    planned: weightedOverall(
      stages.map((stage) => ({
        weight: Number(stage.weightPercent),
        percent: stage.plannedPercent === null ? null : Number(stage.plannedPercent),
      })),
    ),
  };
}

const COMMON_FIELDS: readonly ContributionFieldDefinition[] = [
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
  return responseFieldsToSend(monthlyFields(request), values, language).map((field) => {
    const prefilled = request.prefillLanguages[field.fieldCode];
    return field.language !== null &&
      prefilled !== undefined &&
      field.value === (request.prefill[field.fieldCode] ?? '').trim()
      ? { ...field, language: prefilled === 'AR' ? 'ar' : 'en' }
      : field;
  });
}
