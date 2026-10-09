import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { checkText, useSaveAction } from '@/features/identity-access/forms.ts';
import { type NarrativeTextRequest } from '@/features/projects/api/types.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { type ContributionDecision } from '../api/types.ts';
import { participationProblemMessage, requestFieldMessage } from '../problems.ts';

/** NarrativeTextRequest.TextLength. */
const TEXT_LENGTH = 2000;

interface WordsDialogProps {
  title: string;
  /** What the command does, in a sentence. */
  consequence: string;
  confirmLabel: string;
  /** `required`: the reason the entity reads (a return, a rejection, a cancellation); `none`: no reason (an acceptance). */
  reason: 'required' | 'none';
  /** AHDA's internal note, offered on a review decision only; never shown to the entity (EXT-F-128). */
  internalNote: boolean;
  /** Sends the command with the words given and the ETag it was read with. */
  run: (words: ContributionDecision) => Promise<unknown>;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
  isStale: (error: unknown) => boolean;
  danger?: boolean;
}

/**
 * A command that takes words: a cancellation's reason, or a review decision — accept with an internal note, return or
 * reject with the reason the entity reads and an internal note. It takes no field value: a reviewer never edits an
 * answer (TASK-066 D-5).
 */
export function WordsDialog(props: WordsDialogProps): ReactElement {
  return (
    <Dialog open title={props.title} onClose={props.onClose}>
      <WordsForm {...props} />
    </Dialog>
  );
}

function WordsForm({
  consequence,
  confirmLabel,
  reason: reasonMode,
  internalNote: noteOffered,
  run,
  onClose,
  onDone,
  onStale,
  isStale,
  danger = false,
}: WordsDialogProps): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(participationProblemMessage);
  const [reason, setReason] = useState('');
  const [note, setNote] = useState('');
  const fields = useFieldErrors(
    {
      reason:
        reasonMode === 'required'
          ? checkText(reason, { required: true, maxLength: TEXT_LENGTH })
          : null,
      internalNote: checkText(note, { maxLength: TEXT_LENGTH }),
    },
    formRef,
    requestFieldMessage,
  );
  const narrative = (text: string): NarrativeTextRequest | null =>
    text.trim() === '' ? null : { text: text.trim(), language };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(() =>
      run({ reason: narrative(reason), internalNote: noteOffered ? narrative(note) : null }),
    );
    if (result.ok) {
      onDone();
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={fields.summary ?? save.formError} />
      <p className="form__note">{consequence}</p>
      {reasonMode === 'required' && (
        <TextAreaField
          label={t('externalParticipation.words.reason')}
          name="reason"
          value={reason}
          required
          maxLength={TEXT_LENGTH}
          hint={t('externalParticipation.words.reasonHint')}
          error={fields.errorOf('reason')}
          onChange={(value) => {
            setReason(value);
            fields.clearServer();
          }}
        />
      )}
      {noteOffered && (
        <TextAreaField
          label={t('externalParticipation.words.internalNote')}
          name="internalNote"
          value={note}
          maxLength={TEXT_LENGTH}
          hint={t('externalParticipation.words.internalNoteHint')}
          error={fields.errorOf('internalNote')}
          onChange={(value) => {
            setNote(value);
            fields.clearServer();
          }}
        />
      )}
      <div className="form__actions">
        <button
          type="submit"
          className={danger ? 'button button--danger' : 'button button--primary'}
          disabled={save.saving}
        >
          {save.saving ? t('common.states.saving') : confirmLabel}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
