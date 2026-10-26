import { type ExternalContributionDetail, type ExternalUpdateRequestDetail } from './api/types.ts';

// The least-disclosure projection on the screen side (WF-13 §8.2, TASK-066 D-6). The API decides the audience and
// withholds the internal-only fields; the screens render what `disclosed` returns and choose their layout by the
// record's `projection`, never by guessing from the session. `disclosed` drops every field the record names as masked
// and, for an EXTERNAL record, every field AHDA classifies as internal-only, so a field the server should have withheld
// still never reaches an external page (acceptance criterion 1).

/** A request's fields only AHDA sees (`ExternalParticipationViews.RequestInternalOnlyFields`). */
export const REQUEST_INTERNAL_ONLY_FIELDS = [
  'reviewerUserId',
  'participationConfigurationVersionId',
  'issuedByUserId',
  'createdBy',
  'updatedBy',
] as const satisfies readonly (keyof ExternalUpdateRequestDetail)[];

/** A revision's fields only AHDA sees (`ExternalParticipationViews.ContributionInternalOnlyFields`). */
export const CONTRIBUTION_INTERNAL_ONLY_FIELDS = [
  'targetVersion',
  'targetState',
  'reviewedByUserId',
  'reviewStartedAt',
  'reviewInternalNote',
  'createdBy',
  'updatedBy',
] as const satisfies readonly (keyof ExternalContributionDetail)[];

interface Projected {
  projection: 'INTERNAL' | 'EXTERNAL';
  maskedFields: string[];
}

export function isExternalView(record: Projected): boolean {
  return record.projection === 'EXTERNAL';
}

function withhold<T extends Projected>(record: T, internalOnly: readonly string[]): T {
  const withheld = new Set([
    ...record.maskedFields,
    ...(isExternalView(record) ? internalOnly : []),
  ]);
  return Object.fromEntries(Object.entries(record).filter(([field]) => !withheld.has(field))) as T;
}

/** The request as the person may see it: masked fields gone, and every internal-only field gone from an external view. */
export function disclosedRequest(
  request: ExternalUpdateRequestDetail,
): ExternalUpdateRequestDetail {
  return withhold(request, REQUEST_INTERNAL_ONLY_FIELDS);
}

/** The revision as the person may see it, by the same rule. */
export function disclosedContribution(
  contribution: ExternalContributionDetail,
): ExternalContributionDetail {
  return withhold(contribution, CONTRIBUTION_INTERNAL_ONLY_FIELDS);
}
