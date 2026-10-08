import { type ReactElement, type ReactNode } from 'react';
import { Link } from 'react-router';

import { instanceTone } from '@/features/approvals/presentation.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { activationStateOf, approvalStateOf, type GovernedStatus } from '../governedRequest.ts';
import { activationTone, approvalTone, statusTone } from '../presentation.ts';
import { type Review } from '../useSuspensionClosureData.ts';

/** A WF-09 request's or WF-10 case's lifecycle status. */
export function GovernedStatusBadge({ status }: { status: GovernedStatus }): ReactElement {
  const { t } = useI18n();
  return (
    <span data-badge="status">
      <StatusBadge label={t(`suspensionClosure.status.${status}`)} tone={statusTone(status)} />
    </span>
  );
}

/**
 * Approval and activation as two labelled dimensions (TASK-062 D-5, TASK-063 D-4; Step 16 "WF-11 decisions acknowledged
 * separately from source application"): an approved record has not moved the project until it is activated, and the
 * screen says so in words, beside the lifecycle status.
 */
export function GovernedStateSection({
  status,
  explanation,
  children,
}: {
  status: GovernedStatus;
  explanation: string;
  children?: ReactNode;
}): ReactElement {
  const { t } = useI18n();
  const approval = approvalStateOf(status);
  const activation = activationStateOf(status);
  return (
    <section className="section" aria-labelledby="governed-state">
      <h2 id="governed-state">{t('suspensionClosure.state.title')}</h2>
      <dl className="details state-dimensions">
        <Detail term={t('suspensionClosure.state.approval')}>
          <span data-badge="approval">
            <StatusBadge
              label={t(`suspensionClosure.approval.${approval}`)}
              tone={approvalTone(approval)}
            />
          </span>
        </Detail>
        <Detail term={t('suspensionClosure.state.activation')}>
          <span data-badge="activation">
            <StatusBadge
              label={t(`suspensionClosure.activation.${activation}`)}
              tone={activationTone(activation)}
            />
          </span>
        </Detail>
        <Detail term={t('suspensionClosure.state.status')}>
          <GovernedStatusBadge status={status} />
        </Detail>
        {children}
      </dl>
      <p className="form__note" data-field="stateExplanation">
        {explanation}
      </p>
    </section>
  );
}

export interface Command {
  key: string;
  label: string;
  onClick: () => void;
  primary?: boolean;
  danger?: boolean;
}

/** The commands the record's state and the person allow, in lifecycle order; links (edit) come first. */
export function CommandBar({
  label,
  links,
  commands,
}: {
  label: string;
  links?: ReactNode;
  commands: Command[];
}): ReactElement | null {
  if (commands.length === 0 && (links === undefined || links === null || links === false)) {
    return null;
  }
  return (
    <div className="risk-detail__commands" role="group" aria-label={label}>
      {links}
      {commands.map((command) => (
        <button
          key={command.key}
          type="button"
          className={
            command.danger === true
              ? 'button button--danger'
              : command.primary === true
                ? 'button button--primary'
                : 'button'
          }
          onClick={command.onClick}
        >
          {command.label}
        </button>
      ))}
    </div>
  );
}

/** The WF-11 runs that reviewed the record, each linking to its history (SCR-115) for those who may read runs. */
export function ReviewSection({
  review,
  loading,
  deciding,
}: {
  review: Review | undefined;
  loading: boolean;
  /** The person's inbox holds the task deciding the revision under review. */
  deciding: boolean;
}): ReactElement {
  const { t } = useI18n();
  const runs = review?.runs;
  return (
    <section className="section" aria-labelledby="governed-review">
      <h2 id="governed-review">{t('suspensionClosure.review.title')}</h2>
      {loading && review === undefined ? (
        <LoadingState />
      ) : runs === null ? (
        <p className="form__note">{t('suspensionClosure.review.unreadable')}</p>
      ) : runs === undefined || runs.length === 0 ? (
        <p className="form__note">{t('suspensionClosure.review.none')}</p>
      ) : (
        <ul className="link-list">
          {runs.map((run) => (
            <li key={run.id}>
              <Link to={`/approvals/instances/${run.id}`}>
                {t('suspensionClosure.review.ofRevision', { revision: run.subject.revisionNo })}
              </Link>{' '}
              <StatusBadge
                label={t(`approvals.instanceStatus.${run.status}`)}
                tone={instanceTone(run.status)}
              />
            </li>
          ))}
        </ul>
      )}
      {deciding && <p className="form__note">{t('suspensionClosure.review.youDecide')}</p>}
    </section>
  );
}
