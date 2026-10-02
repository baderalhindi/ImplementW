import { checkText } from '@/features/identity-access/forms.ts';
import { type Language, type TranslationKey } from '@/shared/i18n/i18n.ts';

import {
  type NarrativeText,
  type NarrativeTextRequest,
  type ParticipationMode,
  type ProjectDetail,
  type ProjectRequest,
} from './api/types.ts';

// The registration as SCR-033/034/035 edit it, and the checks the API makes on it (ProjectRequest.Validate,
// TASK-041 D-8), so a mistake is caught before the round trip. The API stays the authority: its field errors land on
// the same inputs.

export interface RegistrationValues {
  title: string;
  description: string;
  classificationItemId: string;
  departmentId: string;
  externalEntityId: string;
  participationMode: ParticipationMode | '';
  governanceProfileItemId: string;
  registrationBudgetSar: string;
  plannedStartDate: string;
  plannedEndDate: string;
  regionItemId: string;
  cityItemId: string;
  latitude: string;
  longitude: string;
}

export type RegistrationField = keyof RegistrationValues;

/** The label of each field, for the lists of what is still needed. */
export const FIELD_LABELS: Record<RegistrationField, TranslationKey> = {
  title: 'projects.fields.title',
  description: 'projects.fields.description',
  classificationItemId: 'projects.fields.classification',
  departmentId: 'projects.fields.department',
  externalEntityId: 'projects.fields.externalEntity',
  participationMode: 'projects.fields.participation',
  governanceProfileItemId: 'projects.fields.governanceProfile',
  registrationBudgetSar: 'projects.fields.registrationBudget',
  plannedStartDate: 'projects.fields.plannedStartDate',
  plannedEndDate: 'projects.fields.plannedEndDate',
  regionItemId: 'projects.fields.region',
  cityItemId: 'projects.fields.city',
  latitude: 'projects.fields.latitude',
  longitude: 'projects.fields.longitude',
};

export const EMPTY_REGISTRATION: RegistrationValues = {
  title: '',
  description: '',
  classificationItemId: '',
  departmentId: '',
  externalEntityId: '',
  participationMode: '',
  governanceProfileItemId: '',
  registrationBudgetSar: '',
  plannedStartDate: '',
  plannedEndDate: '',
  regionItemId: '',
  cityItemId: '',
  latitude: '',
  longitude: '',
};

/** NarrativeTextRequest.TextLength. */
export const TEXT_LENGTH = 2000;

/** R-16, not negative: up to 16 integer digits and up to two decimals as typed; sent with exactly two. */
const BUDGET_PATTERN = /^[0-9]{1,16}(\.[0-9]{1,2})?$/;
const DECIMAL_PATTERN = /^-?[0-9]{1,3}(\.[0-9]{1,8})?$/;

/** What the API requires to create or edit a draft (TASK-041 D-8). */
export function draftRequiredFields(values: RegistrationValues): RegistrationField[] {
  return [
    'title',
    'classificationItemId',
    'departmentId',
    'participationMode',
    'governanceProfileItemId',
    ...(values.participationMode === 'ENTITY_MANAGED' ? (['externalEntityId'] as const) : []),
  ];
}

/** What submission adds (TASK-041 D-8, `PROJECT_INCOMPLETE`): the registration budget and both planned dates. */
export const SUBMISSION_REQUIRED_FIELDS: RegistrationField[] = [
  'registrationBudgetSar',
  'plannedStartDate',
  'plannedEndDate',
];

function isBlank(value: string): boolean {
  return value.trim() === '';
}

function checkCoordinate(value: string, limit: number): string | null {
  if (isBlank(value)) {
    return null;
  }
  const number = Number(value);
  return DECIMAL_PATTERN.test(value.trim()) && Math.abs(number) <= limit ? null : 'MALFORMED';
}

/**
 * The API code of the first failed check of each field (REQUIRED, MAX_LENGTH, MALFORMED, DATE_BEFORE_START), or
 * null. `required` names the fields that must have a value at this step.
 */
