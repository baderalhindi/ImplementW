import { type ReactElement, useId, useState } from 'react';

import { fieldErrorsOf, type FieldErrors } from '@/features/identity-access/problems.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { roleLabel, SHARE_ROLES } from '../presentation.ts';
import { reportProblemMessage } from '../problems.ts';

// MOD-062 Save Report View (FG-02 §13.6): a name, in the language it is written in (ADR-012), and the roles it is
// shared with — none keeps it private. A view is configuration only: it holds no report data, and whoever opens it
// runs it with their own access (TBC-RPT-20 as TASK-072 enables it: the configuration transfers, the authorisation
// does not). Only ADR-019's saved-view roles may receive it.

/** The API's NarrativeTextRequest.TextLength. */
const NAME_MAX_LENGTH = 2000;

export interface SaveViewValues {
  name: string;
  language: 'ar' | 'en';
  sharedWithRoleCodes: string[];
}

interface SaveViewFormProps {
  initial: SaveViewValues;
  save: (values: SaveViewValues) => Promise<void>;
  onClose: () => void;
}

function SaveViewForm({ initial, save, onClose }: SaveViewFormProps): ReactElement {
  const { t } = useI18n();
  const shareHintId = useId();
  const [values, setValues] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({});

  const nameError =
    errors['name.text'] ??
    errors.name ??
    (values.name.length > NAME_MAX_LENGTH ? t('common.fieldErrors.maxLength') : undefined);
  const shareError = Object.entries(errors).find(([field]) =>
    field.startsWith('sharedWithRoleCodes'),
  )?.[1];

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        if (values.name.trim() === '') {
          setErrors({ name: t('common.fieldErrors.required') });
          return;
        }
        if (nameError !== undefined) {
          return;
        }
        setBusy(true);
        setProblem(null);
        setErrors({});
        save({ ...values, name: values.name.trim() }).catch((error: unknown) => {
          setProblem(reportProblemMessage(error, t));
          setErrors(fieldErrorsOf(error, t));
          setBusy(false);
        });
      }}
    >
      <p className="form__note">{t('reports.saveView.intro')}</p>
      <FormAlert message={problem} />
      <TextField
        label={t('reports.saveView.name')}
        name="name"
        value={values.name}
        dir="auto"
        required
        error={nameError}
        onChange={(name) => {
          setValues((current) => ({ ...current, name }));
          setErrors({});
        }}
      />
      <RadioGroupField
        label={t('reports.saveView.nameLanguage')}
        name="nameLanguage"
        value={values.language}
        onChange={(language) => {
          setValues((current) => ({ ...current, language: language === 'ar' ? 'ar' : 'en' }));
        }}
        options={[
          { value: 'ar', label: t('common.languages.ar') },
          { value: 'en', label: t('common.languages.en') },
        ]}
      />
      <fieldset className="field field--group" aria-describedby={shareHintId}>
        <legend className="field__label">{t('reports.saveView.shareWith')}</legend>
        <p id={shareHintId} className="field__hint">
          {t('reports.saveView.shareHint')}
        </p>
        <div className="field__options">
          {SHARE_ROLES.map((role) => (
            <label key={role} className="field__option">
              <input
                type="checkbox"
                name="sharedWithRoleCodes"
                value={role}
                checked={values.sharedWithRoleCodes.includes(role)}
                onChange={(event) => {
                  const checked = event.target.checked;
                  setValues((current) => ({
                    ...current,
                    sharedWithRoleCodes: SHARE_ROLES.filter((candidate) =>
                      candidate === role
                        ? checked
                        : current.sharedWithRoleCodes.includes(candidate),
                    ),
                  }));
                }}
              />
              {roleLabel(role, t)}
            </label>
          ))}
        </div>
        {shareError !== undefined && <p className="field__error">{shareError}</p>}
      </fieldset>
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={busy}>
          {busy ? t('common.states.saving') : t('reports.saveView.save')}
        </button>
        <button type="button" className="button button--quiet" disabled={busy} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}

export function SaveViewDialog({
  open,
  title,
  onClose,
  ...form
}: Omit<SaveViewFormProps, 'onClose'> & {
  open: boolean;
  title: string;
  onClose: () => void;
}): ReactElement {
  return (
    <Dialog open={open} title={title} onClose={onClose}>
      <SaveViewForm {...form} onClose={onClose} />
    </Dialog>
  );
}
