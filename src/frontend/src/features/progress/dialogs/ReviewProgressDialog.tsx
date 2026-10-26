import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { checkText, useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { progressApi } from '../api/progressApi.ts';
import { type ProgressSubmissionDetail } from '../api/types.ts';
import { FigureDetails, type IntakeDate } from '../components/Figures.tsx';
import { isStaleSubmission, progressProblemMessage } from '../problems.ts';
import { narrativeRequest } from '../progressUpdate.ts';

/** The decision on a revision under review: one command each (R-4). */
type Decision = 'publish' | 'return';

interface ReviewProgressDialogProps {
  open: boolean;
  submission: ProgressSubmissionDetail;
  etag: string | null;
  periodLabel: string;
  intakeDate: IntakeDate;
  onClose: () => void;
  onDone: (notice: TranslationKey) => void;
  onStale: () => void;
}

/**
 * MOD-022 Review Progress Update, AHDA's gate (ADR-013, TASK-044 D-9): a SUBMITTED revision is taken into review; one
 * under review is published, writing the period's immutable official snapshot, or returned with the reason, which
 * opens the next revision as a DRAFT. The reviewer sees what they decide on: the figures side by side, an override
 * with its reason, and the narrative.
 */
export function ReviewProgressDialog(props: ReviewProgressDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('progress.review.title')} onClose={props.onClose}>
      {props.open && <ReviewProgressBody {...props} />}
    </Dialog>
  );
}

function ReviewProgressBody({
  submission,
  etag,
  periodLabel,
  intakeDate,
  onClose,
  onDone,
  onStale,
}: ReviewProgressDialogProps): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const save = useSaveAction(progressProblemMessage);
  const personName = usePersonNames([submission.submittedByUserId]);
  const [decision, setDecision] = useState<Decision | null>(null);
  const [reason, setReason] = useState('');
  const [attempted, setAttempted] = useState(false);
  const underReview = submission.status === 'UNDER_REVIEW';

  const reasonCode =
    decision === 'return' ? checkText(reason, { required: true, maxLength: TEXT_LENGTH }) : null;
  const shownReasonCode = reasonCode === 'REQUIRED' && !attempted ? null : reasonCode;
  const reasonError =
    shownReasonCode === null
      ? (save.fieldErrors.reason ?? save.fieldErrors['reason.text'])
      : fieldMessage(shownReasonCode, t);

  const act = () => {
    if (!underReview) {
      return progressApi.startReview(submission.id, etag);
    }
    return decision === 'publish'
      ? progressApi.publish(submission.id, etag)
      : progressApi.returnForRevision(submission.id, narrativeRequest(reason, language), etag);
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (underReview && (decision === null || reasonCode !== null)) {
      setAttempted(true);
      return;
    }
    const result = await save.run(act);
    if (result.ok) {
      onDone(
        !underReview
          ? 'progress.done.reviewStarted'
          : decision === 'publish'
            ? 'progress.done.published'
            : 'progress.done.returned',
      );
    } else if (isStaleSubmission(result.error)) {
      onStale();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">
        {t('progress.update.period', { period: periodLabel, revision: submission.revisionNo })}
      </p>
      <FormAlert message={save.formError} />
      <dl className="details">
        <FigureDetails
          figures={{
            ...submission,
            isOpeningPosition: submission.projectIntakeId !== null,
          }}
          intakeDate={intakeDate}
        />
        {submission.isOverridden && submission.overrideReason !== null && (
          <Detail term={t('progress.update.overrideReason')}>
            <span dir="auto">{submission.overrideReason.text}</span>
          </Detail>
        )}
        <Detail term={t('progress.update.narrative')}>
          {submission.narrative === null ? (
            t('common.values.none')
          ) : (
            <span dir="auto">{submission.narrative.text}</span>
          )}
        </Detail>
        <Detail term={t('progress.history.submitted')}>
          {submission.submittedAt === null
            ? t('common.values.none')
            : `${formatDateTime(submission.submittedAt)} · ${personName(submission.submittedByUserId)}`}
        </Detail>
      </dl>
      {underReview ? (
        <>
          <RadioGroupField
            label={t('progress.review.decision')}
            name="decision"
            required
            value={decision ?? ''}
            options={[
              { value: 'publish', label: t('progress.review.publish') },
              { value: 'return', label: t('progress.review.return') },
            ]}
            error={attempted && decision === null ? fieldMessage('REQUIRED', t) : undefined}
            onChange={(value) => {
              setDecision(value === 'publish' || value === 'return' ? value : null);
            }}
          />
          {decision === 'publish' && (
            <p className="form__note">{t('progress.review.publishConsequence')}</p>
          )}
          {decision === 'return' && (
            <TextAreaField
              label={t('progress.review.reason')}
              name="reason"
              required
              rows={3}
              maxLength={TEXT_LENGTH}
              value={reason}
              onChange={setReason}
              hint={t('progress.review.returnConsequence')}
              error={reasonError}
            />
          )}
        </>
      ) : (
        <p className="form__note">{t('progress.review.startConsequence')}</p>
      )}
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving
            ? t('common.states.saving')
            : underReview
              ? t('progress.review.confirm')
              : t('progress.review.start')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
