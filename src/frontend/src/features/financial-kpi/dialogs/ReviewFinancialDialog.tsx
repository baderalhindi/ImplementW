import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { checkText, useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { financialKpiApi } from '../api/financialKpiApi.ts';
import { type FinancialProgressUpdateDetail } from '../api/types.ts';
import { UpdateFigures } from '../components/UpdateFigures.tsx';
import { financialKpiProblemMessage, isStale } from '../problems.ts';

type Decision = 'publish' | 'return';

interface ReviewFinancialDialogProps {
  open: boolean;
  update: FinancialProgressUpdateDetail;
  etag: string | null;
  periodLabel: string;
  personName: (id: string | null) => string;
  onClose: () => void;
  onDone: (notice: TranslationKey) => void;
  onStale: () => void;
}

/**
 * AHDA's review of a period's financial figures (ADR-013, TASK-052 D-15): a SUBMITTED update is taken into review; one
 * under review is published, writing the period's immutable official snapshot rated under the thresholds in force, or
 * returned with the reason, which opens the next revision. The reviewer sees the figures, their states and provenance.
 */
export function ReviewFinancialDialog(props: ReviewFinancialDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t('financialKpi.review.title')} onClose={props.onClose}>
      {props.open && <ReviewBody {...props} />}
    </Dialog>
  );
}

function ReviewBody({
  update,
  etag,
  periodLabel,
  personName,
  onClose,
  onDone,
  onStale,
}: ReviewFinancialDialogProps): ReactElement {
  const { t, language } = useI18n();
  const save = useSaveAction(financialKpiProblemMessage);
  const [decision, setDecision] = useState<Decision | null>(null);
  const [reason, setReason] = useState('');
  const [attempted, setAttempted] = useState(false);
  const underReview = update.status === 'UNDER_REVIEW';

  const reasonCode =
    decision === 'return' ? checkText(reason, { required: true, maxLength: TEXT_LENGTH }) : null;
  const shownReasonCode = reasonCode === 'REQUIRED' && !attempted ? null : reasonCode;
  const reasonError =
    shownReasonCode === null
      ? (save.fieldErrors.reason ?? save.fieldErrors['reason.text'])
      : fieldMessage(shownReasonCode, t);

  const act = () => {
    if (!underReview) {
      return financialKpiApi.startReview(update.id, etag);
    }
    return decision === 'publish'
      ? financialKpiApi.publishUpdate(update.id, etag)
      : financialKpiApi.returnUpdate(update.id, narrativeRequest(reason, language), etag);
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
          ? 'financialKpi.done.reviewStarted'
          : decision === 'publish'
            ? 'financialKpi.done.published'
            : 'financialKpi.done.returned',
      );
    } else if (isStale(result.error)) {
      onStale();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">
        {t('financialKpi.update.period', { period: periodLabel, revision: update.revisionNo })}
      </p>
      <FormAlert message={save.formError} />
      <UpdateFigures update={update} personName={personName} />
      {underReview ? (
        <>
          <RadioGroupField
            label={t('financialKpi.review.decision')}
            name="decision"
            required
            value={decision ?? ''}
            options={[
              { value: 'publish', label: t('financialKpi.review.publish') },
              { value: 'return', label: t('financialKpi.review.return') },
            ]}
            error={attempted && decision === null ? fieldMessage('REQUIRED', t) : undefined}
            onChange={(value) => {
              setDecision(value === 'publish' || value === 'return' ? value : null);
            }}
          />
          {decision === 'publish' && (
            <p className="form__note">{t('financialKpi.review.publishConsequence')}</p>
          )}
          {decision === 'return' && (
            <TextAreaField
              label={t('financialKpi.review.reason')}
              name="reason"
              required
              rows={3}
              maxLength={TEXT_LENGTH}
              value={reason}
              onChange={setReason}
              hint={t('financialKpi.review.returnConsequence')}
              error={reasonError}
            />
          )}
        </>
      ) : (
        <p className="form__note">{t('financialKpi.review.startConsequence')}</p>
      )}
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving
            ? t('common.states.saving')
            : underReview
              ? t('financialKpi.review.confirm')
              : t('financialKpi.review.start')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
