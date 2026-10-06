import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { type NarrativeTextRequest } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { TextAreaField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { checkNarrative } from '../concernRules.ts';
import { concernFieldMessage, concernProblemMessage, isStale } from '../problems.ts';

interface NarrativeCommandDialogProps {
  title: string;
  /** What the command does and does not do, one sentence each. */
  notes: string[];
  /** The API's field the text is sent as, so its refusal (400 REQUIRED) lands here. */
  field: 'resolution' | 'reason';
  label: string;
  hint: string;
  /** A returned resolution starts from the text that was sent back. */
  initialText?: string;
  confirmLabel: string;
  run: (text: NarrativeTextRequest) => Promise<unknown>;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}

/**
 * A command that carries one required text: submitting a resolution for validation, escalating (MOD-039) and resolving
 * an escalation with the management direction. A blank text is refused here before anything is sent, as the API
 * refuses it (400 REQUIRED); a record that moved on is read again.
 */
export function NarrativeCommandDialog({
  title,
  notes,
  field,
  label,
  hint,
  initialText = '',
  confirmLabel,
  run,
  onClose,
  onDone,
  onStale,
}: NarrativeCommandDialogProps): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(concernProblemMessage);
  const [text, setText] = useState(initialText);
  const fields = useFieldErrors(checkNarrative(field, text), formRef, concernFieldMessage);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const result = await save.run(() => run(narrativeRequest(text, language)));
    if (result.ok) {
      onDone();
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <Dialog open title={title} onClose={onClose}>
      <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
        <FormAlert message={fields.summary ?? save.formError} />
        <ul className="form__note risk-close__warnings">
          {notes.map((note) => (
            <li key={note}>{note}</li>
          ))}
        </ul>
        <TextAreaField
          label={label}
          name={field}
          required
          maxLength={TEXT_LENGTH}
          hint={hint}
          value={text}
          onChange={(value) => {
            setText(value);
            fields.clearServer();
          }}
          error={fields.errorOf(field)}
        />
        <div className="form__actions">
          <button type="submit" className="button button--primary" disabled={save.saving}>
            {save.saving ? t('common.states.saving') : confirmLabel}
          </button>
          <button type="button" className="button" disabled={save.saving} onClick={onClose}>
            {t('common.actions.cancel')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
