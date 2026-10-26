import { type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { accessRelationshipsApi } from '../api/identityAccessApi.ts';
import { type AccessRelationshipSummary } from '../api/types.ts';
import { useSaveAction } from '../forms.ts';
import { type OrganizationLookups } from '../lookups.ts';

interface AssignmentsTableProps {
  caption: string;
  assignments: AccessRelationshipSummary[];
  lookups: OrganizationLookups | undefined;
  userNames: Map<string, string>;
  /** ADM-010 lists every user's assignments; ADM-003 only the one user's, so it hides the column. */
  showUser: boolean;
  onEnded: () => void;
}

/** Assignments with their scope anchors and period; an active one can be ended, never edited (record D-5). */
export function AssignmentsTable({
  caption,
  assignments,
  lookups,
  userNames,
  showUser,
  onEnded,
}: AssignmentsTableProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const [ending, setEnding] = useState<AccessRelationshipSummary | null>(null);

  const scopeOf = (assignment: AccessRelationshipSummary) => {
    const parts = [
      lookups?.departmentName(assignment.departmentId),
      lookups?.entityName(assignment.externalEntityId),
      assignment.projectId === null
        ? null
        : t('identityAccess.assignments.projectScope', { project: assignment.projectId }),
    ].filter((part): part is string => typeof part === 'string');
    return parts.length === 0 ? t('identityAccess.assignments.noAnchor') : parts.join(' · ');
  };

  return (
    <>
      <TableContainer caption={caption}>
        <thead>
          <tr>
            {showUser && <th scope="col">{t('identityAccess.assignments.user')}</th>}
            <th scope="col">{t('identityAccess.assignments.role')}</th>
            <th scope="col">{t('identityAccess.assignments.scope')}</th>
            <th scope="col">{t('identityAccess.assignments.sponsor')}</th>
            <th scope="col">{t('identityAccess.assignments.period')}</th>
            <th scope="col">{t('identityAccess.assignments.status')}</th>
            <th scope="col">
              <span className="visually-hidden">{t('common.table.actions')}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {assignments.map((assignment) => (
            <tr key={assignment.id}>
              {showUser && (
                <td>
                  <Link to={`/admin/users/${assignment.userId}`}>
                    {userNames.get(assignment.userId) ??
                      t('identityAccess.assignments.unknownUser')}
                  </Link>
                </td>
              )}
              <td>{assignment.roleCode}</td>
              <td>{scopeOf(assignment)}</td>
              <td>
                {assignment.sponsorUserId === null
                  ? '—'
                  : (userNames.get(assignment.sponsorUserId) ??
                    t('identityAccess.assignments.unknownUser'))}
              </td>
              <td>
                {t('identityAccess.assignments.periodValue', {
                  start: formatDateTime(assignment.startsAt),
                  end:
                    assignment.endsAt === null
                      ? t('identityAccess.assignments.openEnded')
                      : formatDateTime(assignment.endsAt),
                })}
              </td>
              <td>
                <StatusBadge
                  label={
                    assignment.endReason === null
                      ? t(`identityAccess.assignmentStatus.${assignment.status}`)
                      : `${t(`identityAccess.assignmentStatus.${assignment.status}`)} — ${t(`identityAccess.endReason.${assignment.endReason}`)}`
                  }
                  tone={assignment.status === 'ACTIVE' ? 'positive' : 'neutral'}
                />
              </td>
              <td>
                {assignment.status === 'ACTIVE' && (
                  <button
                    type="button"
                    className="button button--danger"
                    onClick={() => {
                      setEnding(assignment);
                    }}
                  >
                    {t('identityAccess.assignments.end')}{' '}
                    <span className="visually-hidden">{assignment.roleCode}</span>
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
      <EndAssignmentDialog
        assignment={ending}
        onClose={() => {
          setEnding(null);
        }}
        onEnded={() => {
          setEnding(null);
          onEnded();
        }}
      />
    </>
  );
}

interface EndAssignmentDialogProps {
  assignment: AccessRelationshipSummary | null;
  onClose: () => void;
  onEnded: () => void;
}

function EndAssignmentDialog({
  assignment,
  onClose,
  onEnded,
}: EndAssignmentDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={assignment !== null}
      title={t('identityAccess.assignments.endTitle')}
      onClose={onClose}
    >
      {assignment !== null && (
        <EndAssignmentConfirmation assignment={assignment} onClose={onClose} onEnded={onEnded} />
      )}
    </Dialog>
  );
}

interface EndAssignmentConfirmationProps {
  assignment: AccessRelationshipSummary;
  onClose: () => void;
  onEnded: () => void;
}

function EndAssignmentConfirmation({
  assignment,
  onClose,
  onEnded,
}: EndAssignmentConfirmationProps): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction();

  const end = async () => {
    const result = await save.run(() => accessRelationshipsApi.end(assignment.id));
    if (result.ok) {
      onEnded();
    }
  };

  return (
    <>
      <p>{t('identityAccess.assignments.endBody', { role: assignment.roleCode })}</p>
      <p className="form__note">{t('identityAccess.stepUpNote')}</p>
      <FormAlert message={save.formError} />
      <div className="form__actions">
        <button
          type="button"
          className="button button--danger"
          disabled={save.saving}
          onClick={() => void end()}
        >
          {save.saving ? t('common.states.saving') : t('identityAccess.assignments.end')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </>
  );
}