export function checkRegistration(
  values: RegistrationValues,
  required: readonly RegistrationField[],
): Partial<Record<RegistrationField, string | null>> {
  const codes: Partial<Record<RegistrationField, string | null>> = {};
  for (const field of required) {
    if (isBlank(values[field])) {
      codes[field] = 'REQUIRED';
    }
  }
  codes.title ??= checkText(values.title, { maxLength: TEXT_LENGTH });
  codes.description ??= checkText(values.description, { maxLength: TEXT_LENGTH });
  if (!isBlank(values.registrationBudgetSar)) {
    codes.registrationBudgetSar ??= BUDGET_PATTERN.test(values.registrationBudgetSar.trim())
      ? null
      : 'MALFORMED';
  }
  if (
    !isBlank(values.plannedStartDate) &&
    !isBlank(values.plannedEndDate) &&
    values.plannedEndDate < values.plannedStartDate
  ) {
    codes.plannedEndDate ??= 'DATE_BEFORE_START';
  }
  codes.latitude ??= checkCoordinate(values.latitude, 90);
  codes.longitude ??= checkCoordinate(values.longitude, 180);
  return codes;
}

export function hasErrors(codes: Partial<Record<RegistrationField, string | null>>): boolean {
  return Object.values(codes).some((code) => typeof code === 'string');
}

/** The fields among `required` that are still empty, in form order. */
export function missingFields(
  values: RegistrationValues,
  required: readonly RegistrationField[],
): RegistrationField[] {
  return (Object.keys(EMPTY_REGISTRATION) as RegistrationField[]).filter(
    (field) => required.includes(field) && isBlank(values[field]),
  );
}

export function valuesOf(project: ProjectDetail): RegistrationValues {
  return {
    title: project.title.text,
    description: project.description?.text ?? '',
    classificationItemId: project.classificationItemId,
    departmentId: project.departmentId,
    externalEntityId: project.externalEntityId ?? '',
    participationMode: project.participationMode,
    governanceProfileItemId: project.governanceProfileItemId,
    registrationBudgetSar: project.registrationBudgetSar ?? '',
    plannedStartDate: project.plannedStartDate ?? '',
    plannedEndDate: project.plannedEndDate ?? '',
    regionItemId: project.regionItemId ?? '',
    cityItemId: project.cityItemId ?? '',
    latitude: project.latitude === null ? '' : String(project.latitude),
    longitude: project.longitude === null ? '' : String(project.longitude),
  };
}

function optional(value: string): string | null {
  return isBlank(value) ? null : value.trim();
}

function optionalNumber(value: string): number | null {
  return isBlank(value) ? null : Number(value);
}

/** The API writes a text's language as `EN`/`AR` and reads it as `en`/`ar`. */
function languageOf(tag: string): Language {
  return tag.toLowerCase() === 'ar' ? 'ar' : 'en';
}

/** R-16: exactly two fraction digits. */
export function moneyString(value: string): string {
  const [whole = '0', fraction = ''] = value.trim().split('.');
  return `${whole}.${fraction.padEnd(2, '0')}`;
}

/**
 * The request for valid values. Free text is tagged with the interface language, the language the person writes in
 * (ADR-012 extension, ERD D-7); an edit keeps the language a text was entered in unless the text changed.
 */
export function toRequest(
  values: RegistrationValues,
  language: Language,
  existing?: ProjectDetail,
): ProjectRequest {
  const narrative = (text: string, before: NarrativeText | null): NarrativeTextRequest => {
    const trimmed = text.trim();
    const unchanged = before !== null && before.text === trimmed;
    return { text: trimmed, language: unchanged ? languageOf(before.language) : language };
  };
  const budget = optional(values.registrationBudgetSar);
  return {
    title: narrative(values.title, existing?.title ?? null),
    description: isBlank(values.description)
      ? null
      : narrative(values.description, existing?.description ?? null),
    classificationItemId: values.classificationItemId,
    departmentId: values.departmentId,
    externalEntityId: optional(values.externalEntityId),
    // Required at every step, so checkRegistration has refused an empty one before this is called.
    participationMode: values.participationMode as ParticipationMode,
    governanceProfileItemId: values.governanceProfileItemId,
    registrationBudgetSar: budget === null ? null : moneyString(budget),
    plannedStartDate: optional(values.plannedStartDate),
    plannedEndDate: optional(values.plannedEndDate),
    regionItemId: optional(values.regionItemId),
    cityItemId: optional(values.cityItemId),
    latitude: optionalNumber(values.latitude),
    longitude: optionalNumber(values.longitude),
  };
}
