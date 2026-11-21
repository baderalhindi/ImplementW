import { type TranslationKey } from '@/shared/i18n/i18n.ts';

import { type Translate } from './problems.ts';

// Delete in the administration (the client wants no "retire"): the API answers what it did, or which records still need what was to be deleted.

export interface RecordUse {
  record: string;
  field: string;
  required: boolean;
  count: number;
}

export interface DeletionOutcome {
  deleted: boolean;
  clearedCount: number;
  blockedBy: RecordUse[];
}

/** What a record that uses something is called, in the person's words; one the screen does not name is shown by its table. */
const RECORD_NAMES: Partial<Record<string, TranslationKey>> = {
  'project.project': 'identityAccess.deletion.records.projects',
  'project.project.closed': 'identityAccess.deletion.records.closedProjects',
  'identity_access.external_entity': 'identityAccess.deletion.records.entities',
  'identity_access.user': 'identityAccess.deletion.records.people',
  'identity_access.access_relationship': 'identityAccess.deletion.records.assignments',
  'external_participation.external_update_request':
    'identityAccess.deletion.records.updateRequests',
  'external_participation.external_contribution': 'identityAccess.deletion.records.contributions',
  'risk.risk': 'identityAccess.deletion.records.risks',
  'management_concern.management_concern': 'identityAccess.deletion.records.concerns',
  'document_management.document': 'identityAccess.deletion.records.documents',
  'schedule.project_milestone': 'identityAccess.deletion.records.milestones',
  'audit_activity.audit_event': 'identityAccess.deletion.records.audit',
};

function recordName(record: string, t: Translate): string {
  const key = RECORD_NAMES[record];
  if (key !== undefined) {
    return t(key);
  }
  return record.startsWith('master_data_config.') ||
    record.startsWith('reports.') ||
    record.startsWith('dashboards.')
    ? t('identityAccess.deletion.records.settings')
    : record;
}

/** The uses that kept something from being deleted, as one phrase: "12 projects، 3 people". */
export function usesText(uses: readonly RecordUse[], t: Translate): string {
  return uses
    .map((use) =>
      t('identityAccess.deletion.use', { count: use.count, record: recordName(use.record, t) }),
    )
    .join('، ');
}
