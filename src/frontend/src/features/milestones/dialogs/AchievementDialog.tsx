import { type ReactElement, useCallback, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { varianceLabel } from '@/features/schedule/presentation.ts';
import { OverdueFlag } from '@/features/tasks/components/TaskBadges.tsx';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { activityName } from '@/features/tasks/useTaskData.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import { canChangeMilestone, canClaimAchievement } from '../access.ts';
import { milestonesApi } from '../api/milestonesApi.ts';
import { type MilestoneAchievementDetail, type ProjectMilestoneDetail } from '../api/types.ts';
import {
  AchievementBadge,
  MilestoneStatusBadge,
  RevisionStatusBadge,
} from '../components/MilestoneBadges.tsx';
import { ReturnedReason, ReturnedReasonLine } from '../components/ReturnedReason.tsx';
import { achievementState, nextClaimKind, openRevision, overdueDays } from '../milestoneRules.ts';
import { isForbidden, isStale, milestoneProblemMessage } from '../problems.ts';
import { type MilestoneLookups, useScheduleActivities } from '../useMilestoneData.ts';

import { type ClaimChanged, ClaimSummary, NewClaim, OpenRevision } from './ClaimSection.tsx';
import { type MilestoneDialog } from './milestoneDialog.ts';

interface AchievementDialogProps {
  milestoneId: string;
  project: ProjectSummary;
  user: SessionUser;
  lookups: MilestoneLookups;
  /** Another modal in its place: MOD-016 Edit, MOD-054 Attach, MOD-050 Upload. */
  onOpen: (dialog: MilestoneDialog) => void;
  onClose: () => void;
  /** Something was saved: the screen behind is read again. */
  onChanged: () => void;
}

/** The milestone with the ETag its commands send, and its revisions (null without MILESTONE_VIEW). */
interface MilestoneRecord {
  milestone: ApiResponse<ProjectMilestoneDetail>;
  revisions: MilestoneAchievementDetail[] | null;
}

async function readRecord(milestoneId: string, signal: AbortSignal): Promise<MilestoneRecord> {
  const [milestone, revisions] = await Promise.all([
    milestonesApi.milestone(milestoneId, signal),
    milestonesApi.revisions(milestoneId, signal).catch((error: unknown) => {
      if (isForbidden(error)) {
        return null;
      }
      throw error;
    }),
  ]);
  return { milestone, revisions: revisions?.sort((a, b) => b.revisionNo - a.revisionNo) ?? null };
}

/**
 * MOD-019 Milestone Achievement, a drawer: the milestone as WF-03 plans it, and its achievement as WF-05 claims it —
 * the claim, its evidence under the policy in force, submission to AHDA's review, and every revision. A returned claim's
 * reason is the first thing in it (acceptance criterion 2).
 */
export function AchievementDialog(props: AchievementDialogProps): ReactElement {
  const { t } = useI18n();
  const { milestoneId } = props;
  const load = useCallback((signal: AbortSignal) => readRecord(milestoneId, signal), [milestoneId]);
  const record = useApiResource(load, { keepWhileReloading: true });
  return (
    <Dialog
      open
      variant="drawer"
      title={record.data?.milestone.data.title.text ?? t('milestones.detail.title')}
      onClose={props.onClose}
    >
      {record.data === undefined ? (
        record.loading ? (
          <LoadingState />
        ) : (
          <ErrorState message={milestoneProblemMessage(record.error, t)} onRetry={record.reload} />
        )
      ) : (
        <AchievementBody {...props} record={record.data} reload={record.reload} />
      )}
    </Dialog>
  );
}

function AchievementBody({
  record: {
    milestone: { data: milestone, etag },
    revisions,
  },
  reload,
  project,
  user,
  lookups,
  onOpen,
  onClose,
  onChanged,
}: AchievementDialogProps & { record: MilestoneRecord; reload: () => void }): ReactElement {
  const { t } = useI18n();
  const activities = useScheduleActivities(project.id);
  const cancel = useSaveAction(milestoneProblemMessage);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const state = revisions === null ? null : achievementState(revisions);
  const open = revisions === null ? null : openRevision(revisions);
  const claimKind = revisions === null ? null : nextClaimKind(milestone, revisions);
  const claimant = canClaimAchievement(user, project);
  const changeable = canChangeMilestone(user, project, milestone);
  const overdue = overdueDays(milestone, todayUtc());

  const changed: ClaimChanged = (message: TranslationKey, tone = 'success') => {
    setNotice({ tone, message: t(message) });
    reload();
    onChanged();
  };

  const cancelMilestone = async () => {
    const result = await cancel.run(() => milestonesApi.cancelMilestone(milestone.id, etag));
    if (result.ok || isStale(result.error)) {
      setConfirmCancel(false);
      changed(
        result.ok ? 'milestones.done.cancelled' : 'milestones.done.stale',
        result.ok ? 'success' : 'warning',
      );
    }
  };

  return (
    <div className="task-detail">
      <p className="figure-group">
        <MilestoneStatusBadge status={milestone.status} />
        {state !== null && <AchievementBadge state={state} />}
        {overdue !== null && <OverdueFlag days={overdue} />}
      </p>
      <PageNotice notice={notice} />
      {state?.kind === 'returned' && <ReturnedReason revision={state.revision} />}
      {state !== null && state.kind !== 'unclaimed' && (
        <AcceptedLine accepted={state.kind === 'accepted' ? state.revision : state.accepted} />
      )}

      <dl className="details">
        <Detail term={t('milestones.table.project')}>
          <span dir="auto">{project.title.text}</span>
        </Detail>
        <Detail term={t('milestones.fields.category')}>
          {lookups.itemLabel(milestone.milestoneCategoryItemId)}
        </Detail>
        <Detail term={t('milestones.fields.activity')}>
          <span dir="auto">
            {activityName(milestone.scheduleActivityId, activities.data ?? null) ??
              t('milestones.detail.noActivity')}
          </span>
        </Detail>
        <Detail term={t('milestones.fields.forecastDate')}>
          <span dir="ltr">{milestone.forecastDate}</span>
        </Detail>
        <Detail term={t('milestones.detail.baseline')}>
          {milestone.baselinePlannedDate === null ? (
            t('milestones.table.notBaselined')
          ) : (
            <>
              <span dir="ltr">{milestone.baselinePlannedDate}</span>
              <span className="details__aside">
                {varianceLabel(milestone.forecastVarianceDays, t)}
              </span>
            </>
          )}
        </Detail>
      </dl>

      <section className="task-detail__section" aria-labelledby={`claim-${milestone.id}`}>
        <h3 id={`claim-${milestone.id}`}>{t('milestones.claim.title')}</h3>
        {revisions === null ? (
          <p className="state">{t('milestones.claim.forbidden')}</p>
        ) : open !== null ? (
          <OpenRevision
            key={open.id}
            revisionId={open.id}
            editable={claimant}
            lookups={lookups}
            onAttach={(revision, mandatory) => {
              onOpen({
                kind: 'attach',
                milestoneId: milestone.id,
                achievementId: revision.data.id,
                etag: revision.etag,
                mandatory,
              });
            }}
            onUpload={() => {
              onOpen({ kind: 'upload', milestoneId: milestone.id });
            }}
            onChanged={changed}
          />
        ) : claimKind !== null && claimant ? (
          <NewClaim
            key={revisions.length}
            milestone={milestone}
            kind={claimKind}
            onChanged={changed}
          />
        ) : (
          <p className="form__note">{whyNoClaim(milestone, project, t)}</p>
        )}
      </section>

      {revisions !== null && revisions.length > 0 && (
        <section className="task-detail__section" aria-labelledby={`revisions-${milestone.id}`}>
          <h3 id={`revisions-${milestone.id}`}>{t('milestones.revision.title')}</h3>
          <RevisionHistory revisions={revisions} />
        </section>
      )}

      {confirmCancel && (
        <div className="task-detail__confirm">
          <p>{t('milestones.cancel.body')}</p>
          <FormAlert message={cancel.formError} />
          <div className="form__actions">
            <button
              type="button"
              className="button button--danger"
              disabled={cancel.saving}
              onClick={() => void cancelMilestone()}
            >
              {t('milestones.cancel.confirm')}
            </button>
            <button
              type="button"
              className="button"
              disabled={cancel.saving}
              onClick={() => {
                setConfirmCancel(false);
              }}
            >
              {t('milestones.cancel.keep')}
            </button>
          </div>
        </div>
      )}
      <div className="form__actions">
        {changeable && (
          <button
            type="button"
            className="button"
            onClick={() => {
              onOpen({ kind: 'edit', milestoneId: milestone.id });
            }}
          >
            {t('milestones.actions.edit')}
          </button>
        )}
        {changeable && (
          <button
            type="button"
            className="button button--danger"
            aria-expanded={confirmCancel}
            onClick={() => {
              setConfirmCancel(!confirmCancel);
            }}
          >
            {t('milestones.actions.cancel')}
          </button>
        )}
        <button type="button" className="button button--primary" onClick={onClose}>
          {t('milestones.actions.close')}
        </button>
      </div>
    </div>
  );
}

/** The accepted achievement, kept while a correction is prepared, reviewed or returned. */
function AcceptedLine({
  accepted,
}: {
  accepted: MilestoneAchievementDetail | null;
}): ReactElement | null {
  const { t } = useI18n();
  const personName = usePersonNames([accepted?.reviewedByUserId ?? null]);
  if (accepted === null) {
    return null;
  }
  return (
    <p className="notice">
      {t('milestones.detail.accepted', {
        date: accepted.acceptedActualAchievementDate ?? '',
        revision: accepted.revisionNo,
        reviewer: personName(accepted.reviewedByUserId),
      })}
    </p>
  );
}

/** Why no claim can be opened now, in words. */
function whyNoClaim(
  milestone: ProjectMilestoneDetail,
  project: ProjectSummary,
  t: (key: TranslationKey) => string,
): string {
  if (milestone.status === 'CANCELLED') {
    return t('milestones.claim.why.cancelled');
  }
  return project.status === 'ACTIVE'
    ? t('milestones.claim.why.notManager')
    : t('milestones.claim.why.projectNotActive');
}

/** Every revision, newest first: its status, dates and review; a returned one's reason on its own line, no click needed. */
function RevisionHistory({ revisions }: { revisions: MilestoneAchievementDetail[] }): ReactElement {
  const { t } = useI18n();
  const personName = usePersonNames(revisions.map((revision) => revision.reviewedByUserId));
  const numberOf = new Map(revisions.map((revision) => [revision.id, revision.revisionNo]));
  return (
    <ol className="revision-list">
      {revisions.map((revision) => (
        <li key={revision.id}>
          <p className="figure-group">
            <span className="revision-list__number">
              {t('milestones.revision.number', { number: revision.revisionNo })}
            </span>
            <RevisionStatusBadge status={revision.status} />
            {revision.isCurrent && <span>{t('milestones.revision.current')}</span>}
          </p>
          <ClaimSummary claim={revision} />
          <p className="cell__aside">
            {[
              revision.submittedAt === null
                ? null
                : t('milestones.revision.submitted', { date: revision.submittedAt.slice(0, 10) }),
              revision.reviewedAt === null
                ? null
                : t('milestones.revision.reviewed', {
                    date: revision.reviewedAt.slice(0, 10),
                    reviewer: personName(revision.reviewedByUserId),
                  }),
              revision.acceptedActualAchievementDate === null
                ? null
                : t('milestones.revision.acceptedDate', {
                    date: revision.acceptedActualAchievementDate,
                  }),
              revision.supersededByAchievementId === null
                ? null
                : t('milestones.revision.supersededBy', {
                    number: numberOf.get(revision.supersededByAchievementId) ?? '?',
                  }),
            ]
              .filter((part) => part !== null)
              .join(' · ')}
          </p>
          {revision.status === 'RETURNED' && <ReturnedReasonLine revision={revision} />}
        </li>
      ))}
    </ol>
  );
}
