import { type GovernanceProfileSettings } from './api/types.ts';
import { type RegistrationField } from './registration.ts';

// ADR-015: mandatory fields follow the governance profile. A profile's `mandatoryFieldCodes` (GOVERNANCE_PROFILE,
// TASK-034) are free codes; this module reads a code as the registration property it names, in UPPER_SNAKE_CASE
// (`REGISTRATION_BUDGET_SAR` → `registrationBudgetSar`). A code naming anything else is not a registration field
// and asks nothing of this form (F-4).

const FIELD_BY_CODE: Record<string, RegistrationField> = {
  DESCRIPTION: 'description',
  EXTERNAL_ENTITY_ID: 'externalEntityId',
  REGISTRATION_BUDGET_SAR: 'registrationBudgetSar',
  PLANNED_START_DATE: 'plannedStartDate',
  PLANNED_END_DATE: 'plannedEndDate',
  REGION_ITEM_ID: 'regionItemId',
  CITY_ITEM_ID: 'cityItemId',
  LATITUDE: 'latitude',
  LONGITUDE: 'longitude',
};

/**
 * The registration fields a profile makes mandatory. They are asked for at submission, with the budget and dates
 * the API itself requires then: a draft may be saved incomplete (TASK-041 D-8).
 */
export function profileMandatoryFields(
  profiles: GovernanceProfileSettings[] | undefined,
  governanceProfileItemId: string,
): RegistrationField[] {
  const profile = profiles?.find(
    (candidate) => candidate.governanceProfileItemId === governanceProfileItemId,
  );
  return (profile?.mandatoryFieldCodes ?? [])
    .map((code) => FIELD_BY_CODE[code])
    .filter((field): field is RegistrationField => field !== undefined);
}
