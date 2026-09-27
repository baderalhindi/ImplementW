import { type SyntheticEvent, type ReactElement, useCallback, useState } from 'react';
import { useSearchParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField, TextField } from '@/shared/ui/FormFields.tsx';
import { PageHeader, Pagination } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { accessRelationshipsApi } from '../api/identityAccessApi.ts';
import { type AccessRelationshipStatus } from '../api/types.ts';
import { UUID_PATTERN } from '../forms.ts';
import { useOrganizationLookups } from '../lookups.ts';
import { fieldMessage, problemMessage } from '../problems.ts';
import { AssignmentsTable } from './AssignmentsTable.tsx';
import { useUserNames } from './useUserNames.ts';
import { AssignRoleDialog } from './AssignRoleDialog.tsx';

const PAGE_SIZE = 25;
const STATUSES: AccessRelationshipStatus[] = ['ACTIVE', 'ENDED'];

function statusOf(value: string | null): AccessRelationshipStatus | undefined {
  return STATUSES.find((status) => status === value);
}

/** ADM-010 Role Assignment: every assignment, filtered by status or project, and MOD-081/MOD-082 to add one. */
export function RoleAssignmentPage(): ReactElement {
  const { t, language } = useI18n();
  const [params, setParams] = useSearchParams();
  const [assignDialogOpen, setAssignDialogOpen] = useState(false);
  const [notice, setNotice] = useState<'assigned' | 'ended' | null>(null);
  const { lookups } = useOrganizationLookups(language);

  const status = statusOf(params.get('status'));
  const projectId = params.get('projectId') ?? undefined;
  const page = Math.max(1, Number(params.get('page') ?? '1') || 1);

  const load = useCallback(
    (signal: AbortSignal) =>
      accessRelationshipsApi.list({ status, projectId, page, pageSize: PAGE_SIZE }, signal),
    [status, projectId, page],
  );
  const assignments = useApiResource(load);
  const items = assignments.data?.items ?? [];
  const userNames = useUserNames(items.flatMap((a) => [a.userId, a.sponsorUserId]));

  const goToPage = (next: number) => {
    const updated = new URLSearchParams(params);
    updated.set('page', String(next));
    setParams(updated);
  };

  return (
    <>
      <PageHeader
        title={t('identityAccess.assignments.title')}
        description={t('identityAccess.assignments.description')}
        actions={
          <button
            type="button"
            className="button button--primary"
            onClick={() => {
              setAssignDialogOpen(true);
            }}
          >
            {t('identityAccess.assignRole.open')}
          </button>
        }
      />
      {notice !== null && (
        <p className="notice" role="status">
          {t(`identityAccess.users.detail.notice.${notice}`)}
        </p>
      )}

      <AssignmentFilters key={params.toString()} status={status} projectId={projectId} />

      {assignments.loading && <LoadingState />}
      {assignments.error !== null && (
        <ErrorState message={problemMessage(assignments.error, t)} onRetry={assignments.reload} />
      )}
      {assignments.data !== undefined &&
        (items.length === 0 ? (
          <EmptyState
            title={
              status === undefined && projectId === undefined
                ? t('identityAccess.assignments.empty')
                : t('identityAccess.assignments.emptyFiltered')
            }
          />
        ) : (
          <>
            <AssignmentsTable
              caption={t('identityAccess.assignments.title')}
              assignments={items}
              lookups={lookups}
              userNames={userNames}
              showUser
              onEnded={() => {
                setNotice('ended');
                assignments.reload();
              }}
            />
            <Pagination
              page={assignments.data.page}
              pageSize={assignments.data.pageSize}
              totalCount={assignments.data.totalCount}
              onPageChange={goToPage}
            />
          </>
        ))}

      <AssignRoleDialog
        open={assignDialogOpen}
        onClose={() => {
          setAssignDialogOpen(false);
        }}
        onAssigned={() => {
          setAssignDialogOpen(false);
          setNotice('assigned');
          assignments.reload();
        }}
      />
    </>
  );
}

interface AssignmentFiltersProps {
  status: AccessRelationshipStatus | undefined;
  projectId: string | undefined;
}

function AssignmentFilters({ status, projectId }: AssignmentFiltersProps): ReactElement {
  const { t } = useI18n();
  const [, setParams] = useSearchParams();
  const [draftStatus, setDraftStatus] = useState<string>(status ?? '');
  const [draftProject, setDraftProject] = useState(projectId ?? '');
  const [projectError, setProjectError] = useState<string | undefined>(undefined);

  const apply = (event: SyntheticEvent) => {
    event.preventDefault();
    const project = draftProject.trim();
    if (project !== '' && !UUID_PATTERN.test(project)) {
      setProjectError(fieldMessage('MALFORMED', t));
      return;
    }
    const next = new URLSearchParams();
    if (draftStatus !== '') {
      next.set('status', draftStatus);
    }
    if (project !== '') {
      next.set('projectId', project);
    }
    setParams(next);
  };

  return (
    <form className="filters" noValidate onSubmit={apply} aria-label={t('common.filters.label')}>
      <FormAlert
        message={projectError === undefined ? null : t('common.form.fixErrors', { count: 1 })}
      />
      <SelectField
        label={t('identityAccess.assignments.status')}
        name="status"
        value={draftStatus}
        placeholder={t('common.filters.any')}
        options={STATUSES.map((value) => ({
          value,
          label: t(`identityAccess.assignmentStatus.${value}`),
        }))}
        onChange={setDraftStatus}
      />
      <TextField
        label={t('identityAccess.assignScope.projectId')}
        name="projectId"
        dir="ltr"
        value={draftProject}
        onChange={setDraftProject}
        error={projectError}
      />
      <div className="filters__actions">
        <button type="submit" className="button button--primary">
          {t('common.filters.apply')}
        </button>
      </div>
    </form>
  );
}
