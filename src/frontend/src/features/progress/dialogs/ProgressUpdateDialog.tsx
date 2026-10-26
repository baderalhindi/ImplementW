import { type ReactElement, type SyntheticEvent, useEffect, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { fieldMessage } from '@/features/identity-access/problems.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { ApiError } from '@/shared/api/httpClient.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { RadioGroupField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { progressApi } from '../api/progressApi.ts';
import { type ProgressSubmissionDetail } from '../api/types.ts';
import { FigureDetails, type IntakeDate } from '../components/Figures.tsx';
import { PERCENT_FIELD_MESSAGES, isStaleSubmission, progressProblemMessage } from '../problems.ts';
import {
  checkProgressUpdate,
  differsFrom,
  type FigureSource,
  hasErrors,
  NARRATIVE_FIELD,
  OVERRIDE_PERCENT_FIELD,
  OVERRIDE_REASON_FIELD,
  type ProgressUpdateCodes,
  type ProgressUpdateValues,
  toRequest,
  valuesOf,
} from '../progressUpdate.ts';

/** MOD-021 saves the DRAFT; MOD-020 saves what changed and submits it. */
export type ProgressUpdateMode = 'edit' | 'submit';

const TITLES: Record<ProgressUpdateMode, TranslationKey> = {
  edit: 'progress.update.editTitle',
  submit: 'progress.update.submitTitle',
};

interface ProgressUpdateDialogProps {
  open: boolean;
  mode: ProgressUpdateMode;
  submission: ProgressSubmissionDetail;
  etag: string | null;
  periodLabel: string;
  intakeDate: IntakeDate;
  onClose: () => void;
  onDone: (notice: TranslationKey) => void;
  /** The revision moved on since it was read: the screen reads it again. */
  onStale: () => void;
}

/**
 * MOD-020 Submit Progress Update and MOD-021 Edit Progress Update Draft. The figures are derived and shown, never
 * typed (ADR-009); a person writes the narrative and, if the roll-up is wrong, an override with its reason. The draft
 * is pre-filled from the last published period, so confirming is the default action (ADR-017).
 */
export function ProgressUpdateDialog(props: ProgressUpdateDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog open={props.open} title={t(TITLES[props.mode])} onClose={props.onClose}>
      {props.open && <ProgressUpdateBody {...props} />}
    </Dialog>
  );
}

/** A field path as the API names it: a narrative's `.text` or `.language` belongs to the narrative's input. */
function inputOf(field: string): string {
  return field.replace(/\.(text|language)$/, '');
}

/** The API's field codes of a refusal, by input. */
function serverCodesOf(error: unknown): ProgressUpdateCodes {
  const codes: ProgressUpdateCodes = {};
  if (error instanceof ApiError) {
    for (const issue of error.fieldIssues) {
      codes[inputOf(issue.field)] ??= issue.code;
    }
  }
  return codes;
}

function ProgressUpdateBody({
  mode,
  submission,
  etag,
  periodLabel,
  intakeDate,
  onClose,
  onDone,
  onStale,
}: ProgressUpdateDialogProps): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(progressProblemMessage);
  const [values, setValues] = useState<ProgressUpdateValues>(() => valuesOf(submission));
  const [attempted, setAttempted] = useState(false);
  const [serverCodes, setServerCodes] = useState<ProgressUpdateCodes>({});
  const [focusRequest, setFocusRequest] = useState(0);

  const codes = checkProgressUpdate(values);
  const overriding = values.figureSource === 'override';

  useEffect(() => {
    if (focusRequest > 0) {
      formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
    }
  }, [focusRequest]);

  const set = (field: keyof ProgressUpdateValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    setServerCodes({});
  };

  // A value of the wrong shape is flagged as it is typed; an empty required one once saving is tried. The API's
  // refusals land on the input they name.
  const codeOf = (field: string): string | null => {
    const code = codes[field] ?? null;
    if (code !== null && (attempted || code !== 'REQUIRED')) {
      return code;
    }
    return serverCodes[field] ?? null;
  };
  const errorOf = (field: string): string | undefined => {
    const code = codeOf(field);
    if (code === null) {
      return undefined;
    }
    const percentKey = field === OVERRIDE_PERCENT_FIELD ? PERCENT_FIELD_MESSAGES[code] : undefined;
    return percentKey === undefined ? fieldMessage(code, t) : t(percentKey);
  };
  const invalidCount = attempted
    ? Object.values(codes).filter((code) => typeof code === 'string').length
    : 0;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (hasErrors(codes)) {
      setAttempted(true);
      setFocusRequest((current) => current + 1);
      return;
    }
    const changed = differsFrom(values, submission);
    const result = await save.run(async () => {
      let version = etag;
      if (changed) {
        version = (
          await progressApi.update(submission.id, toRequest(values, language, submission), version)
        ).etag;
      }
      if (mode === 'submit') {
        await progressApi.submit(submission.id, version);
      }
    });
    if (result.ok) {
      onDone(mode === 'submit' ? 'progress.done.submitted' : 'progress.done.saved');
    } else if (isStaleSubmission(result.error)) {
      onStale();
    } else {
      setServerCodes(serverCodesOf(result.error));
      setFocusRequest((current) => current + 1);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <p className="form__note">
        {t('progress.update.period', { period: periodLabel, revision: submission.revisionNo })}
      </p>
      <p className="form__note">{t('progress.update.prefilled')}</p>
      <FormAlert
        message={
          invalidCount > 0 ? t('common.form.fixErrors', { count: invalidCount }) : save.formError
        }
      />
      <dl className="details">
        <FigureDetails
          figures={{
            plannedPercent: submission.plannedPercent,
            actualPercent: submission.actualPercentCalculated,
            isOverridden: false,
            isOpeningPosition: submission.projectIntakeId !== null,
          }}
          intakeDate={intakeDate}
        />
      </dl>
      <p className="form__note">{t('progress.update.derived')}</p>
      <RadioGroupField
        label={t('progress.update.figureSource')}
        name="figureSource"
        required
        value={values.figureSource}
        options={[
          { value: 'calculated', label: t('progress.update.useCalculated') },
          { value: 'override', label: t('progress.update.useOverride') },
        ]}
        onChange={(value) => {
          set('figureSource')(
            value === 'override' ? 'override' : ('calculated' satisfies FigureSource),
          );
        }}
      />
      {overriding && (
        <>
          <TextField
            label={t('progress.update.overridePercent')}
            name="overridePercent"
            required
            value={values.overridePercent}
            onChange={set('overridePercent')}
            inputMode="decimal"
            dir="ltr"
            hint={t('progress.update.overridePercentHint')}
            error={errorOf(OVERRIDE_PERCENT_FIELD)}
          />
          <TextAreaField
            label={t('progress.update.overrideReason')}
            name="overrideReason"
            required
            rows={3}
            maxLength={TEXT_LENGTH}
            value={values.overrideReason}
            onChange={set('overrideReason')}
            hint={t('progress.update.overrideReasonHint')}
            error={errorOf(OVERRIDE_REASON_FIELD)}
          />
        </>
      )}
      <TextAreaField
        label={t('progress.update.narrative')}
        name="narrative"
        maxLength={TEXT_LENGTH}
        value={values.narrative}
        onChange={set('narrative')}
        error={errorOf(NARRATIVE_FIELD)}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={save.saving}>
          {save.saving
            ? t('common.states.saving')
            : mode === 'submit'
              ? t('progress.update.confirmSubmit')
              : t('progress.update.confirmSave')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
