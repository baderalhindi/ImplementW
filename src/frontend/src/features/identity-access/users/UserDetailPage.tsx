import { type ReactElement, type ReactNode, useCallback, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';

import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { accessRelationshipsApi, MAX_PAGE_SIZE, usersApi } from '../api/identityAccessApi.ts';
import { type UserDetail } from '../api/types.ts';
import { AssignmentsTable } from '../assignments/AssignmentsTable.tsx';
import { useUserNames } from '../assignments/useUserNames.ts';
import { AssignRoleDialog } from '../assignments/AssignRoleDialog.tsx';
import { ConfirmDialog, type Confirmation } from '../components/ConfirmDialog.tsx';
import { type OrganizationLookups, useOrganizationLookups } from '../lookups.ts';
import { useCan } from '../permissions/permissions.ts';
import { problemMessage } from '../problems.ts';
import { TemporaryPasswordNotice } from './TemporaryPasswordNotice.tsx';
import { ResetPasswordDialog, SetEndDateDialog } from './UserAccountDialogs.tsx';
import { NationalIdSection } from './NationalIdSection.tsx';
import { UserPermissionsSection } from './UserPermissionsSection.tsx';
import { UserStatusDialog } from './UserStatusDialog.tsx';

type Notice =
  | 'created'
  | 'updated'
  | 'statusChanged'
  | 'assigned'
  | 'ended'
  | 'passwordReset'
  | 'unlocked'
  | 'endDateSet';

function noticeFromState(state: unknown): Notice | null {
  const notice = (state as { notice?: unknown } | null)?.notice;
  return notice === 'created' || notice === 'updated' ? notice : null;
}

function isLocked(user: UserDetail): boolean {
  return user.lockedUntil !== null && new Date(user.lockedUntil) > new Date();
}

function Detail({ term, children }: { term: string; children: ReactNode }): ReactElement {
  return (
    <div className="details__row">
      <dt>{term}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/** ADM-003 User Detail, with MOD-080 and the user's assignments (ADM-010 for one user, MOD-081/MOD-082). */
export function UserDetailPage(): ReactElement {
  const { t, language } = useI18n();
  const { userId = '' } = useParams();
  const location = useLocation();
  const [notice, setNotice] = useState<Notice | null>(() => noticeFromState(location.state));
  const [statusDialogOpen, setStatusDialogOpen] = useState(false);
  const [assignDialogOpen, setAssignDialogOpen] = useState(false);
  const [resetDialogOpen, setResetDialogOpen] = useState(false);
  const [endDateDialogOpen, setEndDateDialogOpen] = useState(false);
  const [unlock, setUnlock] = useState<Confirmation | null>(null);
  const [deletion, setDeletion] = useState<Confirmation | null>(null);
  const navigate = useNavigate();
  // In memory only, and gone with the screen: the API returns a temporary password once.
  const [temporaryPassword, setTemporaryPassword] = useState<string | null>(null);
  const mayManage = useCan('USER_MANAGE');

  const loadUser = useCallback((signal: AbortSignal) => usersApi.get(userId, signal), [userId]);
  const user = useApiResource(loadUser);
  const { lookups } = useOrganizationLookups(language);

  const loadAssignments = useCallback(
    (signal: AbortSignal) =>
      accessRelationshipsApi.list({ userId, pageSize: MAX_PAGE_SIZE }, signal),
    [userId],
  );
  const assignments = useApiResource(loadAssignments);
  const userNames = useUserNames(assignments.data?.items.map((a) => a.sponsorUserId) ?? []);

  if (user.loading) {
    return <LoadingState />;
  }
  if (user.error !== null || user.data === undefined) {
    return (
      <ErrorState
        error={user.error}
        message={problemMessage(user.error, t)}
        onRetry={user.reload}
      />
    );
  }

  const detail = user.data.data;
  const administrable = detail.userType !== 'SERVICE';
  const etag = user.data.etag;
  const managesLocal = administrable && mayManage && detail.accountSource === 'LOCAL';

  return (
    <>
      <PageHeader
        title={detail.displayName}
        description={t('identityAccess.users.detail.description')}
        actions={
          administrable ? (
            <>
              <Link className="button" to={`/admin/users/${detail.id}/edit`}>
                {t('common.actions.edit')}
              </Link>
              <button
                type="button"
                className={detail.status === 'ACTIVE' ? 'button button--danger' : 'button'}
                onClick={() => {
                  setStatusDialogOpen(true);
                }}
              >
                {detail.status === 'ACTIVE'
                  ? t('identityAccess.userStatusDialog.disable')
                  : t('identityAccess.userStatusDialog.activate')}
              </button>
              {mayManage && (
                <button
                  type="button"
                  className="button button--danger"
                  onClick={() => {
                    setDeletion({
                      title: t('identityAccess.users.delete.title'),
                      body: t('identityAccess.users.delete.body', { name: detail.displayName }),
                      confirmLabel: t('identityAccess.users.delete.confirm'),
                      destructive: true,
                      action: () => usersApi.remove(detail.id, etag),
                    });
                  }}
                >
                  {t('identityAccess.users.delete.confirm')}
                </button>
              )}
              {managesLocal && (
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setResetDialogOpen(true);
                  }}
                >
                  {t('identityAccess.users.account.resetPassword')}
                </button>
              )}
              {managesLocal && isLocked(detail) && (
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setUnlock({
                      title: t('identityAccess.users.account.unlockTitle', {
                        name: detail.displayName,
                      }),
                      body: t('identityAccess.users.account.unlockBody'),
                      confirmLabel: t('identityAccess.users.account.unlock'),
                      destructive: false,
                      action: () => usersApi.unlock(detail.id, etag),
                    });
                  }}
                >
                  {t('identityAccess.users.account.unlock')}
                </button>
              )}
              {mayManage && (
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setEndDateDialogOpen(true);
                  }}
                >
                  {t('identityAccess.users.account.setEndDate')}
                </button>
              )}
            </>
          ) : undefined
        }
      />
      {notice !== null && (
        <p className="notice" role="status">
          {t(`identityAccess.users.detail.notice.${notice}`)}
        </p>
      )}

      {temporaryPassword !== null && (
        <TemporaryPasswordNotice
          name={detail.displayName}
          password={temporaryPassword}
          onDismiss={() => {
            setTemporaryPassword(null);
          }}
        />
      )}

      <UserSummaryDetails user={detail} lookups={lookups} />

      {administrable && (
        <NationalIdSection
          user={detail}
          etag={etag}
          onChanged={() => {
            setNotice('updated');
            user.reload();
          }}
        />
      )}

      <section className="section" aria-labelledby="assignments-heading">
        <div className="section__header">
          <h2 id="assignments-heading">{t('identityAccess.users.detail.assignments')}</h2>
          {administrable && detail.status === 'ACTIVE' && (
            <button
              type="button"
              className="button button--primary"
              onClick={() => {
                setAssignDialogOpen(true);
              }}
            >
              {t('identityAccess.assignRole.open')}
            </button>
          )}
        </div>
        {assignments.loading && <LoadingState />}
        {assignments.error !== null && (
          <ErrorState
            error={assignments.error}
            message={problemMessage(assignments.error, t)}
            onRetry={assignments.reload}
          />
        )}
        {assignments.data !== undefined &&
          (assignments.data.items.length === 0 ? (
            <EmptyState title={t('identityAccess.users.detail.noAssignments')} />
          ) : (
            <AssignmentsTable
              caption={t('identityAccess.users.detail.assignmentsCaption', {
                name: detail.displayName,
              })}
              assignments={assignments.data.items}
              lookups={lookups}
              userNames={userNames}
              showUser={false}
              onEnded={() => {
                setNotice('ended');
                assignments.reload();
              }}
            />
          ))}
      </section>

      {administrable && (
        <UserPermissionsSection userId={detail.id} displayName={detail.displayName} />
      )}

      <ConfirmDialog
        confirmation={deletion}
        onClose={() => {
          setDeletion(null);
        }}
        onDone={() => {
          void navigate('/admin/users');
        }}
      />
      <UserStatusDialog
        open={statusDialogOpen}
        user={detail}
        etag={user.data.etag}
        onClose={() => {
          setStatusDialogOpen(false);
        }}
        onChanged={() => {
          setStatusDialogOpen(false);
          setNotice('statusChanged');
          user.reload();
        }}
      />
      <ResetPasswordDialog
        open={resetDialogOpen}
        user={detail}
        etag={etag}
        onClose={() => {
          setResetDialogOpen(false);
        }}
        onDone={(response) => {
          setResetDialogOpen(false);
          setTemporaryPassword(response.data.temporaryPassword ?? null);
          setNotice('passwordReset');
          user.reload();
        }}
      />
      <SetEndDateDialog
        open={endDateDialogOpen}
        user={detail}
        etag={etag}
        onClose={() => {
          setEndDateDialogOpen(false);
        }}
        onDone={() => {
          setEndDateDialogOpen(false);
          setNotice('endDateSet');
          user.reload();
        }}
      />
      <ConfirmDialog
        confirmation={unlock}
        onClose={() => {
          setUnlock(null);
        }}
        onDone={() => {
          setUnlock(null);
          setNotice('unlocked');
          user.reload();
        }}
      />
      <AssignRoleDialog
        open={assignDialogOpen}
        user={detail}
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

