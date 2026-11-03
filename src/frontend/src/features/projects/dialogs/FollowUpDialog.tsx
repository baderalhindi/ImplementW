import { type ReactElement, type SyntheticEvent, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import { SelectField, TextAreaField, TextField } from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { followUpApi } from '../api/followUpApi.ts';
import {
  type NarrativeText,
  type ProjectDetail,
  type ProjectFollowUpRequest,
} from '../api/types.ts';
import { projectProblemMessage } from '../problems.ts';
import { TEXT_LENGTH } from '../registration.ts';

interface FollowUpValues {
  currentObstacle: string;
  nextAction: string;
  nextActionOwner: string;
  nextActionDueDate: string;
  declaredPlannedPercent: string;
  declaredActualPercent: string;
  declaredPlannedSpendPercent: string;
  declaredActualSpendPercent: string;
  entityUpdateCompliant: '' | 'true' | 'false';
  note: string;
}

type PercentField =
  | 'declaredPlannedPercent'
  | 'declaredActualPercent'
  | 'declaredPlannedSpendPercent'
  | 'declaredActualSpendPercent';

const PERCENT_FIELDS: PercentField[] = [
  'declaredPlannedPercent',
  'declaredActualPercent',
  'declaredPlannedSpendPercent',
  'declaredActualSpendPercent',
];

const PERCENT_LABELS = {
  declaredPlannedPercent: 'projects.followUp.declaredPlanned',
  declaredActualPercent: 'projects.followUp.declaredActual',
  declaredPlannedSpendPercent: 'projects.followUp.declaredPlannedSpend',
  declaredActualSpendPercent: 'projects.followUp.declaredActualSpend',
} as const;

const PERCENT_PATTERN = /^[0-9]{1,3}(\.[0-9]{1,4})?$/;

function percentString(value: number | null | undefined): string {
  return value === null || value === undefined ? '' : String(value);
}

function valuesOf(project: ProjectDetail): FollowUpValues {
  const f = project.followUp;
  return {
    currentObstacle: f?.currentObstacle?.text ?? '',
    nextAction: f?.nextAction?.text ?? '',
    nextActionOwner: f?.nextActionOwner ?? '',
    nextActionDueDate: f?.nextActionDueDate ?? '',
    declaredPlannedPercent: percentString(f?.declaredPlannedPercent),
    declaredActualPercent: percentString(f?.declaredActualPercent),
    declaredPlannedSpendPercent: percentString(f?.declaredPlannedSpendPercent),
    declaredActualSpendPercent: percentString(f?.declaredActualSpendPercent),
    entityUpdateCompliant:
      f?.entityUpdateCompliant === null || f?.entityUpdateCompliant === undefined
        ? ''
        : f.entityUpdateCompliant
          ? 'true'
          : 'false',
    note: '',
  };
}

/** A percentage as typed: blank (not available), or 0 to 100 with up to four decimals. */
function percentIssue(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' || (PERCENT_PATTERN.test(trimmed) && Number(trimmed) <= 100)
    ? null
    : 'OUT_OF_RANGE';
}

interface FollowUpDialogProps {
  open: boolean;
  project: ProjectDetail;
  etag: string | null;
  onClose: () => void;
  onSaved: () => void;
}

/** The whole follow-up, replaced in one command with an optional dated note (R3-3). A blank percentage stays blank. */
export function FollowUpDialog(props: FollowUpDialogProps): ReactElement {
  const { t } = useI18n();
  return (
    <Dialog
      open={props.open}
      title={t('projects.followUp.dialogTitle')}
      onClose={props.onClose}
      variant="drawer"
    >
      {props.open && <FollowUpBody {...props} />}
    </Dialog>
  );
}

function FollowUpBody({ project, etag, onClose, onSaved }: FollowUpDialogProps): ReactElement {
  const { t, language } = useI18n();
  const save = useSaveAction(projectProblemMessage);
  const [values, setValues] = useState<FollowUpValues>(valuesOf(project));
  const issues = Object.fromEntries(
    PERCENT_FIELDS.map((field) => [field, percentIssue(values[field])]),
  );
  const ready = Object.values(issues).every((issue) => issue === null);

  const set = (field: keyof FollowUpValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
  };
  const narrative = (text: string, before: NarrativeText | null | undefined) => {
    const trimmed = text.trim();
    if (trimmed === '') {
      return null;
    }
    return {
      text: trimmed,
      language:
        before?.text === trimmed
          ? before.language.toLowerCase() === 'ar'
            ? ('ar' as const)
            : ('en' as const)
          : language,
    };
  };
  const number = (value: string) => (value.trim() === '' ? null : Number(value.trim()));

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!ready) {
      return;
    }
    const request: ProjectFollowUpRequest = {
      currentObstacle: narrative(values.currentObstacle, project.followUp?.currentObstacle),
      nextAction: narrative(values.nextAction, project.followUp?.nextAction),
      nextActionOwner: values.nextActionOwner.trim() === '' ? null : values.nextActionOwner.trim(),
      nextActionDueDate: values.nextActionDueDate === '' ? null : values.nextActionDueDate,
      declaredPlannedPercent: number(values.declaredPlannedPercent),
      declaredActualPercent: number(values.declaredActualPercent),
      declaredPlannedSpendPercent: number(values.declaredPlannedSpendPercent),
      declaredActualSpendPercent: number(values.declaredActualSpendPercent),
      declaredAsOf: null,
      entityUpdateCompliant:
        values.entityUpdateCompliant === '' ? null : values.entityUpdateCompliant === 'true',
      note: narrative(values.note, null),
    };
    const result = await save.run(() => followUpApi.update(project.id, request, etag));
    if (result.ok) {
      onSaved();
    }
  };

  return (
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={save.formError} />
      <TextAreaField
        label={t('projects.followUp.obstacle')}
        name="currentObstacle"
        maxLength={TEXT_LENGTH}
        value={values.currentObstacle}
        onChange={set('currentObstacle')}
        error={save.fieldErrors['currentObstacle.text']}
      />
      <TextAreaField
        label={t('projects.followUp.nextAction')}
        name="nextAction"
        maxLength={TEXT_LENGTH}
        value={values.nextAction}
        onChange={set('nextAction')}
        error={save.fieldErrors['nextAction.text']}
      />
      <TextField
        label={t('projects.followUp.nextActionOwner')}
        name="nextActionOwner"
        dir="auto"
        value={values.nextActionOwner}
        onChange={set('nextActionOwner')}
        error={save.fieldErrors.nextActionOwner}
      />
      <TextField
        label={t('projects.followUp.nextActionDueDate')}
        name="nextActionDueDate"
        type="date"
        dir="ltr"
        value={values.nextActionDueDate}
        onChange={set('nextActionDueDate')}
        error={save.fieldErrors.nextActionDueDate}
      />
      {PERCENT_FIELDS.map((field) => (
        <TextField
          key={field}
          label={t(PERCENT_LABELS[field])}
          name={field}
          dir="ltr"
          inputMode="decimal"
          value={values[field]}
          hint={t('projects.followUp.percentHint')}
          onChange={set(field)}
          error={
            issues[field] === null
              ? save.fieldErrors[field]
              : t('projects.import.issue.PERCENT_OUT_OF_RANGE')
          }
        />
      ))}
      <SelectField
        label={t('projects.followUp.compliance')}
        name="entityUpdateCompliant"
        value={values.entityUpdateCompliant}
        placeholder={t('projects.followUp.notAvailable')}
        options={[
          { value: 'true', label: t('projects.followUp.yes') },
          { value: 'false', label: t('projects.followUp.no') },
        ]}
        hint={t('projects.followUp.complianceHint')}
        onChange={set('entityUpdateCompliant')}
      />
      <TextAreaField
        label={t('projects.followUp.note')}
        name="note"
        maxLength={TEXT_LENGTH}
        value={values.note}
        hint={t('projects.followUp.noteHint')}
        onChange={set('note')}
        error={save.fieldErrors['note.text']}
      />
      <div className="form__actions">
        <button type="submit" className="button button--primary" disabled={!ready || save.saving}>
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
