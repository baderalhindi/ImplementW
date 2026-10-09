import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { sourceApplicationsApi } from '../api/externalParticipationApi.ts';
import { type SourceApplicationDetail } from '../api/types.ts';
import { isStale, participationProblemMessage } from '../problems.ts';

interface ApplyDialogProps {
  contributionId: string;
  /** A first attempt, or another after a revalidated conflict or a refusal the source may lift. */
  retry: boolean;
  onClose: () => void;
  /** The attempt as recorded: APPLIED, CONFLICT or FAILED — every one a result, none an error (TASK-066 D-10). */
  onDone: (attempt: SourceApplicationDetail) => void;
  onStale: () => void;
}

/**
 * Applying an accepted answer to its source through the typed adapter (WF-04's task percentage). The attempt's
 * Idempotency-Key is made once per dialog: sent again after a lost answer, the API replays the same attempt and
 * applies nothing twice.
 */
export function ApplyDialog(props: ApplyDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open
      title={t(
        props.retry
          ? 'externalParticipation.apply.retryTitle'
          : 'externalParticipation.apply.title',
      )}
      onClose={props.onClose}
    >
      <ApplyForm {...props} />
    </Dialog>
  );
}

function ApplyForm({
  contributionId,
  retry,
  onClose,
  onDone,
  onStale,
}: ApplyDialogProps): ReactElement {
  const { t } = useI18n();
  const save = useSaveAction(participationProblemMessage);
  const [idempotencyKey] = useState(() => crypto.randomUUID());

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    const result = await save.run(
      async () => (await sourceApplicationsApi.apply(contributionId, idempotencyKey)).data,
    );
    if (result.ok) {
      onDone(result.value);
    } else if (isStale(result.error)) {
      onStale();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <p className="form__note">{t('externalParticipation.apply.consequence')}</p>
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving
            ? t('common.states.saving')
            : t(
                retry
                  ? 'externalParticipation.actions.applyAgain'
                  : 'externalParticipation.actions.apply',
              )}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