function UserSummaryDetails({
  user,
  lookups,
}: {
  user: UserDetail;
  lookups: OrganizationLookups | undefined;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const none = t('common.values.none');
  return (
    <dl className="details">
      <Detail term={t('identityAccess.users.fields.status')}>
        <StatusBadge
          label={t(`identityAccess.userStatus.${user.status}`)}
          tone={user.status === 'ACTIVE' ? 'positive' : 'negative'}
        />
        {user.disabledAt !== null && (
          <span className="details__aside">
            {t('identityAccess.users.detail.disabledAt', { date: formatDateTime(user.disabledAt) })}
          </span>
        )}
      </Detail>
      <Detail term={t('identityAccess.users.fields.userType')}>
        {t(`identityAccess.userType.${user.userType}`)}
      </Detail>
      <Detail term={t('identityAccess.users.fields.accountSource')}>
        {t(`identityAccess.accountSource.${user.accountSource}`)}
      </Detail>
      {user.accountSource === 'LOCAL' && (
        <Detail term={t('identityAccess.users.fields.mustChangePassword')}>
          {user.mustChangePassword ? t('common.values.yes') : t('common.values.no')}
        </Detail>
      )}
      {isLocked(user) && user.lockedUntil !== null && (
        <Detail term={t('identityAccess.users.fields.lockedUntil')}>
          <StatusBadge label={t('identityAccess.users.account.locked')} tone="negative" />
          <span className="details__aside">{formatDateTime(user.lockedUntil)}</span>
        </Detail>
      )}
      <Detail term={t('identityAccess.users.fields.accountEndsOn')}>
        {user.accountEndsOn === null ? (
          t('identityAccess.users.account.noEndDate')
        ) : (
          <span dir="ltr">{user.accountEndsOn}</span>
        )}
      </Detail>
      <Detail term={t('identityAccess.users.fields.username')}>
        <span dir="ltr">{user.username}</span>
      </Detail>
      <Detail term={t('identityAccess.users.fields.email')}>
        <span dir="ltr">{user.email}</span>
      </Detail>
      <Detail term={t('identityAccess.users.fields.mobileNumber')}>
        {user.mobileNumber === null ? (
          none
        ) : (
          <>
            <span dir="ltr">{user.mobileNumber}</span>{' '}
            <StatusBadge
              label={
                user.mobileVerifiedAt === null
                  ? t('identityAccess.users.detail.mobileUnverified')
                  : t('identityAccess.users.detail.mobileVerified')
              }
              tone={user.mobileVerifiedAt === null ? 'neutral' : 'positive'}
            />
          </>
        )}
      </Detail>
      <Detail term={t('identityAccess.users.fields.department')}>
        {lookups?.departmentName(user.departmentId) ?? none}
      </Detail>
      <Detail term={t('identityAccess.users.fields.externalEntity')}>
        {lookups?.entityName(user.externalEntityId) ?? none}
      </Detail>
      <Detail term={t('identityAccess.users.fields.jobTitle')}>{user.jobTitle ?? none}</Detail>
      <Detail term={t('identityAccess.users.fields.directorySubjectId')}>
        {user.directorySubjectId === null ? none : <span dir="ltr">{user.directorySubjectId}</span>}
      </Detail>
      <Detail term={t('identityAccess.users.fields.preferredLanguage')}>
        {t(`common.languages.${user.preferredLanguage}`)}
      </Detail>
      <Detail term={t('identityAccess.users.fields.multiFactor')}>
        {user.multiFactorEnrolled ? t('common.values.yes') : t('common.values.no')}
      </Detail>
      <Detail term={t('identityAccess.users.fields.updatedAt')}>
        {formatDateTime(user.updatedAt)}
      </Detail>
    </dl>
  );
}
