import { type ReactElement, type SyntheticEvent, useRef, useState } from 'react';

import { useSaveAction } from '@/features/identity-access/forms.ts';
import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { useFieldErrors } from '@/shared/forms/useFieldErrors.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { Dialog } from '@/shared/ui/Dialog.tsx';
import {
  ReadOnlyField,
  type SelectOption,
  SelectField,
  TextAreaField,
  TextField,
} from '@/shared/ui/FormFields.tsx';
import { FormAlert } from '@/shared/ui/States.tsx';

import { concernsApi } from '../api/concernsApi.ts';
import { type ConcernDetail, type ConcernType } from '../api/types.ts';
import {
  checkConcernForm,
  concernFormValuesOf,
  type ConcernFormValues,
  emptyConcernForm,
  toConcernRequest,
} from '../concernRules.ts';
import { concernFieldMessage, concernProblemMessage, isStale } from '../problems.ts';
import { type ConcernLookups } from '../useConcernData.ts';

interface ConcernFormDialogProps {
  /** null: MOD-036 Create Issue or MOD-038 Create Challenge; a concern with its ETag: MOD-037 Edit Issue. */
  editing: ApiResponse<ConcernDetail> | null;
  /** The type a new concern is raised as; an existing one keeps its own. */
  concernType: ConcernType;
  project: Pick<ProjectSummary, 'id'>;
  lookups: ConcernLookups;
  onClose: () => void;
  onDone: (concern: ConcernDetail) => void;
  onStale: () => void;
}

const TITLES = {
  ISSUE: { create: 'issuesChallenges.form.createIssue', edit: 'issuesChallenges.form.editIssue' },
  CHALLENGE: {
    create: 'issuesChallenges.form.createChallenge',
    edit: 'issuesChallenges.form.editChallenge',
  },
} as const satisfies Record<ConcernType, { create: TranslationKey; edit: TranslationKey }>;

/**
 * MOD-036 Create Issue, MOD-038 Create Challenge and MOD-037 Edit Issue: title, description, category, priority and a
 * target resolution date. Priority is the person's to choose and change; severity is not (acceptance criterion): the
 * edit shows it as a read-only field, the server's, computed from the impact assessment, and nothing here sends one.
 * The edit re-sends every field (R-5) with the ETag it was read with.
 */
export function ConcernFormDialog(props: ConcernFormDialogProps): ReactElement {
  const { t } = useI18n();
  const type = props.editing?.data.concernType ?? props.concernType;
  return (
    <Dialog
      open
      title={t(TITLES[type][props.editing === null ? 'create' : 'edit'])}
      onClose={props.onClose}
    >
      <ConcernForm {...props} concernType={type} />
    </Dialog>
  );
}

/** A value it already names stays listed, even if no longer PUBLISHED, so an edit does not drop it unseen. */
function withCurrent(options: SelectOption[], current: string, label: (id: string) => string) {
  return current === '' || options.some((option) => option.value === current)
    ? options
    : [...options, { value: current, label: label(current) }];
}

function ConcernForm({
  editing,
  concernType,
  project,
  lookups,
  onClose,
  onDone,
  onStale,
}: ConcernFormDialogProps): ReactElement {
  const { t, language } = useI18n();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSaveAction(concernProblemMessage);
  const today = todayUtc();
  const before = editing?.data ?? null;
  const [values, setValues] = useState<ConcernFormValues>(() =>
    before === null ? emptyConcernForm() : concernFormValuesOf(before),
  );
  const fields = useFieldErrors(
    checkConcernForm(values, today, before),
    formRef,
    concernFieldMessage,
  );
  const creatingBlind = before === null && !lookups.readable;

  const set = (field: keyof ConcernFormValues) => (value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    fields.clearServer();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (!fields.attempt()) {
      return;
    }
    const request = toConcernRequest(values, language, before);
    const result = await save.run(async () =>
      before === null
        ? (
            await concernsApi.raise({
              ...request,
              projectId: project.id,
              concernType,
              impacts: [],
            })
          ).data
        : (await concernsApi.update(before.id, request, editing?.etag ?? null)).data,
    );
    if (result.ok) {
      onDone(result.value);
    } else if (isStale(result.error)) {
      onStale();
    } else {
      fields.showServer(result.error);
    }
  };

  return (
    <form ref={formRef} className="form" noValidate onSubmit={(event) => void submit(event)}>
      <FormAlert message={fields.summary ?? save.formError} />
      {before === null && (
        <p className="form__note">
          {t(
            concernType === 'ISSUE'
              ? 'issuesChallenges.form.createIssueIntro'
              : 'issuesChallenges.form.createChallengeIntro',
          )}
        </p>
      )}
      <TextField
        label={t('issuesChallenges.fields.title')}
        name="title"
        required
        dir="auto"
        value={values.title}
        onChange={set('title')}
        error={fields.errorOf('title')}
      />
      <TextAreaField
        label={t('issuesChallenges.fields.description')}
        name="description"
        required
        maxLength={TEXT_LENGTH}
        hint={t(
          concernType === 'ISSUE'
            ? 'issuesChallenges.form.issueDescriptionHint'
            : 'issuesChallenges.form.challengeDescriptionHint',
        )}
        value={values.description}
        onChange={set('description')}
        error={fields.errorOf('description')}
      />
      {creatingBlind ? (
        <p className="form__note">{t('issuesChallenges.form.cataloguesUnreadable')}</p>
      ) : (
        <>
          <SelectField
            label={t('issuesChallenges.fields.category')}
            name="categoryItemId"
            required
            value={values.categoryItemId}
            options={withCurrent(lookups.categoryOptions, values.categoryItemId, lookups.itemLabel)}
            placeholder={t('issuesChallenges.form.choose')}
            onChange={set('categoryItemId')}
            error={fields.errorOf('categoryItemId')}
          />
          <SelectField
            label={t('issuesChallenges.fields.priority')}
            name="priorityItemId"
            required
            value={values.priorityItemId}
            options={withCurrent(lookups.priorityOptions, values.priorityItemId, lookups.itemLabel)}
            placeholder={t('issuesChallenges.form.choose')}
            hint={t('issuesChallenges.form.priorityHint')}
            onChange={set('priorityItemId')}
            error={fields.errorOf('priorityItemId')}
          />
        </>
      )}
      {before === null ? (
        <p className="form__note">{t('issuesChallenges.form.severityOnCreate')}</p>
      ) : (
        <ReadOnlyField
          label={t('issuesChallenges.fields.severity')}
          name="severity"
          value={
            before.severityItemId === null
              ? t('issuesChallenges.severity.none')
              : lookups.itemLabel(before.severityItemId)
          }
          hint={t('issuesChallenges.form.severityReadOnly')}
        />
      )}
      <TextField
        label={t('issuesChallenges.fields.targetResolutionDate')}
        name="targetResolutionDate"
        type="date"
        hint={t('issuesChallenges.form.targetHint')}
        value={values.targetResolutionDate}
        onChange={set('targetResolutionDate')}
        error={fields.errorOf('targetResolutionDate')}
      />
      <div className="form__actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={save.saving || creatingBlind}
        >
          {save.saving ? t('common.states.saving') : t('common.actions.save')}
        </button>
        <button type="button" className="button" disabled={save.saving} onClick={onClose}>
          {t('common.actions.cancel')}
        </button>
      </div>
    </form>
  );
}
